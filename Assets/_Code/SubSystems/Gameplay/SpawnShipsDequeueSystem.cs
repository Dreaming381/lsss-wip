using Latios;
using Latios.Transforms;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace Lsss
{
    [RequireMatchingQueriesForUpdate]
    [BurstCompile]
    public partial struct SpawnShipsDequeueSystem : ISystem, ILatiosApi, ISystemNewScene
    {
        struct NextSpawnCounter : IComponentData
        {
            public int    index;
            public Random random;
        }

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            this.OnCreateForLatios(ref state);
        }

        public void OnNewScene(ref SystemState state) => this.GetApi(ref state).sceneBlackboardEntity.AddComponentData(new NextSpawnCounter
        {
            index  = 0,
            random = new Random(57108)
        });

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var api                = this.GetApi(ref state);
            new SpawnTimesJob { dt = api.deltaTime }.Schedule(api);

            var    spawnQueues  = api.sceneBlackboardEntity.GetCollectionComponent<SpawnQueues>();
            int    initialIndex = api.sceneBlackboardEntity.GetComponentData<NextSpawnCounter>().index;
            Entity nscEntity    = api.sceneBlackboardEntity;
            var    icb          = api.syncPoint.CreateInstantiateCommandBuffer<ParentCommand>();

            var job = new SpawnDequeueJob
            {
                icb            = icb,
                initialIndex   = initialIndex,
                useBeforeIndex = true,
                nscEntity      = nscEntity,
                spawnQueues    = spawnQueues,
            };
            job.Schedule(api);
            job.useBeforeIndex = false;
            job.Schedule(api);
        }

        [With(typeof(SpawnPointTag))]
        [BurstCompile]
        partial struct SpawnTimesJob : IJobEach
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

        [With(typeof(SpawnPointTag))]
        [BurstCompile]
        partial struct SpawnDequeueJob : IJobEach
        {
            public int  initialIndex;
            public bool useBeforeIndex;

            public SpawnQueues                                     spawnQueues;
            public Entity                                          nscEntity;
            public InstantiateCommandBufferCommand1<ParentCommand> icb;
            [Inject] ComponentLookup<NextSpawnCounter>             nscLookup;

            public void Execute(Entity entity,
                                in IJobEach.JobContext context,
                                [RootOnly] TransformAspect transform,
                                ref SpawnPayload payload,
                                ref SpawnTimes times,
                                in SpawnPoint spawnData,
                                in SafeToSpawn safe)
            {
                if (useBeforeIndex && context.entityIndexInQuery < initialIndex)
                    return;
                if (!useBeforeIndex && context.entityIndexInQuery >= initialIndex)
                    return;

                bool playerQueued = !spawnQueues.playerQueue.IsEmpty();
                bool aiQueued     = !spawnQueues.aiQueue.IsEmpty();
                bool isReady      = times.pauseTime <= 0f;

                if ((playerQueued || aiQueued) && isReady && safe.safe)
                {
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

                    nsc.index            = context.entityIndexInQuery;
                    nscLookup[nscEntity] = nsc;
                }
            }

            public bool OnChunkBegin(in IJobEach.JobContext context)
            {
                // Cull entire chunks if we can
                if (spawnQueues.playerQueue.IsEmpty() && spawnQueues.aiQueue.IsEmpty())
                    return false;

                var baseIndex = context.entityIndexInQuery;
                if (useBeforeIndex && baseIndex + context.chunk.Count <= initialIndex)
                    return false;
                if (!useBeforeIndex && baseIndex >= initialIndex)
                    return false;
                return true;
            }
        }
    }
}

