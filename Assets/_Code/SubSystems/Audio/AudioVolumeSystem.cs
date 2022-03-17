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
    public partial class AudioVolumeSystem : SubSystem
    {
        protected override void OnCreate()
        {
            worldBlackboardEntity.AddComponentIfMissing<AudioMasterVolumes>();
        }

        protected override void OnUpdate()
        {
            var volumes = worldBlackboardEntity.GetComponentData<AudioMasterVolumes>();
            Entities.ForEach((ref AudioListener listener) =>
            {
                listener.volume = volumes.sfxVolume;
            }).Run();

            //Todo: Music
        }
    }
}

