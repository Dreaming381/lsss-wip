using Latios;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace Lsss
{
    [BurstCompile]
    public partial struct SpawnShipsEnableSystem : ISystem
    {
        [BurstCompile] public void OnCreate(ref SystemState state)
        {
        }
        [BurstCompile] public void OnDestroy(ref SystemState state)
        {
        }
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb       = new EnableCommandBuffer(Allocator.TempJob);
            var transCdfe = state.GetComponentDataFromEntity<Translation>(false);
            var rotCdfe   = state.GetComponentDataFromEntity<Rotation>(false);

            state.Entities.WithAll<SpawnPointTag>().ForEach((Entity entity, ref SpawnPayload payload, in SpawnTimes times) =>
            {
                if (times.enableTime <= 0f && payload.disabledShip != Entity.Null)
                {
                    var ship = payload.disabledShip;
                    ecb.Add(ship);
                    var trans            = transCdfe[entity];
                    var rot              = rotCdfe[entity];
                    transCdfe[ship]      = trans;
                    rotCdfe[ship]        = rot;
                    payload.disabledShip = Entity.Null;
                }
            }).WithReadOnly(transCdfe).WithReadOnly(rotCdfe).Run();

            ecb.Playback(state.EntityManager, state.GetBufferFromEntity<LinkedEntityGroup>(true));
            ecb.Dispose();
        }
    }
}

