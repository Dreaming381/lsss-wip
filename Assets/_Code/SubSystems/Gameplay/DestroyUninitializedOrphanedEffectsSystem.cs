using Latios;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

//Firing effects will spawn parented to their ships.
//If the ship is destroyed the same frame, then the parent won't exist
//when the transform system runs which produces an error.
//Since the transform data is inherited from the parent which no longer
//exists and is thus lost, we should just kill this effect entity early.

namespace Lsss
{
    [BurstCompile]
    public partial struct DestroyUninitializedOrphanedEffectsSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state) {
        }
        [BurstCompile]
        public void OnDestroy(ref SystemState state) {
        }
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var dcb = new DestroyCommandBuffer(Allocator.TempJob);
            // LocalToWorld is what Parent System uses to reactively remove Child buffers.
            // Testing for entity existence here doesn't work since the entity may be getting cleaned up.
            var ltwCdfe = state.GetComponentDataFromEntity<LocalToWorld>(true);

            state.Entities.WithNone<PreviousParent>().ForEach((Entity entity, in Parent parent) =>
            {
                if (!ltwCdfe.HasComponent(parent.Value))
                {
                    dcb.Add(entity);
                }
            }).WithReadOnly(ltwCdfe).Run();

            dcb.Playback(state.EntityManager);
            dcb.Dispose();
        }
    }
}

