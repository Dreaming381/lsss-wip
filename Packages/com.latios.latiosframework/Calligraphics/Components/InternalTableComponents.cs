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

    enum RenderFormat : byte
    {
        SDF8 = 0,
        SDF16 = 1,
        Bitmap8888 = 2,
    }

    internal partial struct GlyphTable : ICollectionComponent
    {
        public struct Entry
        {
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

        public NativeHashMap<ulong, uint> glyphHashToIdMap;
        public NativeList<Entry>          entries;

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

