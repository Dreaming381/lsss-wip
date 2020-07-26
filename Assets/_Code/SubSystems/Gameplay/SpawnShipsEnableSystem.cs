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

            Entities.WithAll<SpawnPointTag>().ForEach((ref SpawnPayload payload, in SpawnTimes times, in Translation trans, in Rotation rot) =>
            {
                if (times.enableTime <= 0f && payload.disabledShip != Entity.Null)
                {
                    var ship = payload.disabledShip;
                    EntityManager.SetEnabled(ship, true);
                    EntityManager.SetComponentData(ship, trans);
                    EntityManager.SetComponentData(ship, rot);
                    payload.disabledShip = Entity.Null;

                    enabledShipList.Add(ship);
                }
            }).WithStructuralChanges().Run();

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

