using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;

namespace Latios.Calligraphics
{
    internal unsafe struct ShapeStream
    {
        NativeStream mainStream;
        NativeStream allocationStream;

        public ShapeStream(int foreachCount, AllocatorManager.AllocatorHandle allocator)
        {
            mainStream       = new NativeStream(foreachCount, allocator);
            allocationStream = new NativeStream(foreachCount, allocator);
        }

        public Writer AsWriter() => new Writer(this);
        public Reader AsReader() => new Reader(this);

        public struct Writer
        {
            NativeStream.Writer mainStream;
            NativeStream.Writer allocationStream;

            [NativeDisableUnsafePtrRestriction] Chunk* m_chunk;

            public Writer(ShapeStream shapeStream)
            {
                mainStream       = shapeStream.mainStream.AsWriter();
                allocationStream = shapeStream.allocationStream.AsWriter();
                m_chunk          = null;
            }

            public void BeginChunk(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledFiltering, in v128 chunkEnabledMask)
            {
                mainStream.BeginForEachIndex(unfilteredChunkIndex);
                allocationStream.BeginForEachIndex(unfilteredChunkIndex);
                m_chunk  = (Chunk*)UnsafeUtility.AddressOf(ref mainStream.Allocate<Chunk>());
                *m_chunk = new Chunk
                {
                    chunk       = chunk,
                    entityCount = 0,
                    reserved    = 0,
                };
            }

            public void EndChunk()
            {
                mainStream.EndForEachIndex();
                allocationStream.EndForEachIndex();
            }
        }

        public struct Reader
        {
            NativeStream.Reader mainStream;
            NativeStream.Reader allocationStream;

            public Reader(ShapeStream shapeStream)
            {
                mainStream       = shapeStream.mainStream.AsReader();
                allocationStream = shapeStream.allocationStream.AsReader();
            }

            public void BeginChunk(int unfilteredChunkIndex, out ArchetypeChunk chunk, out int entityCount)
            {
                mainStream.BeginForEachIndex(unfilteredChunkIndex);
                allocationStream.BeginForEachIndex(unfilteredChunkIndex);
                var data    = mainStream.Read<Chunk>();
                chunk       = data.chunk;
                entityCount = data.entityCount;
            }
            public void EndChunk()
            {
                mainStream.EndForEachIndex();
                allocationStream.EndForEachIndex();
            }

            public Entity PeakEntity() => mainStream.Peek<Entity>();
            public Entity ReadEntity() => mainStream.Read<Entity>();
            public XMLTag ReadXmlTag() => mainStream.Read<XMLTag>();
            public Shape ReadShape() => mainStream.Read<Shape>();
            public Glyph ReadGlyph() => mainStream.Read<Glyph>();
        }

        public struct Chunk
        {
            public ArchetypeChunk chunk;
            public int            entityCount;
            public int            reserved;
        }

        public struct Entity
        {
            public int entityIndexInChunk;
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
            public GlyphTable.Key glyphKey;
            public uint           cluster;
            public uint           xAdvance;
            public uint           yAdvance;
            public uint           xOffset;
            public uint           yOffset;
            public uint           reserved;
        }
    }
}

