using Latios;
using Latios.Transforms;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

using static Unity.Entities.SystemAPI;

namespace Lsss
{
    [BurstCompile]
    public partial struct SpawnShipsEnableSystem : ISystem
    {
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

                    var entityTransform          = GetComponent<WorldTransform>(entity);
                    var shipTransform            = state.EntityManager.GetTransfromAspect(ship);
                    shipTransform.worldRotation  = entityTransform.rotation;
                    shipTransform.worldPosition  = entityTransform.position;
                    payload.ValueRW.disabledShip = Entity.Null;
                }
            }

            ecb.Playback(state.EntityManager, GetBufferLookup<LinkedEntityGroup>(true));
            ecb.Dispose();
        }
    }
}

