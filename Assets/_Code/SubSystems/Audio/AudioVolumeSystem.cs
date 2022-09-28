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
            state.GetWorldBlackboardEntity().AddComponent<AudioMasterVolumes>();
        }
        [BurstCompile] public void OnDestroy(ref SystemState state) {
        }
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var volumes = state.GetWorldBlackboardEntity().GetComponentData<AudioMasterVolumes>();

            foreach (var listener in SystemAPI.Query<RefRW<AudioListener> >())
                listener.ValueRW.volume = volumes.sfxVolume;

            //Todo: Music
        }
    }
}

