using Unity.Collections;
using Unity.Mathematics;

namespace Latios.Calligraphics
{
    internal unsafe struct ShapeStream
    {
        public struct Writer
        {
            NativeStream.Writer writer;

            public Writer(NativeStream.Writer writer) => this.writer = writer;
        }

        public struct Reader
        {
            NativeStream.Reader reader;

            public Reader(NativeStream.Reader reader) => this.reader = reader;

            public Entity ReadEntity() => reader.Read<Entity>();
            public XMLTag ReadXmlTag() => reader.Read<XMLTag>();
            public Shape ReadShape() => reader.Read<Shape>();
            public Glyph ReadGlyph() => reader.Read<Glyph>();
        }

        public struct Chunk
        {
        }

        public struct Entity
        {
            public int xmlTagCount;
            public int shapeCount;
            public int glyphCount;
        }

        public struct Shape
        {
            public int                           glyphCount;
            public int                           materialPadding;  // Todo: Find a better home for this.
            public FontConstants*                fontConstants;
            public FontDirectionConstants*       fontDirectionConstants;
            public FontDirectionScriptConstants* fontDirectionScriptConstants;
        }

        public struct Glyph
        {
            public uint codepoint;
            public uint cluster;
            public uint xAdvance;
            public uint yAdvance;
            public uint xOffset;
            public uint yOffset;
        }
    }
}

