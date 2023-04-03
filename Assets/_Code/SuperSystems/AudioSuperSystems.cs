using Latios;

namespace Lsss
{
    public partial class AudioSuperSystem : SuperSystem
    {
        protected override void CreateSystems()
        {
            GetOrCreateAndAddUnmanagedSystem<AudioVolumeSystem>();
        }
    }
}

