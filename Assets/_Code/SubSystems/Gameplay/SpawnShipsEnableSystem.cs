using Latios;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace Lsss
{
    public class SpawnShipsEnableSystem : SubSystem
    {
        protected override void OnUpdate()
        {
            var enabledShipList = new NativeList<Entity>(Allocator.TempJob);

            var ecb = new EnableCommandBuffer(Allocator.TempJob);

            Entities.WithAll<SpawnPointTag>().ForEach((Entity entity, ref SpawnPayload payload, in SpawnTimes times) =>
            {
                if (times.enableTime <= 0f && payload.disabledShip != Entity.Null)
                {
                    var ship = payload.disabledShip;
                    ecb.Add(ship);
                    SetComponent(ship, GetComponent<Translation>(entity));
                    SetComponent(ship, GetComponent<Rotation>(entity));
                    payload.disabledShip = Entity.Null;

                    enabledShipList.Add(ship);
                }
            }).Run();

            ecb.Playback(EntityManager, GetBufferFromEntity<LinkedEntityGroup>(true));
            ecb.Dispose();

            //Todo: It seems that if you Instantiate and then immediately disable a Transform hierarchy, the disabled entities do not get their child buffers.
            //This hack attempts to dirty the children so that the transform system picks up on this.
            var linkedBfe  = GetBufferFromEntity<LinkedEntityGroup>(true);
            var parentCdfe = GetComponentDataFromEntity<Parent>(false);
            Job.WithCode(() =>
            {
                for (int i = 0; i < enabledShipList.Length; i++)
                {
                    var ship         = enabledShipList[i];
                    var linkedBuffer = linkedBfe[ship];
                    for (int j = 0; j < linkedBuffer.Length; j++)
                    {
                        var e = linkedBuffer[j].Value;
                        if (parentCdfe.HasComponent(e))
                        {
                            var p         = parentCdfe[e];
                            parentCdfe[e] = p;
                        }
                    }
                }
            }).Run();

            enabledShipList.Dispose();
        }
    }
}

