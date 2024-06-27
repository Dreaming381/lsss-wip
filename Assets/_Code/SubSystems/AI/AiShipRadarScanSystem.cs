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
        private NativeList<NativeList<AiShipRadarScanResults> > m_scanResultsArrayListCache;

        private EntityQuery                      m_radarsQuery;
        private DynamicSharedComponentTypeHandle m_factionMemberHandle;

        private LatiosWorldUnmanaged latiosWorld;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            latiosWorld = state.GetLatiosWorldUnmanaged();

            m_scanResultsArrayListCache = new NativeList<NativeList<AiShipRadarScanResults> >(Allocator.Persistent);
            m_factionMemberHandle       = state.GetDynamicSharedComponentTypeHandle(ComponentType.ReadOnly<FactionMember>());

            m_radarsQuery =
                QueryBuilder().WithAllRW<AiShipRadarScanResults>().WithAll<AiRadarTag, AiShipRadar, WorldTransform, FactionMember, AiShipRadarNeedsFullScanFlag>().Build();
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            m_scanResultsArrayListCache.Dispose();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            CollisionLayerSettings settings;
            if (latiosWorld.sceneBlackboardEntity.HasComponent<ArenaCollisionSettings>())
                settings = latiosWorld.sceneBlackboardEntity.GetComponentData<ArenaCollisionSettings>().settings;
            else
                settings               = BuildCollisionLayerConfig.defaultSettings;
            var collisionLayerSettings = settings;
            var wallLayer              = latiosWorld.sceneBlackboardEntity.GetCollectionComponent<WallCollisionLayer>(true).layer;
            var shipLayer              = latiosWorld.sceneBlackboardEntity.GetCollectionComponent<ShipsCollisionLayer>(true).layer;

            state.Dependency = new EvaluateScanRequestsJob
            {
                wallLayer            = wallLayer,
                worldTransformLookup = GetComponentLookup<WorldTransform>(true)
            }.ScheduleParallel(state.Dependency);

            var allocator = state.WorldUpdateAllocator;

            var factionEntities = QueryBuilder().WithAll<Faction>().Build().ToEntityArray(Allocator.Temp);
            m_factionMemberHandle.Update(ref state);

            var scanProcessor = new ScanProcessor
            {
                radarLookup             = GetComponentLookup<AiShipRadar>(true),
                wallLayer               = wallLayer,
                entityStorageInfoLookup = GetEntityStorageInfoLookup(),
                factionMemberHandle     = m_factionMemberHandle
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
                                                                                   rootDependency);

                var scanResultsList = new NativeList<AiShipRadarScanResults>(allocator);
                m_scanResultsArrayListCache.Add(scanResultsList);
                scanProcessor.scanResultsArray = scanResultsList.AsDeferredJobArray();

                jh = new InitializeScanResultsJob { collisionLayer = radarLayer, scanResultsList = scanResultsList }.Schedule(jh);

                jh = Physics.FindPairs(radarLayer, shipLayer, scanProcessor).ScheduleParallelByA(jh);

                jhs[i] = jh;
            }

            state.Dependency = JobHandle.CombineDependencies(jhs);

            for (int i = 0; i < factionEntities.Length; i++)
            {
                var array   = m_scanResultsArrayListCache[i];
                var faction = new FactionMember { factionEntity = factionEntities[i] };

                m_radarsQuery.SetSharedComponentFilter(faction);
                state.Dependency = new CopyBackJob
                {
                    array = array.AsDeferredJobArray(),
                }.ScheduleParallel(m_radarsQuery, state.Dependency);
            }

            m_radarsQuery.ResetFilter();
        }

        [BurstCompile]
        [WithOptions(EntityQueryOptions.IgnoreComponentEnabledState)]
        [WithAll(typeof(AiRadarTag))]
        partial struct EvaluateScanRequestsJob : IJobEntity
        {
            [ReadOnly] public CollisionLayer                  wallLayer;
            [ReadOnly] public ComponentLookup<WorldTransform> worldTransformLookup;

            public void Execute(EnabledRefRW<AiShipRadarNeedsFullScanFlag> flag,
                                ref AiShipRadarScanResults results,
                                in WorldTransform transform,
                                in AiShipRadar radar,
                                in AiShipRadarRequests requests)
            {
                if (requests.requestFriendAndNearestEnemy)
                {
                    flag.ValueRW = true;
                    return;
                }

                if (radar.target != Entity.Null)
                {
                    if (!worldTransformLookup.TryGetComponent(radar.target, out var targetTransform))
                    {
                        flag.ValueRW = true;
                        return;
                    }
                    var  radarToTarget  = targetTransform.position - transform.position;
                    bool outOfView      = math.lengthsq(radarToTarget) >= radar.distance * radar.distance;
                    outOfView          |=
                        math.dot(math.normalize(radarToTarget), math.forward(math.mul(transform.rotation, radar.crossHairsForwardDirectionBias))) <= radar.cosFov;

                    if (outOfView || Physics.RaycastAny(transform.position, targetTransform.position, in wallLayer, out _, out _))
                    {
                        flag.ValueRW = true;
                    }
                    else
                    {
                        flag.ValueRW            = false;
                        results.targetTransform = new RigidTransform(targetTransform.rotation, targetTransform.position);
                    }
                }
                else
                {
                    flag.ValueRW = true;
                }
            }
        }

        [BurstCompile]
        struct InitializeScanResultsJob : IJob
        {
            [ReadOnly] public CollisionLayer          collisionLayer;
            public NativeList<AiShipRadarScanResults> scanResultsList;

            public void Execute()
            {
                scanResultsList.Resize(collisionLayer.count, NativeArrayOptions.ClearMemory);
            }
        }

        [BurstCompile]
        partial struct CopyBackJob : IJobEntity
        {
            [ReadOnly] public NativeArray<AiShipRadarScanResults> array;

            public void Execute([EntityIndexInQuery] int entityIndexInQuery, ref AiShipRadarScanResults dst)
            {
                dst = array[entityIndexInQuery];
            }
        }

        private JobHandle BuildRadarLayer(ref SystemState state,
                                          FactionMember factionMember,
                                          CollisionLayerSettings settings,
                                          Allocator allocator,
                                          out CollisionLayer layer,
                                          JobHandle inputDeps)
        {
            m_radarsQuery.SetSharedComponentFilter(factionMember);
            var entities = m_radarsQuery.ToEntityListAsync(allocator, inputDeps, out var jh);
            var bodies   = new NativeList<ColliderBody>(allocator);
            var aabbs    = new NativeList<Aabb>(allocator);
            jh           = new ResizeListsJob { entities = entities, bodies = bodies, aabbs = aabbs }.Schedule(jh);
            jh           = new BuildRadarBodiesJob
            {
                entities = entities.AsDeferredJobArray(),
                bodies   = bodies.AsDeferredJobArray(),
                aabbs    = aabbs.AsDeferredJobArray()
            }.ScheduleParallel(m_radarsQuery, jh);
            jh = Physics.BuildCollisionLayer(bodies, aabbs).WithSettings(settings).ScheduleParallel(out layer, allocator, jh);
            return jh;
        }

        [BurstCompile]
        struct ResizeListsJob : IJob
        {
            [ReadOnly] public NativeList<Entity> entities;
            public NativeList<ColliderBody>      bodies;
            public NativeList<Aabb>              aabbs;

            public void Execute()
            {
                bodies.ResizeUninitialized(entities.Length);
                aabbs.ResizeUninitialized(entities.Length);
            }
        }

        [BurstCompile]
        partial struct BuildRadarBodiesJob : IJobEntity
        {
            [ReadOnly] public NativeArray<Entity> entities;
            public NativeArray<ColliderBody>      bodies;
            public NativeArray<Aabb>              aabbs;

            public void Execute([EntityIndexInQuery] int entityInQueryIndex, in AiShipRadar radar, in WorldTransform worldTransform)
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
                    entity    = entities[entityInQueryIndex],
                    transform = worldTransform.worldTransform
                };
            }
        }

        // Assumes A is radar, and B is other ship. Safe to schedule by A.
        struct ScanProcessor : IFindPairsProcessor
        {
            [ReadOnly] public CollisionLayer                                                 wallLayer;
            [ReadOnly] public ComponentLookup<AiShipRadar>                                   radarLookup;
            [ReadOnly] public DynamicSharedComponentTypeHandle                               factionMemberHandle;
            [ReadOnly] public EntityStorageInfoLookup                                        entityStorageInfoLookup;
            [NativeDisableParallelForRestriction] public NativeArray<AiShipRadarScanResults> scanResultsArray;

            int radarFactionIndex;

            public bool BeginBucket(in FindPairsBucketContext context)
            {
                if (context.bucketCountA == 0 || context.bucketCountB == 0)
                    return false;
                var radar         = context.layerA.colliderBodies[context.bucketStartA].entity;
                radarFactionIndex = entityStorageInfoLookup[radar].Chunk.GetSharedComponentIndex(ref factionMemberHandle);
                return true;
            }

            public void Execute(in FindPairsResult result)
            {
                var factionIndexB = entityStorageInfoLookup[result.entityB].Chunk.GetSharedComponentIndex(ref factionMemberHandle);
                if (radarFactionIndex == factionIndexB)
                    ExecuteFriend(in result);
                else
                    ExecuteEnemy(in result);
            }

            public void ExecuteFriend(in FindPairsResult result)
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

            public void ExecuteEnemy(in FindPairsResult result)
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

    [BurstCompile]
    public partial struct AiShipRadarScanSystem2 : ISystem
    {
        private EntityQuery m_radarsQuery;

        private LatiosWorldUnmanaged latiosWorld;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            latiosWorld = state.GetLatiosWorldUnmanaged();

            m_radarsQuery = QueryBuilder().WithAllRW<AiShipRadarScanResults>().WithAll<AiRadarTag, AiShipRadar, WorldTransform, FactionMember>().Build();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var collisionLayerSettings = latiosWorld.sceneBlackboardEntity.GetComponentData<ArenaCollisionSettings>().settings;

            var shipLayer = latiosWorld.sceneBlackboardEntity.GetCollectionComponent<ShipsCollisionLayer>(true).layer;
            var wallLayer = latiosWorld.sceneBlackboardEntity.GetCollectionComponent<WallCollisionLayer>(true).layer;

            var allocator = state.WorldUpdateAllocator;

            var count        = m_radarsQuery.CalculateEntityCountWithoutFiltering();
            var bodies       = CollectionHelper.CreateNativeArray<ColliderBody>(count, allocator, NativeArrayOptions.UninitializedMemory);
            var aabbs        = CollectionHelper.CreateNativeArray<Aabb>(count, allocator, NativeArrayOptions.UninitializedMemory);
            var scanResults  = CollectionHelper.CreateNativeArray<AiShipRadarScanResults>(count, allocator, NativeArrayOptions.UninitializedMemory);
            state.Dependency = new BuildRadarBodiesJob { bodies = bodies, aabbs = aabbs, scanResults = scanResults }.ScheduleParallel(m_radarsQuery, state.Dependency);
            state.Dependency = Physics.BuildCollisionLayer(bodies, aabbs).WithSettings(collisionLayerSettings).ScheduleParallel(out var radarLayer, allocator, state.Dependency);

            var scanProcessor = new ScanProcessor
            {
                entityStorageInfoLookup = GetEntityStorageInfoLookup(),
                factionMemberHandle     = GetSharedComponentTypeHandle<FactionMember>(),
                radarLookup             = GetComponentLookup<AiShipRadar>(false),
                scanResultsArray        = scanResults,
                wallLayer               = wallLayer
            };

            state.Dependency = Physics.FindPairs(radarLayer, shipLayer, scanProcessor).WithCrossCache().ScheduleParallelByA(state.Dependency);

            var indices      = m_radarsQuery.CalculateBaseEntityIndexArrayAsync(allocator, state.Dependency, out var jh);
            state.Dependency = new CopyBackJob
            {
                array                         = scanResults,
                scanResultsHandle             = GetComponentTypeHandle<AiShipRadarScanResults>(false),
                indicesOfFirstEntitiesInChunk = indices
            }.ScheduleParallel(m_radarsQuery, jh);
        }

        [BurstCompile]
        partial struct BuildRadarBodiesJob : IJobEntity
        {
            public NativeArray<ColliderBody>           bodies;
            public NativeArray<Aabb>                   aabbs;
            public NativeArray<AiShipRadarScanResults> scanResults;

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
                scanResults = default;
            }
        }

        // Assumes A is radar, and B is other ship. Safe to schedule by A.
        struct ScanProcessor : IFindPairsProcessor
        {
            [ReadOnly] public CollisionLayer                                                 wallLayer;
            [ReadOnly] public ComponentLookup<AiShipRadar>                                   radarLookup;
            [ReadOnly] public SharedComponentTypeHandle<FactionMember>                       factionMemberHandle;
            [ReadOnly] public EntityStorageInfoLookup                                        entityStorageInfoLookup;
            [NativeDisableParallelForRestriction] public NativeArray<AiShipRadarScanResults> scanResultsArray;

            public void Execute(in FindPairsResult result)
            {
                var factionIndexA = entityStorageInfoLookup[result.entityA].Chunk.GetSharedComponentIndex(factionMemberHandle);
                var factionIndexB = entityStorageInfoLookup[result.entityB].Chunk.GetSharedComponentIndex(factionMemberHandle);
                if (factionIndexA == factionIndexB)
                    ExecuteFriend(in result);
                else
                    ExecuteEnemy(in result);
            }

            public void ExecuteFriend(in FindPairsResult result)
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

            public void ExecuteEnemy(in FindPairsResult result)
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
    }
}

