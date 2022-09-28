using System.Collections.Generic;
using Latios;
using Latios.Psyshock;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine.Profiling;

namespace Lsss
{
    public partial class AiShipRadarScanSystem : SubSystem
    {
        private List<FactionShipsCollisionLayer>           m_factionsCache             = new List<FactionShipsCollisionLayer>();
        private List<NativeArray<AiShipRadarScanResults> > m_scanResultsArrayListCache = new List<NativeArray<AiShipRadarScanResults> >();

        private EntityQuery m_factionsQuery;
        private EntityQuery m_radarsQuery;

        protected override void OnUpdate()
        {
            CollisionLayerSettings settings;
            if (sceneBlackboardEntity.HasComponent<ArenaCollisionSettings>())
                settings = sceneBlackboardEntity.GetComponentData<ArenaCollisionSettings>().settings;
            else
                settings               = BuildCollisionLayerConfig.defaultSettings;
            var collisionLayerSettings = settings;
            var wallLayer              = sceneBlackboardEntity.GetCollectionComponent<WallCollisionLayer>(true).layer;

            var allocator = World.UpdateAllocator.ToAllocator;

            m_factionsCache.Clear();
            var backup = Dependency;
            Dependency = default;
            Entities.WithAll<FactionTag>().ForEach((Entity entity, int entityInQueryIndex) =>
            {
                if (entityInQueryIndex == 0)
                    Dependency = backup;

                m_factionsCache.Add(EntityManager.GetCollectionComponent<FactionShipsCollisionLayer>(entity, true));
            }).WithStoreEntityQueryInField(ref m_factionsQuery).WithoutBurst().Run();
            var factionEntities = m_factionsQuery.ToEntityArray(allocator);

            //Todo: IJobChunk with MemClear?
            //Entities.WithAll<AiRadarTag>().ForEach((ref AiShipRadarScanResults results) =>
            //{
            //    results = default;
            //}).WithName("ScanClear").ScheduleParallel();

            var scanFriendsProcessor = new ScanFriendsProcessor
            {
                radarCdfe = GetComponentLookup<AiShipRadar>(true),
                wallLayer = wallLayer
            };

            var scanEnemiesProcessor = new ScanEnemiesProcessor
            {
                radarCdfe = scanFriendsProcessor.radarCdfe,
                wallLayer = wallLayer
            };

            var rootDependency = Dependency;

            var jhs = new NativeArray<JobHandle>(factionEntities.Length, Allocator.Temp);

            m_scanResultsArrayListCache.Clear();

            for (int i = 0; i < factionEntities.Length; i++)
            {
                var factionA = new FactionMember { factionEntity = factionEntities[i] };
                var jh                                           = BuildRadarLayer(factionA,
                                                                                   collisionLayerSettings,
                                                                                   allocator,
                                                                                   out var radarLayer,
                                                                                   out var remapArray,
                                                                                   rootDependency);

                var scanResultsArray = CollectionHelper.CreateNativeArray<AiShipRadarScanResults>(radarLayer.Count, allocator, NativeArrayOptions.UninitializedMemory);
                m_scanResultsArrayListCache.Add(scanResultsArray);
                scanFriendsProcessor.scanResultsArray = scanResultsArray;
                scanEnemiesProcessor.scanResultsArray = scanResultsArray;
                scanFriendsProcessor.remapArray       = remapArray;
                scanEnemiesProcessor.remapArray       = remapArray;

                unsafe
                {
                    jh = Job.WithCode(() =>
                    {
                        var ptr = scanResultsArray.GetUnsafePtr();
                        UnsafeUtility.MemClear(ptr, scanResultsArray.Length * UnsafeUtility.SizeOf<AiShipRadarScanResults>());
                    }).Schedule(jh);
                }

                var shipLayerA = m_factionsCache[i].layer;

                jh = Physics.FindPairs(radarLayer, shipLayerA, scanFriendsProcessor).WithCrossCache().ScheduleParallel(jh);

                for (int j = 0; j < factionEntities.Length; j++)
                {
                    if (i == j)
                        continue;

                    var shipLayerB = m_factionsCache[j].layer;

                    jh = Physics.FindPairs(radarLayer, shipLayerB, scanEnemiesProcessor).WithCrossCache().ScheduleParallel(jh);
                }

                jhs[i] = jh;
            }

            Dependency = JobHandle.CombineDependencies(jhs);

            var scanResultsHandle = GetComponentTypeHandle<AiShipRadarScanResults>(false);

            for (int i = 0; i < factionEntities.Length; i++)
            {
                var array   = m_scanResultsArrayListCache[i];
                var faction = new FactionMember { factionEntity = factionEntities[i] };
                //Entities.WithAll<AiRadarTag>().WithSharedComponentFilter(faction).ForEach((int entityInQueryIndex, ref AiShipRadarScanResults result) =>
                //{
                //    result = array[entityInQueryIndex];
                //}).ScheduleParallel();
                m_radarsQuery.SetSharedComponentFilter(faction);
                Dependency = new CopyBackJob { array = array, scanResultsHandle = scanResultsHandle }.ScheduleParallel(m_radarsQuery, Dependency);
            }

            m_radarsQuery.ResetFilter();
        }

        [BurstCompile]
        public struct CopyBackJob : IJobEntityBatchWithIndex
        {
            [ReadOnly] public NativeArray<AiShipRadarScanResults> array;
            public ComponentTypeHandle<AiShipRadarScanResults>    scanResultsHandle;

            public void Execute(ArchetypeChunk batchInChunk, int batchIndex, int indexOfFirstEntityInQuery)
            {
                var dst = batchInChunk.GetNativeArray(scanResultsHandle);
                var src = array.GetSubArray(indexOfFirstEntityInQuery, batchInChunk.Count);
                dst.CopyFrom(src);
            }
        }

        //[BurstCompile]
        //public partial struct CopyBackJob : IJobEntity
        //{
        //    [ReadOnly] public NativeArray<AiShipRadarScanResults> array;
        //
        //    public void Execute(int entityInQueryIndex, ref AiShipRadarScanResults result)
        //    {
        //        result = array[entityInQueryIndex];
        //    }
        //}

        private JobHandle BuildRadarLayer(FactionMember factionMember,
                                          CollisionLayerSettings settings,
                                          Allocator allocator,
                                          out CollisionLayer layer,
                                          out NativeArray<int>   remapArray,
                                          JobHandle inputDeps)
        {
            m_radarsQuery.SetSharedComponentFilter(factionMember);
            int count  = m_radarsQuery.CalculateEntityCount();
            var bodies = CollectionHelper.CreateNativeArray<ColliderBody>(count, allocator);
            var aabbs  = CollectionHelper.CreateNativeArray<Aabb>(count, allocator);
            var jh     = Entities.WithAll<AiRadarTag>().WithSharedComponentFilter(factionMember).WithStoreEntityQueryInField(ref m_radarsQuery)
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
            jh = Physics.BuildCollisionLayer(bodies, aabbs).WithSettings(settings).WithRemapArray(out remapArray, allocator).ScheduleParallel(out layer, allocator, jh);
            return jh;
        }

        // Assumes A is radar, and B is friendly ship
        struct ScanFriendsProcessor : IFindPairsProcessor
        {
            [ReadOnly] public CollisionLayer                                                 wallLayer;
            [ReadOnly] public ComponentLookup<AiShipRadar>                                   radarCdfe;
            [NativeDisableParallelForRestriction] public NativeArray<AiShipRadarScanResults> scanResultsArray;
            [ReadOnly] public NativeArray<int>                                               remapArray;

            public void Execute(in FindPairsResult result)
            {
                var    radar       = radarCdfe[result.entityA];
                float3 radarToShip = result.bodyB.transform.pos - result.bodyA.transform.pos;
                bool   isInRange   = math.lengthsq(radarToShip) < radar.friendCrossHairsDistanceFilter * radar.friendCrossHairsDistanceFilter;
                bool   isInView    =
                    math.dot(math.normalize(radarToShip),
                             math.forward(math.mul(result.bodyA.transform.rot, radar.crossHairsForwardDirectionBias))) > radar.friendCrossHairsCosFovFilter;

                if (isInRange && isInView)
                {
                    var hitWall = Physics.RaycastAny(result.bodyA.transform.pos, result.bodyB.transform.pos, in wallLayer, out _, out _);
                    if (!hitWall)
                    {
                        var srcIndex               = remapArray[result.indexA];
                        var scanResult             = scanResultsArray[srcIndex];
                        scanResult.friendFound     = true;
                        scanResultsArray[srcIndex] = scanResult;
                    }
                }
            }
        }

        // Assumes A is radar, and B is enemy ship
        struct ScanEnemiesProcessor : IFindPairsProcessor
        {
            [ReadOnly] public CollisionLayer                                                 wallLayer;
            [ReadOnly] public ComponentLookup<AiShipRadar>                                   radarCdfe;
            [NativeDisableParallelForRestriction] public NativeArray<AiShipRadarScanResults> scanResultsArray;
            [ReadOnly] public NativeArray<int>                                               remapArray;

            public void Execute(in FindPairsResult result)
            {
                var    radar       = radarCdfe[result.entityA];
                float3 radarToShip = result.bodyB.transform.pos - result.bodyA.transform.pos;

                bool  useFullRange  = radar.target.entity == result.entityB || radar.target == Entity.Null;
                float radarDistance = math.select(radar.nearestEnemyCrossHairsDistanceFilter, radar.distance, useFullRange);
                float radarCosFov   = math.select(radar.nearestEnemyCrossHairsCosFovFilter, radar.cosFov, useFullRange);

                bool isInRange = math.lengthsq(radarToShip) < radarDistance * radarDistance;
                bool isInView  = math.dot(math.normalize(radarToShip), math.forward(math.mul(result.bodyA.transform.rot, radar.crossHairsForwardDirectionBias))) > radarCosFov;

                if (isInRange && isInView)
                {
                    var hitWall = Physics.RaycastAny(result.bodyA.transform.pos, result.bodyB.transform.pos, in wallLayer, out _, out _);
                    if (!hitWall)
                    {
                        var srcIndex   = remapArray[result.indexA];
                        var scanResult = scanResultsArray[srcIndex];

                        if (radar.target == Entity.Null)
                        {
                            if (scanResult.target == Entity.Null)
                            {
                                scanResult.target          = result.entityB;
                                scanResult.targetTransform = result.transformB;
                            }
                            else
                            {
                                var optimalPosition =
                                    math.forward(math.mul(result.transformA.rot,
                                                          radar.crossHairsForwardDirectionBias)) * radar.preferredTargetDistance + result.transformA.pos;
                                if (math.distancesq(scanResult.targetTransform.pos, optimalPosition) > math.distancesq(result.transformB.pos, optimalPosition))
                                {
                                    scanResult.target          = result.entityB;
                                    scanResult.targetTransform = result.transformB;
                                }
                            }
                        }
                        else
                        {
                            //Todo: Because no filter is applied to the search, we would have to apply the filter here if target was null.
                            //I'm too lazy to do that right now, so I'm just not populating the nearestResult if there's no target.
                            if (radar.target.entity == result.entityB)
                            {
                                scanResult.target          = result.entityB;
                                scanResult.targetTransform = result.transformB;
                            }

                            if (scanResult.nearestEnemy == Entity.Null)
                            {
                                if (math.dot(math.normalize(result.transformB.pos - result.transformA.pos),
                                             math.forward(math.mul(result.transformA.rot,
                                                                   radar.crossHairsForwardDirectionBias))) > radar.nearestEnemyCrossHairsCosFovFilter)
                                {
                                    scanResult.nearestEnemy          = result.entityB;
                                    scanResult.nearestEnemyTransform = result.transformB;
                                }
                            }
                            else
                            {
                                if (math.distancesq(scanResult.nearestEnemyTransform.pos, result.transformA.pos) > math.distancesq(result.transformB.pos, result.transformA.pos))
                                {
                                    scanResult.nearestEnemy          = result.entityB;
                                    scanResult.nearestEnemyTransform = result.transformB;
                                }
                            }
                        }

                        scanResultsArray[srcIndex] = scanResult;
                    }
                }
            }
        }
    }
}

