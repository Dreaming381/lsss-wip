using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace Latios.Calligraphics
{
    internal partial struct FontTable : ICollectionComponent
    {
        // Todo:
        public JobHandle TryDispose(JobHandle inputDeps)
        {
            throw new System.NotImplementedException();
        }
    }

    internal partial struct GlyphTable : ICollectionComponent
    {
        // Todo:
        public JobHandle TryDispose(JobHandle inputDeps)
        {
            throw new System.NotImplementedException();
        }
    }
}

