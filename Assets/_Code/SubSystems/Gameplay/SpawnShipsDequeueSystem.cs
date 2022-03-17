using Latios;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace Lsss
{
    public partial class SpawnShipsDequeueSystem : SubSystem
    {
        struct NextSpawnCounter : IComponentData
        {
            public int    index;
            public Random random;
        }

        public override void OnNewScene() => sceneBlackboardEntity.AddComponentData(new NextSpawnCounter { index = 0, random = new Random(57108) });

        protected override void OnUpdate()
        {
            float dt = Time.DeltaTime;
            Entities.WithAll<SpawnPointTag>().ForEach((ref SpawnTimes spawnTimes) =>
            {
                spawnTimes.enableTime -= dt;
                spawnTimes.pauseTime  -= dt;

                spawnTimes.enableTime = math.max(spawnTimes.enableTime, 0f);
                spawnTimes.pauseTime  = math.max(spawnTimes.pauseTime, 0f);
            }).Schedule();

            var    spawnQueues  = sceneBlackboardEntity.GetCollectionComponent<SpawnQueues>();
            int    initialIndex = sceneBlackboardEntity.GetComponentData<NextSpawnCounter>().index;
            Entity nscEntity    = sceneBlackboardEntity;
            var    icb          = latiosWorld.syncPoint.CreateInstantiateCommandBuffer<Parent>();
            icb.AddComponentTag<LocalToParent>();

            Entities.WithAll<SpawnPointTag>().ForEach((Entity entity, int entityInQueryIndex, ref SpawnPayload payload, ref SpawnTimes times, ref Rotation rotation,
                                                       in Translation translation,
                                                       in SpawnPoint spawnData, in SafeToSpawn safe) =>
            {
                if (entityInQueryIndex < initialIndex)
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

                    var nsc        = GetComponent<NextSpawnCounter>(nscEntity);
                    rotation.Value = nsc.random.NextQuaternionRotation();
                    rotation.Value = quaternion.LookRotationSafe(math.forward(rotation.Value), new float3(0f, 1f, 0f));

                    icb.Add(spawnData.spawnGraphicPrefab, new Parent { Value = entity });

                    nsc.index = entityInQueryIndex;
                    SetComponent(nscEntity, nsc);
                }
            }).Schedule();

            //Todo: There's got to be a more clever way to do this rather than duplicating the Entities.ForEach just to change the early-out condition.
            Entities.WithAll<SpawnPointTag>().ForEach((Entity entity, int entityInQueryIndex, ref SpawnPayload payload, ref SpawnTimes times, ref Rotation rotation,
                                                       in Translation translation, in SpawnPoint spawnData, in SafeToSpawn safe) =>
            {
                if (entityInQueryIndex >= initialIndex)
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

                    var nsc        = GetComponent<NextSpawnCounter>(nscEntity);
                    rotation.Value = nsc.random.NextQuaternionRotation();
                    rotation.Value = quaternion.LookRotationSafe(math.forward(rotation.Value), new float3(0f, 1f, 0f));

                    icb.Add(spawnData.spawnGraphicPrefab, new Parent { Value = entity });

                    nsc.index = entityInQueryIndex;
                    SetComponent(nscEntity, nsc);
                }
            }).Schedule();
        }
    }
}

