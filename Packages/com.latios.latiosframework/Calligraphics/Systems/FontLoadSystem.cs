using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace Latios.Calligraphics.Systems
{
    [DisableAutoCreation]
    [BurstCompile]
    public partial struct FontLoadSystem : ISystem
    {
        LatiosWorldUnmanaged latiosWorld;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            latiosWorld = state.GetLatiosWorldUnmanaged();

            state.Fluent().With<CalliByte, CalliByteChangedFlag, TextBaseConfiguration>(true).Build();

            latiosWorld.worldBlackboardEntity.AddOrSetCollectionComponentAndDisposeOld(new FontTable
            {
                // Todo:
            });
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var fontTable = latiosWorld.worldBlackboardEntity.GetCollectionComponent<FontTable>(false);
        }
    }
}

