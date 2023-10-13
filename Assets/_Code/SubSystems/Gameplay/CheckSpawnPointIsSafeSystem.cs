using Latios;
using Latios.Psyshock;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.Profiling;

using static Unity.Entities.SystemAPI;

//The IFindPairsProcessors only force safeToSpawn from true to false.
//Because of this, it is safe to use the Unsafe parallel schedulers.
//However, if the logic is ever modified, this decision needs to be re-evaluated.

namespace Lsss
{
    [RequireMatchingQueriesForUpdate]
    [BurstCompile]
    public partial struct CheckSpawnPointIsSafeSystem : ISystem
    {
        LatiosWorldUnmanaged latiosWorld;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            latiosWorld = state.GetLatiosWorldUnmanaged();
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            new SpawnPointResetFlagsJob().ScheduleParallel();

            var processor = new SpawnPointIsNotSafeProcessor
            {
                safeToSpawnLookup = GetComponentLookup<SafeToSpawn>()
            };

            var closeProcessor = new SpawnPointsAreTooCloseProcessor
            {
                safeToSpawnLookup = processor.safeToSpawnLookup
            };

            var spawnLayer   = latiosWorld.sceneBlackboardEntity.GetCollectionComponent<SpawnPointCollisionLayer>(true).layer;
            state.Dependency = Physics.FindPairs(spawnLayer, closeProcessor).ScheduleParallelUnsafe(state.Dependency);

            var wallLayer    = latiosWorld.sceneBlackboardEntity.GetCollectionComponent<WallCollisionLayer>(true).layer;
            state.Dependency = Physics.FindPairs(spawnLayer, wallLayer, processor).ScheduleParallelUnsafe(state.Dependency);

            var bulletLayer  = latiosWorld.sceneBlackboardEntity.GetCollectionComponent<BulletCollisionLayer>(true).layer;
            state.Dependency = Physics.FindPairs(spawnLayer, bulletLayer, processor).ScheduleParallelUnsafe(state.Dependency);

            var explosionLayer = latiosWorld.sceneBlackboardEntity.GetCollectionComponent<ExplosionCollisionLayer>(true).layer;
            state.Dependency   = Physics.FindPairs(spawnLayer, explosionLayer, processor).ScheduleParallelUnsafe(state.Dependency);

            var wormholeLayer = latiosWorld.sceneBlackboardEntity.GetCollectionComponent<WormholeCollisionLayer>(true).layer;
            state.Dependency  = Physics.FindPairs(spawnLayer, wormholeLayer, processor).ScheduleParallelUnsafe(state.Dependency);

            var factionEntities = QueryBuilder().WithAll<Faction, FactionTag>().Build().ToEntityArray(Allocator.Temp);
            foreach (var entity in factionEntities)
            {
                var shipLayer    = latiosWorld.GetCollectionComponent<FactionShipsCollisionLayer>(entity, true).layer;
                state.Dependency = Physics.FindPairs(spawnLayer, shipLayer, processor).ScheduleParallelUnsafe(state.Dependency);
            }
        }

        [BurstCompile]
        partial struct SpawnPointResetFlagsJob : IJobEntity
        {
            public void Execute(ref SafeToSpawn safeToSpawn) => safeToSpawn.safe = true;
        }

        //Assumes A is SpawnPoint
        struct SpawnPointIsNotSafeProcessor : IFindPairsProcessor
        {
            public PhysicsComponentLookup<SafeToSpawn> safeToSpawnLookup;

            public void Execute(in FindPairsResult result)
            {
                // No need to check narrow phase. AABB check is good enough
                safeToSpawnLookup[result.entityA] = new SafeToSpawn { safe = false };
            }
        }

        struct SpawnPointsAreTooCloseProcessor : IFindPairsProcessor
        {
            public PhysicsComponentLookup<SafeToSpawn> safeToSpawnLookup;

            public void Execute(in FindPairsResult result)
            {
                safeToSpawnLookup[result.entityA] = new SafeToSpawn { safe = false };
                safeToSpawnLookup[result.entityB]                          = new SafeToSpawn { safe = false };
            }
        }
    }

    [BurstCompile]
    public partial struct CheckSpawnPointIsSafeSystem2 : ISystem
    {
        LatiosWorldUnmanaged latiosWorld;

        EntityQuery m_spawnerQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            latiosWorld = state.GetLatiosWorldUnmanaged();

            m_spawnerQuery = state.Fluent().With<SafeToSpawn>().With<SpawnTimes>(true).PatchQueryForBuildingCollisionLayer().Build();
            state.RequireForUpdate(m_spawnerQuery);
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var spawnLayer = latiosWorld.sceneBlackboardEntity.GetCollectionComponent<SpawnPointCollisionLayer>(true).layer;

            var hits         = CollectionHelper.CreateNativeArray<bool>(spawnLayer.count, state.WorldUpdateAllocator, NativeArrayOptions.UninitializedMemory);
            state.Dependency = new InitHitsArrayJob
            {
                hitArray   = hits,
                spawnLayer = spawnLayer,
                lookup     = GetComponentLookup<SpawnTimes>(true)
            }.ScheduleParallel(hits.Length, 32, state.Dependency);

            //state.Dependency = new LogInitialCandidatesJob { hitArray = hits }.Schedule(state.Dependency);

            var closeProcessor = new SpawnPointsAreTooCloseProcessor { hitArray = hits };
            state.Dependency                                                    = Physics.FindPairs(spawnLayer, closeProcessor).ScheduleParallelUnsafe(state.Dependency);

            //state.Dependency = new LogPostSelfCandidatesJob { hitArray = hits }.Schedule(state.Dependency);

            var queryJob = new SpawnPointIsNotSafeQueryJob
            {
                hitArray        = hits,
                spawnPointLayer = spawnLayer
            };

            queryJob.otherLayer = latiosWorld.sceneBlackboardEntity.GetCollectionComponent<WallCollisionLayer>(true).layer;
            state.Dependency    = queryJob.ScheduleParallelByRef(hits.Length, 1, state.Dependency);

            queryJob.otherLayer = latiosWorld.sceneBlackboardEntity.GetCollectionComponent<ExplosionCollisionLayer>(true).layer;
            state.Dependency    = queryJob.ScheduleParallelByRef(hits.Length, 1, state.Dependency);

            queryJob.otherLayer = latiosWorld.sceneBlackboardEntity.GetCollectionComponent<WormholeCollisionLayer>(true).layer;
            state.Dependency    = queryJob.ScheduleParallelByRef(hits.Length, 1, state.Dependency);

            var factionEntities = QueryBuilder().WithAll<Faction, FactionTag>().Build().ToEntityArray(Allocator.Temp);
            foreach (var entity in factionEntities)
            {
                queryJob.otherLayer = latiosWorld.GetCollectionComponent<FactionShipsCollisionLayer>(entity, true).layer;
                state.Dependency    = queryJob.ScheduleParallelByRef(hits.Length, 1, state.Dependency);
            }

            //state.Dependency = new LogPreBulletsCandidatesJob { hitArray = hits }.Schedule(state.Dependency);

            queryJob.otherLayer = latiosWorld.sceneBlackboardEntity.GetCollectionComponent<BulletCollisionLayer>(true).layer;
            state.Dependency    = queryJob.ScheduleParallelByRef(hits.Length, 1, state.Dependency);

            //state.Dependency = new LogFinalCandidatesJob { hitArray = hits }.Schedule(state.Dependency);

            state.Dependency = new WriteSpawnPointStatusesJob
            {
                hitArray        = hits,
                spawnPointLayer = spawnLayer,
                lookup          = GetComponentLookup<SafeToSpawn>(false)
            }.ScheduleParallel(hits.Length, 32, state.Dependency);
        }

        [BurstCompile]
        partial struct InitHitsArrayJob : IJobFor
        {
            [ReadOnly] public ComponentLookup<SpawnTimes> lookup;
            [ReadOnly] public CollisionLayer              spawnLayer;
            public NativeArray<bool>                      hitArray;
            public void Execute(int index) => hitArray[index] = lookup[spawnLayer.colliderBodies[index].entity].pauseTime > 0f;
        }

        struct SpawnPointsAreTooCloseProcessor : IFindPairsProcessor
        {
            [NativeDisableParallelForRestriction] public NativeArray<bool> hitArray;

            public void Execute(in FindPairsResult result)
            {
                hitArray[result.bodyIndexA] = true;
                hitArray[result.bodyIndexB] = true;
            }
        }

        [BurstCompile]
        struct SpawnPointIsNotSafeQueryJob : IJobFor
        {
            struct TestAnyInLayerProcessor : IFindObjectsProcessor
            {
                public bool hit;

                public void Execute(in FindObjectsResult result)
                {
                    hit = true;
                }
            }

            public NativeArray<bool>         hitArray;
            [ReadOnly] public CollisionLayer spawnPointLayer;
            [ReadOnly] public CollisionLayer otherLayer;

            public void Execute(int index)
            {
                if (hitArray[index])
                    return;

                var aabb        = spawnPointLayer.GetAabb(index);
                hitArray[index] = Physics.FindObjects(in aabb, in otherLayer, new TestAnyInLayerProcessor()).RunImmediate().hit;
            }
        }

        [BurstCompile]
        struct WriteSpawnPointStatusesJob : IJobFor
        {
            [ReadOnly] public NativeArray<bool>                                       hitArray;
            [ReadOnly] public CollisionLayer                                          spawnPointLayer;
            [NativeDisableParallelForRestriction] public ComponentLookup<SafeToSpawn> lookup;

            public void Execute(int index)
            {
                var entity                           = spawnPointLayer.colliderBodies[index].entity;
                var hit                              = hitArray[index];
                lookup.GetRefRW(entity).ValueRW.safe = !hit;
            }
        }

        [BurstCompile]
        struct LogInitialCandidatesJob : IJob
        {
            [ReadOnly] public NativeArray<bool> hitArray;

            public void Execute()
            {
                int count = 0;
                foreach (var hit in hitArray)
                {
                    if (hit)
                        count++;
                }
                UnityEngine.Debug.Log($"Initial hits: {count} / {hitArray.Length}");
            }
        }

        [BurstCompile]
        struct LogPostSelfCandidatesJob : IJob
        {
            [ReadOnly] public NativeArray<bool> hitArray;

            public void Execute()
            {
                int count = 0;
                foreach (var hit in hitArray)
                {
                    if (hit)
                        count++;
                }
                UnityEngine.Debug.Log($"PostSelf hits: {count} / {hitArray.Length}");
            }
        }

        [BurstCompile]
        struct LogPreBulletsCandidatesJob : IJob
        {
            [ReadOnly] public NativeArray<bool> hitArray;

            public void Execute()
            {
                int count = 0;
                foreach (var hit in hitArray)
                {
                    if (hit)
                        count++;
                }
                UnityEngine.Debug.Log($"PreBullets hits: {count} / {hitArray.Length}");
            }
        }

        [BurstCompile]
        struct LogFinalCandidatesJob : IJob
        {
            [ReadOnly] public NativeArray<bool> hitArray;

            public void Execute()
            {
                int count = 0;
                foreach (var hit in hitArray)
                {
                    if (hit)
                        count++;
                }
                UnityEngine.Debug.Log($"Final hits: {count} / {hitArray.Length}");
            }
        }
    }
}

