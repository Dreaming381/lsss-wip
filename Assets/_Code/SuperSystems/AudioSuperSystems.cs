using Latios;

namespace Lsss
{
    public class AudioSuperSystem : SuperSystem
    {
        protected override void CreateSystems()
        {
            GetOrCreateAndAddUnmanagedSystem<AudioVolumeSystem>();
        }
    }
}

