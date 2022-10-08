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
            var ecb = new EnableCommandBuffer(Allocator.TempJob);

            foreach ((var payload, var times, var entity) in Query<RefRW<SpawnPayload>, RefRO<SpawnTimes> >().WithEntityAccess())
            {
                if (times.ValueRO.enableTime <= 0f && payload.ValueRO.disabledShip != Entity.Null)
                {
                    var ship = payload.ValueRO.disabledShip;
                    ecb.Add(ship);
                    var trans = GetComponent<Translation>(entity);
                    var rot   = GetComponent<Rotation>(entity);
                    SetComponent(ship, trans);
                    SetComponent(ship, rot);
                    payload.ValueRW.disabledShip = Entity.Null;
                }
            }

            ecb.Playback(state.EntityManager, GetBufferLookup<LinkedEntityGroup>(true));
            ecb.Dispose();
        }
    }
}

