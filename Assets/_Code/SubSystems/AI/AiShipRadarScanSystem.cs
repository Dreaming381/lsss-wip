using System.Collections.Generic;
using System.Linq;
using Debug = UnityEngine.Debug;
using Latios;
using Latios.Psyshock;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine.Profiling;

//Todo: There are a bunch of planned physics improvements that can simplify what we are doing here.

namespace Lsss
{
    public class AiShipRadarScanSystem : SubSystem
    {
        private List<FactionShipsCollisionLayer> m_factionsCache = new List<FactionShipsCollisionLayer>();

        private EntityQuery m_factions;
        private EntityQuery m_radars;

        protected override void OnUpdate()
        {
            Profiler.BeginSample("AiShipRadarScanSystem_OnUpdate");

            var backup = Dependency;
            Dependency = default;
            Entities.WithAll<FactionTag>().WithStoreEntityQueryInField(ref m_factions).ForEach((Entity entity, int entityInQueryIndex) =>
            {
                if (entityInQueryIndex == 0)
                    Dependency = backup;

                m_factionsCache.Add(EntityManager.GetCollectionComponent<FactionShipsCollisionLayer>(entity, true));
            }).WithoutBurst().Run();
            var factionEntities = m_factions.ToEntityArray(Allocator.TempJob);

            var wallLayer            = sceneBlackboardEntity.GetCollectionComponent<WallCollisionLayer>(true).layer;
            var radarsCdfe           = GetComponentDataFromEntity<AiShipRadar>(true);
            var radarScanResultsCdfe = GetComponentDataFromEntity<AiShipRadarScanResults>(false);
            var ltwCdfe              = GetComponentDataFromEntity<LocalToWorld>(true);

            //Todo: IJobChunk with MemClear?
            Entities.WithAll<AiRadarTag>().ForEach((ref AiShipRadarScanResults results) =>
            {
                results              = default;
                results.nearestEnemy = Entity.Null;
            }).WithName("ScanClear").ScheduleParallel();

            JobHandle rootDependency = Dependency;
            var       finalHandles   = new NativeList<JobHandle>(Allocator.TempJob);
            JobHandle resultsHandle  = Dependency;

            for (int i = 0; i < factionEntities.Length; i++)
            {
                Profiler.BeginSample($"faction_A_{i}");
                var factionMember     = new FactionMember { factionEntity = factionEntities[i] };
                var buildRadarLayerJh                                     = BuildRadarLayer(factionMember, out CollisionLayer radarLayer, rootDependency);

                var friendsLineOfSightBodies = new NativeList<ColliderBody>(Allocator.TempJob);
                var friendsOccluded          = new NativeList<bool>(Allocator.TempJob);

                var scanFriendsProcessor = new ScanFriendsProcessor
                {
                    lineOfSightBodies = friendsLineOfSightBodies,
                    radars            = radarsCdfe
                };
                var friendsJh                = Physics.FindPairs(radarLayer, m_factionsCache[i].layer, scanFriendsProcessor).ScheduleSingle(buildRadarLayerJh);
                var prepFriendsVisibilityJob = new ResizeOccludedListToBodySizeJob
                {
                    lineOfSightBodies = friendsLineOfSightBodies,
                    occluded          = friendsOccluded
                };
                var prepFriendsJh1 = prepFriendsVisibilityJob.Schedule(friendsJh);
                var prepFriendsJh2 = Physics.BuildCollisionLayer(friendsLineOfSightBodies.AsDeferredJobArray()).WithRemapArray(out NativeArray<int> friendsLosBodiesSrcIndices,
                                                                                                                               Allocator.TempJob)
                                     .ScheduleParallel(out CollisionLayer friendsLineOfSightLayer, Allocator.TempJob, friendsJh);
                var scanFriendsVisibileProcessor = new ScanLineOfSightProcessor
                {
                    occluded        = friendsOccluded.AsDeferredJobArray(),
                    remapSrcIndices = friendsLosBodiesSrcIndices
                };
                friendsJh = Physics.FindPairs(friendsLineOfSightLayer, wallLayer, scanFriendsVisibileProcessor)
                            .ScheduleParallel(JobHandle.CombineDependencies(prepFriendsJh1, prepFriendsJh2));
                var friendsJh1 = friendsLineOfSightLayer.Dispose(friendsJh);
                var friendsJh2 = friendsLosBodiesSrcIndices.Dispose(friendsJh);

                var updateFriendsJob = new UpdateFriendsJob
                {
                    lineOfSightBodies    = friendsLineOfSightBodies.AsDeferredJobArray(),
                    occluded             = friendsOccluded.AsDeferredJobArray(),
                    radarScanResultsCdfe = radarScanResultsCdfe
                };
                resultsHandle = updateFriendsJob.Schedule(JobHandle.CombineDependencies(friendsJh1, friendsJh2, resultsHandle));
                finalHandles.Add(friendsLineOfSightBodies.Dispose(resultsHandle));
                finalHandles.Add(friendsOccluded.Dispose(resultsHandle));

                for (int j = 0; j < factionEntities.Length; j++)
                {
                    if (j == i)
                    {
                        continue;
                    }

                    var enemiesLineOfSightBodies = new NativeList<ColliderBody>(Allocator.TempJob);
                    var enemiesOccluded          = new NativeList<bool>(Allocator.TempJob);
                    var scannedEnemies           = new NativeList<ScannedEnemy>(Allocator.TempJob);

                    var scanEnemiesProcessor = new ScanEnemiesProcessor
                    {
                        lineOfSightBodies = enemiesLineOfSightBodies,
                        radars            = radarsCdfe,
                        scannedEnemies    = scannedEnemies
                    };

                    var enemiesJh                = Physics.FindPairs(radarLayer, m_factionsCache[j].layer, scanEnemiesProcessor).ScheduleSingle(buildRadarLayerJh);
                    var prepEnemiesVisibilityJob = new ResizeOccludedListToBodySizeJob
                    {
                        lineOfSightBodies = enemiesLineOfSightBodies,
                        occluded          = enemiesOccluded
                    };
                    var prepEnemiesJh1 = prepEnemiesVisibilityJob.Schedule(enemiesJh);
                    var prepEnemiesJh2 = Physics.BuildCollisionLayer(enemiesLineOfSightBodies.AsDeferredJobArray()).WithRemapArray(out NativeArray<int> enemiesLosBodiesSrcIndices,
                                                                                                                                   Allocator.TempJob)
                                         .ScheduleParallel(out CollisionLayer enemiesLineOfSightLayer, Allocator.TempJob, friendsJh);
                    var scanEnemiesVisibileProcessor = new ScanLineOfSightProcessor
                    {
                        occluded        = enemiesOccluded.AsDeferredJobArray(),
                        remapSrcIndices = enemiesLosBodiesSrcIndices
                    };
                    enemiesJh = Physics.FindPairs(enemiesLineOfSightLayer, wallLayer, scanEnemiesVisibileProcessor)
                                .ScheduleParallel(JobHandle.CombineDependencies(prepEnemiesJh1, prepEnemiesJh2));
                    var enemiesJh1 = enemiesLineOfSightLayer.Dispose(enemiesJh);
                    var enemiesJh2 = enemiesLosBodiesSrcIndices.Dispose(enemiesJh);

                    var updateEnemiesJob = new UpdateEnemiesJob
                    {
                        lineOfSightBodies    = enemiesLineOfSightBodies.AsDeferredJobArray(),
                        ltwCdfe              = ltwCdfe,
                        occluded             = enemiesOccluded.AsDeferredJobArray(),
                        radarCdfe            = radarsCdfe,
                        radarScanResultsCdfe = radarScanResultsCdfe,
                        scannedEnemies       = scannedEnemies.AsDeferredJobArray()
                    };
                    resultsHandle = updateEnemiesJob.Schedule(JobHandle.CombineDependencies(enemiesJh1, enemiesJh2, resultsHandle));
                    finalHandles.Add(enemiesLineOfSightBodies.Dispose(resultsHandle));
                    finalHandles.Add(enemiesOccluded.Dispose(resultsHandle));
                    finalHandles.Add(scannedEnemies.Dispose(resultsHandle));
                }
                finalHandles.Add(radarLayer.Dispose(resultsHandle));
                Profiler.EndSample();
            }

            Dependency = JobHandle.CombineDependencies(finalHandles);

            finalHandles.Dispose();
            factionEntities.Dispose();
            m_factionsCache.Clear();
            Profiler.EndSample();
        }

        private JobHandle BuildRadarLayer(FactionMember factionMember, out CollisionLayer layer, JobHandle inputDeps)
        {
            m_radars.SetSharedComponentFilter(factionMember);
            int count  = m_radars.CalculateEntityCount();
            var bodies = new NativeArray<ColliderBody>(count, Allocator.TempJob);
            var aabbs  = new NativeArray<Aabb>(count, Allocator.TempJob);
            var jh     = Entities.WithAll<AiRadarTag>().WithSharedComponentFilter(factionMember).WithStoreEntityQueryInField(ref m_radars)
                         .ForEach((Entity e, int entityInQueryIndex, in AiShipRadar radar, in LocalToWorld ltw) =>
            {
                var transform = new RigidTransform(quaternion.LookRotationSafe(ltw.Forward, ltw.Up), ltw.Position);
                var sphere    = new SphereCollider(0f, radar.distance);
                if (radar.cosFov < 0f)
                {
                    //Todo: Create tighter bounds here too.
                    aabbs[entityInQueryIndex] = Physics.AabbFrom(sphere, transform);
                }
                else
                {
                    //Compute aabb of vertex and spherical cap points which are extreme points
                    float3 forward             = math.forward(transform.rot);
                    bool3  positiveOnSphereCap = forward > radar.cosFov;
                    bool3  negativeOnSphereCap = -forward > radar.cosFov;
                    float3 min                 = math.select(0f, -radar.distance, negativeOnSphereCap);
                    float3 max                 = math.select(0f, radar.distance, positiveOnSphereCap);
                    Aabb   aabb                = new Aabb(min, max);

                    //Compute aabb of circle base
                    float4 cos                = new float4(forward, radar.cosFov);
                    float4 sinSq              = 1f - (cos * cos);
                    float4 sin                = math.sqrt(sinSq);
                    float3 center             = forward * radar.distance * radar.cosFov;
                    float  radius             = radar.distance * sin.w;
                    float3 extents            = sin.xyz * radius;
                    min                       = center - extents;
                    max                       = center + extents;
                    aabb.min                  = math.min(aabb.min, min) + transform.pos;
                    aabb.max                  = math.max(aabb.max, max) + transform.pos;
                    aabbs[entityInQueryIndex] = aabb;
                }

                bodies[entityInQueryIndex] = new ColliderBody
                {
                    collider  = sphere,
                    entity    = e,
                    transform = transform
                };
            }).ScheduleParallel(inputDeps);
            jh = Physics.BuildCollisionLayer(bodies, aabbs).ScheduleParallel(out layer, Allocator.TempJob, jh);
            jh = JobHandle.CombineDependencies(bodies.Dispose(jh), aabbs.Dispose(jh));
            return jh;
        }

        //ScheduleSingle only
        // Assumes A is radar, and B is ship
        private struct ScanFriendsProcessor : IFindPairsProcessor
        {
            public NativeList<ColliderBody>                        lineOfSightBodies;
            [ReadOnly] public ComponentDataFromEntity<AiShipRadar> radars;

            public void Execute(FindPairsResult result)
            {
                var    radar       = radars[result.entityA];
                float3 radarToShip = result.bodyB.transform.pos - result.bodyA.transform.pos;
                bool   isInRange   = math.lengthsq(radarToShip) < radar.friendCrossHairsDistanceFilter * radar.friendCrossHairsDistanceFilter;
                bool   isInView    =
                    math.dot(math.normalize(radarToShip),
                             math.forward(math.mul(result.bodyA.transform.rot, radar.crossHairsForwardDirectionBias))) > radar.friendCrossHairsCosFovFilter;

                if (isInRange && isInView)
                {
                    lineOfSightBodies.Add(new ColliderBody
                    {
                        collider  = new CapsuleCollider(0f, radarToShip, 0f),
                        entity    = result.entityA,
                        transform = new RigidTransform(quaternion.identity, result.bodyA.transform.pos)
                    });
                }
            }
        }

        [BurstCompile]
        private struct ResizeOccludedListToBodySizeJob : IJob
        {
            [ReadOnly] public NativeList<ColliderBody> lineOfSightBodies;
            public NativeList<bool>                    occluded;

            public void Execute()
            {
                int count = lineOfSightBodies.Length;
                occluded.Resize(count, NativeArrayOptions.ClearMemory);
            }
        }

        //Assumes A is line of sight, and B is wall
        private struct ScanLineOfSightProcessor : IFindPairsProcessor
        {
            [NativeDisableParallelForRestriction] public NativeArray<bool> occluded;
            [ReadOnly] public NativeArray<int>                             remapSrcIndices;

            public void Execute(FindPairsResult result)
            {
                if (Physics.DistanceBetween(result.bodyA.collider, result.bodyA.transform, result.bodyB.collider, result.bodyB.transform, 0f, out _))
                {
                    int srcIndex       = remapSrcIndices[result.bodyAIndex];
                    occluded[srcIndex] = true;
                }
            }
        }

        private struct ScannedEnemy
        {
            public Entity         enemy;
            public RigidTransform enemyTransform;
        }

        //ScheduleSingle only
        // Assumes A is radar, and B is ship
        private struct ScanEnemiesProcessor : IFindPairsProcessor
        {
            public NativeList<ColliderBody>                        lineOfSightBodies;
            public NativeList<ScannedEnemy>                        scannedEnemies;
            [ReadOnly] public ComponentDataFromEntity<AiShipRadar> radars;

            public void Execute(FindPairsResult result)
            {
                var    radar       = radars[result.entityA];
                float3 radarToShip = result.bodyB.transform.pos - result.bodyA.transform.pos;

                bool  useFullRange  = radar.target.entity == result.entityB || radar.target == Entity.Null;
                float radarDistance = math.select(radar.nearestEnemyCrossHairsDistanceFilter, radar.distance, useFullRange);
                float radarCosFov   = math.select(radar.nearestEnemyCrossHairsCosFovFilter, radar.cosFov, useFullRange);

                bool isInRange = math.lengthsq(radarToShip) < radarDistance * radarDistance;
                bool isInView  = math.dot(math.normalize(radarToShip), math.forward(math.mul(result.bodyA.transform.rot, radar.crossHairsForwardDirectionBias))) > radarCosFov;

                if (isInRange && isInView)
                {
                    lineOfSightBodies.Add(new ColliderBody
                    {
                        collider  = new CapsuleCollider(0f, radarToShip, 0f),
                        entity    = result.entityA,
                        transform = new RigidTransform(quaternion.identity, result.bodyA.transform.pos)
                    });
                    scannedEnemies.Add(new ScannedEnemy
                    {
                        enemy          = result.entityB,
                        enemyTransform = result.bodyB.transform
                    });
                }
            }
        }

        [BurstCompile]
        private struct UpdateFriendsJob : IJob
        {
            public ComponentDataFromEntity<AiShipRadarScanResults> radarScanResultsCdfe;
            [ReadOnly] public NativeArray<ColliderBody>            lineOfSightBodies;
            [ReadOnly] public NativeArray<bool>                    occluded;

            public void Execute()
            {
                for (int i = 0; i < lineOfSightBodies.Length; i++)
                {
                    if (!occluded[i])
                    {
                        var entity                   = lineOfSightBodies[i].entity;
                        var results                  = radarScanResultsCdfe[entity];
                        results.friendFound          = true;
                        radarScanResultsCdfe[entity] = results;
                    }
                }
            }
        }

        [BurstCompile]
        private struct UpdateEnemiesJob : IJob
        {
            public ComponentDataFromEntity<AiShipRadarScanResults>  radarScanResultsCdfe;
            [ReadOnly] public ComponentDataFromEntity<AiShipRadar>  radarCdfe;
            [ReadOnly] public ComponentDataFromEntity<LocalToWorld> ltwCdfe;  //Todo: Optimize out?
            [ReadOnly] public NativeArray<ColliderBody>             lineOfSightBodies;
            [ReadOnly] public NativeArray<bool>                     occluded;
            [ReadOnly] public NativeArray<ScannedEnemy>             scannedEnemies;

            public void Execute()
            {
                for (int i = 0; i < lineOfSightBodies.Length; i++)
                {
                    if (!occluded[i])
                    {
                        var radarEntity  = lineOfSightBodies[i].entity;
                        var results      = radarScanResultsCdfe[radarEntity];
                        var radar        = radarCdfe[radarEntity];
                        var scannedEnemy = scannedEnemies[i];

                        if (radar.target == Entity.Null)
                        {
                            if (results.target == Entity.Null)
                            {
                                results.target          = scannedEnemy.enemy;
                                results.targetTransform = scannedEnemy.enemyTransform;
                            }
                            else
                            {
                                var radarLtw        = ltwCdfe[radarEntity];
                                var optimalPosition =
                                    math.forward(math.mul(quaternion.LookRotationSafe(radarLtw.Forward, radarLtw.Up),
                                                          radar.crossHairsForwardDirectionBias)) * radar.preferredTargetDistance + radarLtw.Position;
                                if (math.distancesq(results.targetTransform.pos, optimalPosition) > math.distancesq(scannedEnemy.enemyTransform.pos, optimalPosition))
                                {
                                    results.target          = scannedEnemy.enemy;
                                    results.targetTransform = scannedEnemy.enemyTransform;
                                }
                            }
                        }
                        else
                        {
                            //Todo: Because no filter is applied to the search, we would have to apply the filter here if target was null.
                            //I'm too lazy to do that right now, so I'm just not populating the nearestResult if there's no target.
                            if (radar.target == scannedEnemy.enemy)
                            {
                                results.target          = scannedEnemy.enemy;
                                results.targetTransform = scannedEnemy.enemyTransform;
                            }

                            if (results.nearestEnemy == Entity.Null)
                            {
                                var radarLtw = ltwCdfe[radarEntity];
                                if (math.dot(math.normalize(scannedEnemy.enemyTransform.pos - radarLtw.Position),
                                             math.forward(math.mul(quaternion.LookRotationSafe(radarLtw.Forward, radarLtw.Up),
                                                                   radar.crossHairsForwardDirectionBias))) > radar.nearestEnemyCrossHairsCosFovFilter)
                                {
                                    results.nearestEnemy          = scannedEnemy.enemy;
                                    results.nearestEnemyTransform = scannedEnemy.enemyTransform;
                                }
                            }
                            else
                            {
                                var radarPosition = lineOfSightBodies[i].transform.pos;
                                if (math.distancesq(results.nearestEnemyTransform.pos, radarPosition) > math.distancesq(scannedEnemy.enemyTransform.pos, radarPosition))
                                {
                                    results.nearestEnemy          = scannedEnemy.enemy;
                                    results.nearestEnemyTransform = scannedEnemy.enemyTransform;
                                }
                            }
                        }

                        radarScanResultsCdfe[radarEntity] = results;
                    }
                }
            }
        }
    }
}

