using Latios;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

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

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
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
            var    icb          = latiosWorld.syncPoint.CreateInstantiateCommandBuffer<Parent>();
            icb.AddComponentTag<LocalToParent>();

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

        [WithAll(typeof(SpawnPointTag))]
        [BurstCompile]
        partial struct SpawnDequeueJob : IJobEntity
        {
            public int  initialIndex;
            public bool useBeforeIndex;

            public SpawnQueues                       spawnQueues;
            public Entity                            nscEntity;
            public InstantiateCommandBuffer<Parent>  icb;
            public ComponentLookup<NextSpawnCounter> nscLookup;

            public void Execute(Entity entity, [EntityIndexInQuery] int entityInQueryIndex, ref SpawnPayload payload, ref SpawnTimes times, ref Rotation rotation,
                                in SpawnPoint spawnData, in SafeToSpawn safe)
            {
                if (useBeforeIndex && entityInQueryIndex < initialIndex)
                    return;
                if (!useBeforeIndex && entityInQueryIndex >= initialIndex)
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

                    var nsc        = nscLookup[nscEntity];
                    rotation.Value = nsc.random.NextQuaternionRotation();
                    rotation.Value = quaternion.LookRotationSafe(math.forward(rotation.Value), new float3(0f, 1f, 0f));

                    icb.Add(spawnData.spawnGraphicPrefab, new Parent { Value = entity });

                    nsc.index            = entityInQueryIndex;
                    nscLookup[nscEntity] = nsc;
                }
            }
        }
    }
}

