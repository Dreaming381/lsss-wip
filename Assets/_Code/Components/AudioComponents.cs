using Unity.Entities;

namespace Lsss
{
    public struct AudioMasterVolumes : IComponentData
    {
        public float musicVolume;
        public float sfxVolume;
    }
}

