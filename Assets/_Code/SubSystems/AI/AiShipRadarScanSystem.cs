using System.Collections.Generic;
using Latios;
using Latios.Psyshock;
using Latios.Transforms;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.Profiling;

using static Unity.Entities.SystemAPI;

namespace Lsss
{
    [BurstCompile]
    public partial struct AiShipRadarScanSystem : ISystem
    {
        private NativeList<FactionShipsCollisionLayer>           m_factionsCache;
        private NativeList<NativeArray<AiShipRadarScanResults> > m_scanResultsArrayListCache;

        private EntityQuery m_radarsQuery;

        private ComponentTypeHandle<AiShipRadarScanResults> m_scanResultsHandle;

        private LatiosWorldUnmanaged latiosWorld;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            latiosWorld = state.GetLatiosWorldUnmanaged();

            m_factionsCache             = new NativeList<FactionShipsCollisionLayer>(Allocator.Persistent);
            m_scanResultsArrayListCache = new NativeList<NativeArray<AiShipRadarScanResults> >(Allocator.Persistent);

            m_scanResultsHandle = state.GetComponentTypeHandle<AiShipRadarScanResults>(false);

            m_radarsQuery = QueryBuilder().WithAllRW<AiShipRadarScanResults>().WithAll<AiRadarTag, AiShipRadar, WorldTransform, FactionMember>().Build();
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            m_factionsCache.Dispose();
            m_scanResultsArrayListCache.Dispose();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            m_scanResultsHandle.Update(ref state);

            CollisionLayerSettings settings;
            if (latiosWorld.sceneBlackboardEntity.HasComponent<ArenaCollisionSettings>())
                settings = latiosWorld.sceneBlackboardEntity.GetComponentData<ArenaCollisionSettings>().settings;
            else
                settings               = BuildCollisionLayerConfig.defaultSettings;
            var collisionLayerSettings = settings;
            var wallLayer              = latiosWorld.sceneBlackboardEntity.GetCollectionComponent<WallCollisionLayer>(true).layer;

            var allocator = state.WorldUnmanaged.UpdateAllocator.ToAllocator;

            m_factionsCache.Clear();

            var factionEntities = new NativeList<Entity>(allocator);
            foreach ((var unused, var entity) in Query<RefRO<Faction> >().WithEntityAccess().WithAll<FactionTag>())
            {
                m_factionsCache.Add(latiosWorld.GetCollectionComponent<FactionShipsCollisionLayer>(entity, true));
                factionEntities.Add(entity);
            }

            var scanFriendsProcessor = new ScanFriendsProcessor
            {
                radarLookup = GetComponentLookup<AiShipRadar>(true),
                wallLayer   = wallLayer
            };

            var scanEnemiesProcessor = new ScanEnemiesProcessor
            {
                radarLookup = scanFriendsProcessor.radarLookup,
                wallLayer   = wallLayer
            };

            var rootDependency = state.Dependency;

            var jhs = new NativeArray<JobHandle>(factionEntities.Length, Allocator.Temp);

            m_scanResultsArrayListCache.Clear();

            for (int i = 0; i < factionEntities.Length; i++)
            {
                var factionA = new FactionMember { factionEntity = factionEntities[i] };
                var jh                                           = BuildRadarLayer(ref state,
                                                                                   factionA,
                                                                                   collisionLayerSettings,
                                                                                   allocator,
                                                                                   out var radarLayer,
                                                                                   out int radarLayerCount,
                                                                                   rootDependency);

                var scanResultsArray = CollectionHelper.CreateNativeArray<AiShipRadarScanResults>(radarLayerCount, allocator, NativeArrayOptions.UninitializedMemory);
                m_scanResultsArrayListCache.Add(scanResultsArray);
                scanFriendsProcessor.scanResultsArray = scanResultsArray;
                scanEnemiesProcessor.scanResultsArray = scanResultsArray;

                jh = new ResizeScanResultsJob { scanResultsArray = scanResultsArray }.Schedule(jh);

                var shipLayerA = m_factionsCache[i].layer;

                var marker = new Unity.Profiling.ProfilerMarker("Dispatch FindPairs");
                marker.Begin();
                jh = Physics.FindPairs(radarLayer, shipLayerA, scanFriendsProcessor).WithCrossCache().ScheduleParallel(jh);

                for (int j = 0; j < factionEntities.Length; j++)
                {
                    if (i == j)
                        continue;

                    var shipLayerB = m_factionsCache[j].layer;

                    jh = Physics.FindPairs(radarLayer, shipLayerB, scanEnemiesProcessor).WithCrossCache().ScheduleParallel(jh);
                }
                marker.End();

                jhs[i] = jh;
            }

            state.Dependency = JobHandle.CombineDependencies(jhs);

            for (int i = 0; i < factionEntities.Length; i++)
            {
                var array   = m_scanResultsArrayListCache[i];
                var faction = new FactionMember { factionEntity = factionEntities[i] };

                m_radarsQuery.SetSharedComponentFilter(faction);
                var indices      = m_radarsQuery.CalculateBaseEntityIndexArrayAsync(allocator, state.Dependency, out var jh);
                state.Dependency = new CopyBackJob
                {
                    array                         = array,
                    scanResultsHandle             = m_scanResultsHandle,
                    indicesOfFirstEntitiesInChunk = indices
                }.ScheduleParallel(m_radarsQuery, jh);
            }

            m_radarsQuery.ResetFilter();
        }

        [BurstCompile]
        public struct ResizeScanResultsJob : IJob
        {
            public NativeArray<AiShipRadarScanResults> scanResultsArray;

            public unsafe void Execute()
            {
                var ptr = scanResultsArray.GetUnsafePtr();
                UnsafeUtility.MemClear(ptr, scanResultsArray.Length * UnsafeUtility.SizeOf<AiShipRadarScanResults>());
            }
        }

        [BurstCompile]
        public struct CopyBackJob : IJobChunk
        {
            [ReadOnly] public NativeArray<AiShipRadarScanResults>         array;
            public ComponentTypeHandle<AiShipRadarScanResults>            scanResultsHandle;
            [NativeDisableParallelForRestriction] public NativeArray<int> indicesOfFirstEntitiesInChunk;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                var dst = chunk.GetNativeArray(ref scanResultsHandle);
                var src = array.GetSubArray(indicesOfFirstEntitiesInChunk[unfilteredChunkIndex], chunk.Count);
                dst.CopyFrom(src);
            }
        }

        private JobHandle BuildRadarLayer(ref SystemState state,
                                          FactionMember factionMember,
                                          CollisionLayerSettings settings,
                                          Allocator allocator,
                                          out CollisionLayer layer,
                                          out int count,
                                          JobHandle inputDeps)
        {
            m_radarsQuery.SetSharedComponentFilter(factionMember);
            count      = CalculateEntityCountBurst(ref m_radarsQuery);
            var bodies = CollectionHelper.CreateNativeArray<ColliderBody>(count, allocator, NativeArrayOptions.UninitializedMemory);
            var aabbs  = CollectionHelper.CreateNativeArray<Aabb>(count, allocator, NativeArrayOptions.UninitializedMemory);
            var jh     = new BuildRadarBodiesJob { bodies = bodies, aabbs = aabbs }.ScheduleParallel(m_radarsQuery, inputDeps);
            jh         = Physics.BuildCollisionLayer(bodies, aabbs).WithSettings(settings).ScheduleParallel(out layer, allocator, jh);
            return jh;
        }

        [BurstCompile]
        static int CalculateEntityCountBurst(ref EntityQuery query) => query.CalculateEntityCount();

        [BurstCompile]
        partial struct BuildRadarBodiesJob : IJobEntity
        {
            public NativeArray<ColliderBody> bodies;
            public NativeArray<Aabb>         aabbs;

            public void Execute(Entity e, [EntityIndexInQuery] int entityInQueryIndex, in AiShipRadar radar, in WorldTransform worldTransform)
            {
                var sphere = new SphereCollider(0f, radar.distance);
                if (radar.cosFov < 0f)
                {
                    //Todo: Create tighter bounds here too.
                    aabbs[entityInQueryIndex] = Physics.AabbFrom(sphere, worldTransform.worldTransform);
                }
                else
                {
                    //Compute aabb of vertex and spherical cap points which are extreme points
                    float3 forward             = worldTransform.forwardDirection;
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
                    aabb.min                  = math.min(aabb.min, min) + worldTransform.position;
                    aabb.max                  = math.max(aabb.max, max) + worldTransform.position;
                    aabbs[entityInQueryIndex] = aabb;
                }

                bodies[entityInQueryIndex] = new ColliderBody
                {
                    collider  = sphere,
                    entity    = e,
                    transform = worldTransform.worldTransform
                };
            }
        }

        // Assumes A is radar, and B is friendly ship
        struct ScanFriendsProcessor : IFindPairsProcessor
        {
            [ReadOnly] public CollisionLayer                                                 wallLayer;
            [ReadOnly] public ComponentLookup<AiShipRadar>                                   radarLookup;
            [NativeDisableParallelForRestriction] public NativeArray<AiShipRadarScanResults> scanResultsArray;

            public void Execute(in FindPairsResult result)
            {
                var    radar       = radarLookup[result.entityA];
                float3 radarToShip = result.transformB.position - result.transformA.position;
                bool   isInRange   = math.lengthsq(radarToShip) < radar.friendCrossHairsDistanceFilter * radar.friendCrossHairsDistanceFilter;
                bool   isInView    =
                    math.dot(math.normalize(radarToShip),
                             math.forward(math.mul(result.transformA.rotation, radar.crossHairsForwardDirectionBias))) > radar.friendCrossHairsCosFovFilter;

                if (isInRange && isInView)
                {
                    var hitWall = Physics.RaycastAny(result.transformA.position, result.transformB.position, in wallLayer, out _, out _);
                    if (!hitWall)
                    {
                        var srcIndex               = result.sourceIndexA;
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
            [ReadOnly] public ComponentLookup<AiShipRadar>                                   radarLookup;
            [NativeDisableParallelForRestriction] public NativeArray<AiShipRadarScanResults> scanResultsArray;

            public void Execute(in FindPairsResult result)
            {
                var    radar       = radarLookup[result.entityA];
                float3 radarToShip = result.transformB.position - result.transformA.position;

                bool  useFullRange  = radar.target.entity == result.entityB || radar.target == Entity.Null;
                float radarDistance = math.select(radar.nearestEnemyCrossHairsDistanceFilter, radar.distance, useFullRange);
                float radarCosFov   = math.select(radar.nearestEnemyCrossHairsCosFovFilter, radar.cosFov, useFullRange);

                bool isInRange = math.lengthsq(radarToShip) < radarDistance * radarDistance;
                bool isInView  = math.dot(math.normalize(radarToShip), math.forward(math.mul(result.transformA.rotation, radar.crossHairsForwardDirectionBias))) > radarCosFov;

                if (isInRange && isInView)
                {
                    var hitWall = Physics.RaycastAny(result.transformA.position, result.transformB.position, in wallLayer, out _, out _);
                    if (!hitWall)
                    {
                        var srcIndex   = result.sourceIndexA;
                        var scanResult = scanResultsArray[srcIndex];

                        if (radar.target == Entity.Null)
                        {
                            if (scanResult.target == Entity.Null)
                            {
                                scanResult.target          = result.entityB;
                                scanResult.targetTransform = new RigidTransform(result.transformB.rotation, result.transformB.position);
                            }
                            else
                            {
                                var optimalPosition =
                                    math.forward(math.mul(result.transformA.rotation,
                                                          radar.crossHairsForwardDirectionBias)) * radar.preferredTargetDistance + result.transformA.position;
                                if (math.distancesq(scanResult.targetTransform.pos, optimalPosition) > math.distancesq(result.transformB.position, optimalPosition))
                                {
                                    scanResult.target          = result.entityB;
                                    scanResult.targetTransform = new RigidTransform(result.transformB.rotation, result.transformB.position);
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
                                scanResult.targetTransform = new RigidTransform(result.transformB.rotation, result.transformB.position);
                            }

                            if (scanResult.nearestEnemy == Entity.Null)
                            {
                                if (math.dot(math.normalize(result.transformB.position - result.transformA.position),
                                             math.forward(math.mul(result.transformA.rotation,
                                                                   radar.crossHairsForwardDirectionBias))) > radar.nearestEnemyCrossHairsCosFovFilter)
                                {
                                    scanResult.nearestEnemy          = result.entityB;
                                    scanResult.nearestEnemyTransform = new RigidTransform(result.transformB.rotation, result.transformB.position);
                                }
                            }
                            else
                            {
                                if (math.distancesq(scanResult.nearestEnemyTransform.pos,
                                                    result.transformA.position) > math.distancesq(result.transformB.position, result.transformA.position))
                                {
                                    scanResult.nearestEnemy          = result.entityB;
                                    scanResult.nearestEnemyTransform = new RigidTransform(result.transformB.rotation, result.transformB.position);
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

