using Latios;
using Latios.Myri;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace Lsss
{
    [BurstCompile]
    public partial struct AudioVolumeSystem : ISystem
    {
        LatiosWorldUnmanaged latiosWorld;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            latiosWorld = state.GetLatiosWorldUnmanaged();

            latiosWorld.worldBlackboardEntity.AddComponent<AudioMasterVolumes>();
        }
        [BurstCompile] public void OnDestroy(ref SystemState state) {
        }
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var volumes = latiosWorld.worldBlackboardEntity.GetComponentData<AudioMasterVolumes>();

            foreach (var listener in SystemAPI.Query<RefRW<AudioListener> >())
                listener.ValueRW.volume = volumes.sfxVolume * 0.5f; // Myri used to have a bug where everything would play at half volume.

            //Todo: Music
        }
    }
}

