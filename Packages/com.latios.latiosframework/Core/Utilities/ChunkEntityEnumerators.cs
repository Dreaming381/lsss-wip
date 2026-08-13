using System.Runtime.CompilerServices;
using Unity.Burst.Intrinsics;
using Unity.Entities;
using Unity.Mathematics;

namespace Latios
{
    public struct ChunkEntityWithIndexEnumerator
    {
        ChunkEntityBatchEnumerator m_enumerator;
        int                        m_entityInQueryIndex;
        int                        m_nextIndexInChunk;
        int                        m_rangeEnd;

        public ChunkEntityWithIndexEnumerator(bool useEnabledMask, v128 chunkEnabledMask, int chunkEntityCount, int baseEntityInQueryIndexForChunk)
        {
            m_enumerator         = new ChunkEntityBatchEnumerator(useEnabledMask, chunkEnabledMask, chunkEntityCount);
            m_entityInQueryIndex = baseEntityInQueryIndexForChunk;
            m_nextIndexInChunk   = 0;
            m_rangeEnd           = 0;
        }

        /// <summary>
        /// Use as the condition in a while loop
        /// </summary>
        /// <param name="indexInChunk">The index in the chunk for accessing arrays in chunks</param>
        /// <param name="indexInEntityQuery">The index in the query for accessing external arrays</param>
        /// <returns>If true, the indices are valid for processing. If false, there are no more entities to process.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool NextEntityIndex(out int indexInChunk, out int indexInEntityQuery)
        {
            indexInEntityQuery = m_entityInQueryIndex;
            m_entityInQueryIndex++;

            if (m_nextIndexInChunk >= m_rangeEnd)
            {
                if (!m_enumerator.NextRange(out var rangeStart, out var rangeCount))
                {
                    indexInChunk = 0;
                    return false;
                }
                m_nextIndexInChunk = rangeStart;
                m_rangeEnd         = rangeStart + rangeCount;
            }

            indexInChunk = m_nextIndexInChunk;
            m_nextIndexInChunk++;
            return true;
        }
    }

    public unsafe struct ChunkEntityBatchEnumerator
    {
        fixed byte starts[64];
        fixed byte counts[64];
        int        index;
        int        count;

        public ChunkEntityBatchEnumerator(bool useEnabledMask, v128 chunkEnabledMask, int chunkEntityCount)
        {
            this  = default;
            index = -1;
            if (!useEnabledMask)
            {
                starts[0] = 0;
                counts[0] = (byte)chunkEntityCount;
                count     = 1;
                return;
            }

            var lower = chunkEnabledMask.ULong0;
            if (chunkEntityCount < 64)
                lower  &= (1ul << chunkEntityCount) - 1;
            int offset  = 0;
            while (lower != 0)
            {
                var firstBit    = math.tzcnt(lower);
                lower         >>= firstBit;
                var flipped     = ~lower;
                var c           = math.tzcnt(flipped);
                starts[count]   = (byte)(firstBit + offset);
                counts[count]   = (byte)c;
                offset         += firstBit + c;
                lower         >>= c;
                count++;
            }
            if (chunkEntityCount < 64)
                return;

            var upper = chunkEnabledMask.ULong1;
            if (chunkEntityCount < 128)
                upper &= (1ul << (chunkEntityCount - 64)) - 1;
            offset     = 64;
            if (count != 0 && starts[count - 1] + counts[count - 1] == 64 && (upper & 1) != 0)
            {
                // Do a single merge pass
                var flipped         = ~upper;
                var c               = math.tzcnt(flipped);
                counts[count - 1]  += (byte)c;
                offset             += c;
                upper             >>= c;
            }
            while (upper != 0)
            {
                var firstBit    = math.tzcnt(upper);
                upper         >>= firstBit;
                var flipped     = ~upper;
                var c           = math.tzcnt(flipped);
                starts[count]   = (byte)(firstBit + offset);
                counts[count]   = (byte)c;
                offset         += firstBit + c;
                upper         >>= c;
                count++;
            }
        }

        public bool NextRange(out int start, out int count)
        {
            index++;
            if (index < this.count)
            {
                start = starts[index];
                count = counts[index];
                return true;
            }
            start = 0;
            count = 0;
            return false;
        }
    }
}

