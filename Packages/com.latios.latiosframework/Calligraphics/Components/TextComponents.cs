using System.Runtime.InteropServices;
using Latios.Calligraphics.RichText;
using Unity.Entities;

namespace Latios.Calligraphics
{
    /// <summary>
    /// The raw byte element as part of the text string.
    /// Cast to CalliString to read  /write.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct CalliByte : IBufferElementData
    {
        public byte element;
    }

    internal struct XMLTag
    {
        public TagType tagType;
        public bool    isClosing;
        public int     startID;  //start position raw text
        public int     endID;  //end position raw text
        public int Length => endID + 1 - startID;
        public TagValue value;
        public XMLTag(bool dummy)
        {
            tagType    = TagType.Unknown;
            isClosing  = false;
            startID    = -1;
            endID      = -1;
            value      = new TagValue();
            value.type = TagValueType.None;
            value.unit = TagUnitType.Pixels;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct GlyphOTF
    {
        internal GlyphTable.Key glyphKey;
        public uint             cluster;
        // xAdvance and yAdvance are already scaled in xy local-space (glyph quad vertices space) in Unity units.
        // These are not HarfBuzz font-unit advances.
        public float xAdvance;
        public float yAdvance;
        public float baseline;
        public int   xOffset;
        public int   yOffset;
        public override string ToString()
        {
            return $"Advance (x,y): {xAdvance}, {yAdvance}  Offset (x,y): {xOffset}, {yOffset}";
        }
    }
}

