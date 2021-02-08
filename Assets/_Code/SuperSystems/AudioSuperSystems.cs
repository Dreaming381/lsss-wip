using Latios;

namespace Lsss
{
    public class AudioSuperSystem : SuperSystem
    {
        protected override void CreateSystems()
        {
            GetOrCreateAndAddSystem<AudioVolumeSystem>();
        }
    }
}

