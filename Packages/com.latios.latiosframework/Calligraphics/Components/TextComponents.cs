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

    /// <summary>
    /// The backing memory for a GlyphMapper struct. Cast to a GlyphMapper
    /// to get a mapping of source string to RenderGlyph to post-process the glyphs.
    /// Usage: ReadOnly
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct GlyphMappingElement : IBufferElementData
    {
        public int2 element;
    }

    /// <summary>
    /// The mask used to command glyph generation which mappings to generate.
    /// Generating more mappings has a slightly large performance cost and a
    /// potentially significant memory cost.
    /// </summary>
    public struct GlyphMappingMask : IComponentData
    {
        public enum WriteMask : byte
        {
            None = 0,
            Line = 0x1,
            Word = 0x2,
            CharNoTags = 0x4,
            CharWithTags = 0x8,
            Byte = 0x10,
        }

        public WriteMask mask;
    }

    [InternalBufferCapacity(0)]
    internal struct PreviousCalliByte : ICleanupBufferElementData
    {
        public byte element;
    }
}

