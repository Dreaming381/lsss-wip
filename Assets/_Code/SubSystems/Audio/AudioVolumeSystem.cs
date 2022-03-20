using Latios;
using Latios.Myri;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace Lsss
{
    [BurstCompile]
    public partial struct AudioVolumeSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.GetWorldBlackboardEntity().AddComponentIfMissing<AudioMasterVolumes>();
        }
        [BurstCompile] public void OnDestroy(ref SystemState state) {
        }
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var volumes = state.GetWorldBlackboardEntity().GetComponentData<AudioMasterVolumes>();
            state.Entities.ForEach((ref AudioListener listener) =>
            {
                listener.volume = volumes.sfxVolume;
            }).Run();

            //Todo: Music
        }
    }
}

