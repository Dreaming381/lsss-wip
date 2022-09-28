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
            var transCdfe = state.GetComponentLookup<Translation>(false);
            var rotCdfe   = state.GetComponentLookup<Rotation>(false);

            foreach ((var payload, var times, var entity) in SystemAPI.Query<RefRW<SpawnPayload>, RefRO<SpawnTimes> >().WithEntityAccess())
            {
                if (times.ValueRO.enableTime <= 0f && payload.ValueRO.disabledShip != Entity.Null)
                {
                    var ship = payload.ValueRO.disabledShip;
                    ecb.Add(ship);
                    var trans                    = transCdfe[entity];
                    var rot                      = rotCdfe[entity];
                    transCdfe[ship]              = trans;
                    rotCdfe[ship]                = rot;
                    payload.ValueRW.disabledShip = Entity.Null;
                }
            }

            ecb.Playback(state.EntityManager, state.GetBufferLookup<LinkedEntityGroup>(true));
            ecb.Dispose();
        }
    }
}

