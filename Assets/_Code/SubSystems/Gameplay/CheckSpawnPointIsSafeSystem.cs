using Latios;
using Latios.Psyshock;
using Latios.Transforms;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.Profiling;

//The IFindPairsProcessors only force safeToSpawn from true to false.
//Because of this, it is safe to use the Unsafe parallel schedulers.
//However, if the logic is ever modified, this decision needs to be re-evaluated.

namespace Lsss
{
    [RequireMatchingQueriesForUpdate]
    [BurstCompile]
    public partial struct CheckSpawnPointIsSafeSystem : ISystem, ILatiosApi
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            this.OnCreateForLatios(ref state);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var api = this.GetApi(ref state);
            new SpawnPointResetFlagsJob().ScheduleParallel();

            var processor      = new SpawnPointIsNotSafeProcessor().Inject(api);
            var closeProcessor = new SpawnPointsAreTooCloseProcessor().Inject(api);

            var spawnLayer   = api.sceneBlackboardEntity.GetCollectionComponent<SpawnPointCollisionLayer>(true).layer;
            state.Dependency = Physics.FindPairs(spawnLayer, closeProcessor).ScheduleParallelUnsafe(state.Dependency);

            var wallLayer    = api.sceneBlackboardEntity.GetCollectionComponent<WallCollisionLayer>(true).layer;
            state.Dependency = Physics.FindPairs(spawnLayer, wallLayer, processor).ScheduleParallelUnsafe(state.Dependency);

            var bulletLayer  = api.sceneBlackboardEntity.GetCollectionComponent<BulletCollisionLayer>(true).layer;
            state.Dependency = Physics.FindPairs(spawnLayer, bulletLayer, processor).ScheduleParallelUnsafe(state.Dependency);

            var explosionLayer = api.sceneBlackboardEntity.GetCollectionComponent<ExplosionCollisionLayer>(true).layer;
            state.Dependency   = Physics.FindPairs(spawnLayer, explosionLayer, processor).ScheduleParallelUnsafe(state.Dependency);

            var wormholeLayer = api.sceneBlackboardEntity.GetCollectionComponent<WormholeCollisionLayer>(true).layer;
            state.Dependency  = Physics.FindPairs(spawnLayer, wormholeLayer, processor).ScheduleParallelUnsafe(state.Dependency);

            var shipLayer = api.sceneBlackboardEntity.GetCollectionComponent<ShipsCollisionLayer>(true).layer;

            state.Dependency = Physics.FindPairs(spawnLayer, shipLayer, processor).ScheduleParallelUnsafe(state.Dependency);
        }

        [BurstCompile]
        partial struct SpawnPointResetFlagsJob : IJobEntity
        {
            public void Execute(ref SafeToSpawn safeToSpawn) => safeToSpawn.safe = true;
        }

        //Assumes A is SpawnPoint
        partial struct SpawnPointIsNotSafeProcessor : IFindPairsProcessor, IInjectable
        {
            [NativeDisableParallelForRestriction, Inject] ComponentLookup<SafeToSpawn> safeToSpawnLookup;

            public void Execute(in FindPairsResult result)
            {
                // No need to check narrow phase. AABB check is good enough
                safeToSpawnLookup[result.entityA] = new SafeToSpawn { safe = false };
            }
        }

        partial struct SpawnPointsAreTooCloseProcessor : IFindPairsProcessor, IInjectable
        {
            [NativeDisableParallelForRestriction, Inject] ComponentLookup<SafeToSpawn> safeToSpawnLookup;

            public void Execute(in FindPairsResult result)
            {
                safeToSpawnLookup[result.entityA] = new SafeToSpawn { safe = false };
                safeToSpawnLookup[result.entityB]                          = new SafeToSpawn { safe = false };
            }
        }
    }

    [BurstCompile]
    public partial struct CheckSpawnPointIsSafeSystem2 : ISystem, ILatiosApi
    {
        EntityQuery m_spawnerQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            this.OnCreateForLatios(ref state);

            m_spawnerQuery = state.Fluent().With<SafeToSpawn>().With<SpawnTimes>(true).PatchQueryForBuildingCollisionLayer().Build();
            state.RequireForUpdate(m_spawnerQuery);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var api        = this.GetApi(ref state);
            var spawnLayer = api.sceneBlackboardEntity.GetCollectionComponent<SpawnPointCollisionLayer>(true).layer;

            var hits         = CollectionHelper.CreateNativeArray<bool>(spawnLayer.count, state.WorldUpdateAllocator, NativeArrayOptions.UninitializedMemory);
            state.Dependency = new InitHitsArrayJob
            {
                hitArray   = hits,
                spawnLayer = spawnLayer,
            }.Inject(api).ScheduleParallel(hits.Length, 32, state.Dependency);

            //state.Dependency = new LogInitialCandidatesJob { hitArray = hits }.Schedule(state.Dependency);

            var closeProcessor = new SpawnPointsAreTooCloseProcessor { hitArray = hits };
            state.Dependency                                                    = Physics.FindPairs(spawnLayer, closeProcessor).ScheduleParallelUnsafe(state.Dependency);

            //state.Dependency = new LogPostSelfCandidatesJob { hitArray = hits }.Schedule(state.Dependency);

            var queryJob = new SpawnPointIsNotSafeQueryJob
            {
                hitArray        = hits,
                spawnPointLayer = spawnLayer
            };

            queryJob.otherLayer = api.sceneBlackboardEntity.GetCollectionComponent<WallCollisionLayer>(true).layer;
            state.Dependency    = queryJob.ScheduleParallelByRef(hits.Length, 1, state.Dependency);

            queryJob.otherLayer = api.sceneBlackboardEntity.GetCollectionComponent<ExplosionCollisionLayer>(true).layer;
            state.Dependency    = queryJob.ScheduleParallelByRef(hits.Length, 1, state.Dependency);

            queryJob.otherLayer = api.sceneBlackboardEntity.GetCollectionComponent<WormholeCollisionLayer>(true).layer;
            state.Dependency    = queryJob.ScheduleParallelByRef(hits.Length, 1, state.Dependency);

            queryJob.otherLayer = api.sceneBlackboardEntity.GetCollectionComponent<ShipsCollisionLayer>(true).layer;
            state.Dependency    = queryJob.ScheduleParallelByRef(hits.Length, 1, state.Dependency);

            //state.Dependency = new LogPreBulletsCandidatesJob { hitArray = hits }.Schedule(state.Dependency);

            queryJob.otherLayer = api.sceneBlackboardEntity.GetCollectionComponent<BulletCollisionLayer>(true).layer;
            state.Dependency    = queryJob.ScheduleParallelByRef(hits.Length, 1, state.Dependency);

            //state.Dependency = new LogFinalCandidatesJob { hitArray = hits }.Schedule(state.Dependency);

            state.Dependency = new WriteSpawnPointStatusesJob
            {
                hitArray        = hits,
                spawnPointLayer = spawnLayer,
            }.Inject(api).ScheduleParallel(hits.Length, 32, state.Dependency);
        }

        [BurstCompile]
        partial struct InitHitsArrayJob : IJobFor, IInjectable
        {
            [ReadOnly, Inject] ComponentLookup<SpawnTimes> lookup;
            [ReadOnly] public CollisionLayer               spawnLayer;
            public NativeArray<bool>                       hitArray;
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
        partial struct WriteSpawnPointStatusesJob : IJobFor, IInjectable
        {
            [ReadOnly] public NativeArray<bool>                                               hitArray;
            [ReadOnly] public CollisionLayer                                                  spawnPointLayer;
            [NativeDisableParallelForRestriction, Inject] public ComponentLookup<SafeToSpawn> lookup;

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

