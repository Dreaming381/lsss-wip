using System;
using HarfbuzzUnity;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace Latios.Calligraphics
{
    internal partial struct FontTable : ICollectionComponent
    {
        public struct FaceEntry
        {
            public IntPtr facePtr;
        }

        public NativeList<FaceEntry>            faceEntries;
        public NativeArray<UnsafeList<IntPtr> > perThreadFontCaches;

        public IntPtr GetOrCreateFont(int faceIndex, int threadIndex)
        {
            var fonts = perThreadFontCaches[threadIndex];
            var font  = fonts[faceIndex];
            if (font == IntPtr.Zero)
            {
                font             = Harfbuzz.hb_font_create(faceEntries[faceIndex].facePtr);
                fonts[faceIndex] = font;
            }
            return font;
        }

        public JobHandle TryDispose(JobHandle inputDeps)
        {
            if (faceEntries.IsCreated)
            {
                var jh = new DisposeInnerJob { table = this }.Schedule(inputDeps);
                return JobHandle.CombineDependencies(faceEntries.Dispose(jh), perThreadFontCaches.Dispose(jh));
            }
            return inputDeps;
        }

        struct DisposeInnerJob : IJob
        {
            public FontTable table;

            public void Execute()
            {
                for (int thread = 0; thread < table.perThreadFontCaches.Length; thread++)
                {
                    var list = table.perThreadFontCaches[thread];
                    foreach (var font in list)
                    {
                        if (font == IntPtr.Zero)
                            continue;
                        Harfbuzz.hb_font_destroy(font);
                    }
                    list.Dispose();
                }
                foreach (var entry in table.faceEntries)
                {
                    // Todo: Destroy Face object.
                }

                // Todo: Destroy Blob objects.
            }
        }
    }

    enum RenderFormat : byte
    {
        SDF8 = 0,
        SDF16 = 1,
        Bitmap8888 = 2,
    }

    internal partial struct GlyphTable : ICollectionComponent
    {
        public struct Key : IEquatable<Key>, IComparable<Key>
        {
            public ulong packed;

            public ushort glyphIndex
            {
                get => (ushort)Bits.GetBits(packed, 0, 16);
                set => Bits.SetBits(ref packed, 0, 16, value);
            }

            public int faceIndex
            {
                get => (int)Bits.GetBits(packed, 16, 20);
                set => Bits.SetBits(ref packed, 16, 20, (uint)value);
            }

            public RenderFormat format
            {
                get => (RenderFormat)Bits.GetBits(packed, 36, 2);
                set => Bits.SetBits(ref packed, 36, 2, (uint)value);
            }

            public FontTextureSize textureSize
            {
                get => (FontTextureSize)Bits.GetBits(packed, 38, 2);
                set => Bits.SetBits(ref packed, 38, 2, (uint)value);
            }

            public int variableProfileIndex
            {
                get => (int)Bits.GetBits(packed, 40, 24);
                set => Bits.SetBits(ref packed, 40, 24, (uint)value);
            }

            public bool Equals(Key other) => packed.Equals(other.packed);
            public override int GetHashCode() => packed.GetHashCode();
            public int CompareTo(Key other) => packed.CompareTo(other.packed);
        }

        public struct Entry
        {
            public Key   key;
            public int   refCount;
            public short x;
            public short y;
            public short z;
            public short width;
            public short height;
            public short xBearing;
            public short yBearing;

            public bool isInAtlas => x >= 0;
            // Todo:
        }

        public NativeHashMap<Key, uint> glyphHashToIdMap;
        public NativeList<Entry>        entries;

        public JobHandle TryDispose(JobHandle inputDeps)
        {
            if (glyphHashToIdMap.IsCreated)
            {
                return JobHandle.CombineDependencies(glyphHashToIdMap.Dispose(inputDeps), entries.Dispose(inputDeps));
            }
            return inputDeps;
        }

        public ref Entry GetEntryRW(uint glyphEntryID)
        {
            return ref entries.ElementAt((int)(glyphEntryID & 0x3fffffff));
        }

        public Entry GetEntry(uint glyphEntryID)
        {
            return entries[(int)(glyphEntryID & 0x3fffffff)];
        }
    }

    internal partial struct GlyphGpuTable : ICollectionComponent
    {
        public struct Totals
        {
            public uint resident;
            public uint dynamic;
        }

        public NativeReference<Totals> totals;
        public NativeList<uint2>       residentGaps;
        public NativeList<uint2>       dispatchDynamicGaps;  // Deferred gaps when multiple dispatches need to skip over previous dynamic regions

        public JobHandle TryDispose(JobHandle inputDeps)
        {
            if (totals.IsCreated)
            {
                return JobHandle.CombineDependencies(totals.Dispose(inputDeps), residentGaps.Dispose(inputDeps), dispatchDynamicGaps.Dispose(inputDeps));
            }
            return inputDeps;
        }
    }

    internal partial struct AtlasTable : ICollectionComponent
    {
        // Todo:
        public JobHandle TryDispose(JobHandle inputDeps)
        {
            throw new System.NotImplementedException();
        }

        public void Allocate(uint glyphEntryId, short width, short height, out short x, out short y, out short z)
        {
            throw new System.NotImplementedException();
        }

        public void Free(uint glyphEntryId, short width, short height, short x, short y, short z)
        {
            throw new System.NotImplementedException();
        }
    }
}

