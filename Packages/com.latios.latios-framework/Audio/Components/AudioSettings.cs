using Unity.Entities;

namespace Latios.Audio
{
    public struct AudioSettings : IComponentData
    {
        public int  audioFramesPerUpdate;
        public int  audioSubframesPerFrame;
        public bool logWarningIfBuffersAreStarved;
    }
}

