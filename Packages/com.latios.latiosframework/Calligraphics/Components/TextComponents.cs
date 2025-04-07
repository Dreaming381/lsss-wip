using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Latios.Calligraphics
{
    /// <summary>
    /// The raw byte element as part of the text string.
    /// Prefer to use TextRendererAspect or cast to CalliString instead.
    /// Usage: ReadWrite, but using the abstraction tools.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct CalliByte : IBufferElementData
    {
        public byte element;
    }

    /// <summary>
    /// You must add this to any entity with CalliByte. This component and its enabled state
    /// serves internal purposes and should not be interacted with directly other than ensuring
    /// its existence.
    /// </summary>
    public struct CalliByteChangedFlag : IComponentData, IEnableableComponent { }

    [InternalBufferCapacity(0)]
    internal struct PreviousCalliByte : ICleanupBufferElementData
    {
        public byte element;
    }
}

