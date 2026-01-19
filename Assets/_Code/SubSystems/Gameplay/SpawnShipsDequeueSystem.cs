using Latios;
using Latios.Transforms;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

using static Unity.Entities.SystemAPI;

namespace Lsss
{
    [RequireMatchingQueriesForUpdate]
    [BurstCompile]
    public partial struct SpawnShipsDequeueSystem : ISystem, ISystemNewScene
    {
        struct NextSpawnCounter : IComponentData
        {
            public int    index;
            public Random random;
        }

        LatiosWorldUnmanaged latiosWorld;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            latiosWorld = state.GetLatiosWorldUnmanaged();
        }

        public void OnNewScene(ref SystemState state) => latiosWorld.sceneBlackboardEntity.AddComponentData(new NextSpawnCounter { index = 0, random = new Random(57108) });

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float dt               = Time.DeltaTime;
            new SpawnTimesJob { dt = dt }.Schedule();

            var    spawnQueues  = latiosWorld.sceneBlackboardEntity.GetCollectionComponent<SpawnQueues>();
            int    initialIndex = latiosWorld.sceneBlackboardEntity.GetComponentData<NextSpawnCounter>().index;
            Entity nscEntity    = latiosWorld.sceneBlackboardEntity;
            var    icb          = latiosWorld.syncPoint.CreateInstantiateCommandBuffer<ParentCommand>();

            var job = new SpawnDequeueJob
            {
                icb            = icb,
                initialIndex   = initialIndex,
                useBeforeIndex = true,
                nscEntity      = nscEntity,
                spawnQueues    = spawnQueues,
                nscLookup      = GetComponentLookup<NextSpawnCounter>()
            };
            job.Schedule();
            job.useBeforeIndex = false;
            job.Schedule();
        }

        [WithAll(typeof(SpawnPointTag))]
        [BurstCompile]
        partial struct SpawnTimesJob : IJobEntity
        {
            public float dt;

            public void Execute(ref SpawnTimes spawnTimes)
            {
                spawnTimes.enableTime -= dt;
                spawnTimes.pauseTime  -= dt;

                spawnTimes.enableTime = math.max(spawnTimes.enableTime, 0f);
                spawnTimes.pauseTime  = math.max(spawnTimes.pauseTime, 0f);
            }
        }

        [WithAll(typeof(SpawnPointTag), typeof(WorldTransform))]
        [BurstCompile]
        partial struct SpawnDequeueJob : IJobEntity, IJobEntityChunkBeginEnd
        {
            public int  initialIndex;
            public bool useBeforeIndex;

            public TransformAspectRootHandle                       transformHandle;
            public SpawnQueues                                     spawnQueues;
            public Entity                                          nscEntity;
            public InstantiateCommandBufferCommand1<ParentCommand> icb;
            public ComponentLookup<NextSpawnCounter>               nscLookup;

            public void Execute(Entity entity,
                                [EntityIndexInQuery] int indexInQuery,
                                [EntityIndexInChunk] int indexInChunk,
                                ref SpawnPayload payload,
                                ref SpawnTimes times,
                                in SpawnPoint spawnData,
                                in SafeToSpawn safe)
            {
                if (useBeforeIndex && indexInQuery < initialIndex)
                    return;
                if (!useBeforeIndex && indexInQuery >= initialIndex)
                    return;

                bool playerQueued = !spawnQueues.playerQueue.IsEmpty();
                bool aiQueued     = !spawnQueues.aiQueue.IsEmpty();
                bool isReady      = times.pauseTime <= 0f;

                if ((playerQueued || aiQueued) && isReady && safe.safe)
                {
                    var transform = transformHandle[indexInChunk];

                    if (playerQueued)
                        payload.disabledShip = spawnQueues.playerQueue.Dequeue();
                    else
                        payload.disabledShip = spawnQueues.aiQueue.Dequeue();

                    times.enableTime = spawnData.maxTimeUntilSpawn;
                    times.pauseTime  = spawnData.maxPauseTime;

                    var nsc                 = nscLookup[nscEntity];
                    var rotation            = nsc.random.NextQuaternionRotation();
                    transform.localRotation = quaternion.LookRotationSafe(math.forward(rotation), new float3(0f, 1f, 0f));

                    icb.Add(spawnData.spawnGraphicPrefab, new ParentCommand(entity));

                    nsc.index            = indexInQuery;
                    nscLookup[nscEntity] = nsc;
                }
            }

            public bool OnChunkBegin(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                // Cull entire chunks if we can
                if (spawnQueues.playerQueue.IsEmpty() && spawnQueues.aiQueue.IsEmpty())
                    return false;

                var baseIndex = __ChunkBaseEntityIndices[unfilteredChunkIndex];
                if (useBeforeIndex && baseIndex + chunk.Count <= initialIndex)
                    return false;
                if (!useBeforeIndex && baseIndex >= initialIndex)
                    return false;
                transformHandle.SetupChunk(in chunk);
                return true;
            }

            public void OnChunkEnd(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask, bool chunkWasExecuted)
            {
            }
        }
    }
}

