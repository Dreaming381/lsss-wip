using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;

namespace Latios.Calligraphics
{
    /// <summary>
    /// The glyphs to be rendered based on the processed CalliByte buffer.
    /// Copy this buffer to AnimatedRenderGlyph to apply animation to the data.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct RenderGlyph : IBufferElementData
    {
        public float2 blPosition;
        public float2 brPosition;
        public float2 tlPosition;
        public float2 trPosition;

        public float2 blUVB;
        public float2 brUVB;
        public float2 tlUVB;
        public float2 trUVB;

        public half4 blColor;
        public half4 brColor;
        public half4 tlColor;
        public half4 trColor;

        // These should be normalized relative to the padded bounding box extents of [0, 1]
        // The uploader will patch these with the atlas coordinates using math.lerp()
        public float2 blUVA;
        public float2 trUVA;

        public uint  arrayIndex;  // Converted to float in upload shader
        public uint  glyphEntryId;
        public float scale;
        public uint  reserved;
    }

    /// <summary>
    /// When this buffer is present, it overrides the RenderGlyph buffer for rendering purposes.
    /// Copy the RenderGlyph buffer into this buffer and then modify the glyphs for animation purposes
    /// within AnimateGlyphsSuperSystem.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct AnimatedRenderGlyph : IBufferElementData
    {
        public RenderGlyph glyph;
    }

    /// <summary>
    /// This is the primary material property for glyphs. Ensure its existence, but otherwise it
    /// will be maintained automatically.
    /// </summary>
    [MaterialProperty("_latiosTextGlyphBase")]
    public struct TextShaderIndex : IComponentData
    {
        public uint firstGlyphIndex;
        public uint glyphCount;
    }

    /// <summary>
    /// You must add this component in order for the glyphs to be rendered.
    /// This component and its enabled state serves internal purposes and should not be interacted
    /// with directly other than to ensure its existence.
    /// </summary>
    public struct GpuState : IComponentData, IEnableableComponent  // Enabled to request dispatch
    {
        internal enum State : byte
        {
            Uncommitted,
            Dynamic,
            DynamicPromoteToResident,
            Resident
        }
        internal State state;
    }

    [InternalBufferCapacity(0)]
    internal struct PreviousRenderGlyph : IBufferElementData
    {
        public RenderGlyph glyph;
    }

    internal struct ResidentRange : ICleanupComponentData
    {
        public uint start;
        public uint count;
    }
}

