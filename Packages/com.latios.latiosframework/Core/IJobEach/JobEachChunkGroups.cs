using System.Diagnostics;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;

namespace Latios
{
    public partial interface IJobEach
    {
        /// <summary>
        /// A contiguous run of captured chunks an IJobEach must process together on a single thread.
        /// </summary>
        public struct ChunkGroupRange
        {
            /// <summary>
            /// The index of the first chunk of the group within the captured chunk array
            /// </summary>
            public int start;
            /// <summary>
            /// The number of chunks belonging to the group
            /// </summary>
            public int count;
        }

        /// <summary>
        /// The metadata captured for a single chunk during an IJobEach's chunk-collecting setup pass.
        /// </summary>
        public struct CapturedChunk
        {
            /// <summary>
            /// The chunk
            /// </summary>
            public ArchetypeChunk chunk;
            /// <summary>
            /// The enabled mask of the chunk. Only meaningful when useEnabledMask is true.
            /// </summary>
            public v128 chunkEnabledMask;
            /// <summary>
            /// The index of the chunk within the unfiltered EntityQuery
            /// </summary>
            public int unfilteredChunkIndex;
            /// <summary>
            /// The number of matching entities in all preceding chunks, in EntityQuery order.
            /// This is captured before grouping reorders chunks, so it is stable regardless of processing order.
            /// </summary>
            public int firstEntityIndexInQuery;
            /// <summary>
            /// The number of entities in this chunk matching the EntityQuery
            /// </summary>
            public int enabledEntityCount;
            /// <summary>
            /// Whether chunkEnabledMask must be consulted to determine which entities in the chunk match the query
            /// </summary>
            public bool useEnabledMask;
        }

        /// <summary>
        /// The write-side view of an IJobEach's chunk grouping, passed to each IGroupedParameterHandle's setup job(s).
        /// A handle should inspect the captured chunks and calls Union() to declare which groups must be processed together.
        /// </summary>
        /// <remarks>
        /// Constraints only ever merge groups, never split them. This lets a job use several grouped parameters at once.
        /// </remarks>
        public struct ChunkGroupBuilder
        {
            /// <summary>
            /// The captured chunks, reordered by group
            /// </summary>
            public NativeList<CapturedChunk> chunks;
            /// <summary>
            /// The range of chunks within each group
            /// </summary>
            public NativeList<ChunkGroupRange> groupRanges;

            UnsafeList<int> sets;

            /// <summary>
            /// Declares that two groups must be combined so that they are processed together on a single thread.
            /// </summary>
            /// <param name="groupA">The index of the first group</param>
            /// <param name="groupB">The index of the second group</param>
            public void Union(int groupA, int groupB)
            {
                CheckIndexInRange(groupA, groupRanges.Length);
                CheckIndexInRange(groupB, groupRanges.Length);

                if (groupA == groupB)
                    return;

                if (!sets.IsCreated)
                {
                    var rangeCount = groupRanges.Length;
                    sets           = new UnsafeList<int>(rangeCount, Allocator.Temp);
                    for (int i = 0; i < rangeCount; i++)
                        sets.AddNoResize(i);
                }

                // Rem's algorithm
                var treeNodeA = groupA;
                var treeNodeB = groupB;
                while (sets[treeNodeA] != sets[treeNodeB])
                {
                    if (sets[treeNodeA] < sets[treeNodeB])
                    {
                        if (sets[treeNodeA] == treeNodeA)
                        {
                            sets[treeNodeA] = sets[treeNodeB];
                            break;
                        }
                        var temp        = sets[treeNodeA];
                        sets[treeNodeA] = sets[treeNodeB];
                        treeNodeA       = temp;
                    }
                    else
                    {
                        if (sets[treeNodeB] == treeNodeB)
                        {
                            sets[treeNodeB] = sets[treeNodeA];
                            break;
                        }
                        var temp        = sets[treeNodeB];
                        sets[treeNodeB] = sets[treeNodeA];
                        treeNodeB       = temp;
                    }
                }
            }
            /// <summary>
            /// Applies every union declared so far to the chunks and groupRanges lists
            /// </summary>
            public unsafe void ApplyChanges()
            {
                if (!sets.IsCreated)
                {
                    // There were no unions. Leave everything as-is.
                    return;
                }

                // Collapse the trees (we iterate backwards because we are using the greater-index algorithm)
                // Also, get count in each set as we will use a counting sort to reorder the groups
                var chunkCounts = new NativeArray<int>(sets.Length, Allocator.Temp, NativeArrayOptions.ClearMemory);
                for (int i = groupRanges.Length - 1; i >= 0; i--)
                {
                    var rootSet           = sets[sets[i]];
                    chunkCounts[rootSet] += groupRanges[i].count;
                    sets[i]               = rootSet;
                }

                // Prefix sum and build ranges
                int running   = 0;
                var dstRanges = new UnsafeList<ChunkGroupRange>(groupRanges.Length, Allocator.Temp);
                for (int i = 0; i < sets.Length; i++)
                {
                    var count      = chunkCounts[i];
                    chunkCounts[i] = running;
                    if (count > 0)
                    {
                        dstRanges.AddNoResize(new ChunkGroupRange { start = running, count  = count });
                        running                                                            += count;
                    }
                }

                // Assign chunk order using a backup copy
                var backup = new NativeArray<CapturedChunk>(chunks.AsArray(), Allocator.Temp);
                for (int i = 0; i < groupRanges.Length; i++)
                {
                    var set           = sets[i];
                    var dst           = chunkCounts[set];
                    var srcRange      = groupRanges[i];
                    chunkCounts[set] += srcRange.count;
                    for (int j = 0; j < srcRange.count; j++)
                    {
                        chunks[dst + j] = backup[srcRange.start + j];
                    }
                }

                // Copy ranges to output
                groupRanges.Clear();
                groupRanges.AddRangeNoResize(dstRanges.Ptr, dstRanges.Length);
            }

            /// <summary>
            /// Gets a ChunkGroups view of this builder for use in a final grouping job to be scheduled.
            /// This is usually used by codegen.
            /// </summary>
            public ChunkGroups AsDeferredChunkGroups() => new ChunkGroups
            {
                chunks      = chunks.AsDeferredJobArray(),
                groupRanges = groupRanges.AsDeferredJobArray(),
            };
            /// <summary>
            /// Gets a ChunkGroups view of this builder for use in a final grouping job to be run on the main thread.
            /// This is usually used by codegen.
            /// </summary>
            public ChunkGroups AsMainThreadChunkGroups() => new ChunkGroups
            {
                chunks      = chunks.AsArray(),
                groupRanges = groupRanges.AsArray(),
            };

            [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS"), Conditional("UNITY_DOTS_DEBUG")]
            static void CheckIndexInRange(int index, int length)
            {
                if (index < 0 || index >= length)
                    throw new System.ArgumentOutOfRangeException($"Captured chunk index {index} is out of range of the {length} captured chunks.");
            }
        }

        /// <summary>
        /// The read-side view of an IJobEach's finished chunk grouping. This is usually used by codegen.
        /// </summary>
        public struct ChunkGroups
        {
            /// <summary>
            /// The captured chunks, reordered by group
            /// </summary>
            [ReadOnly] public NativeArray<CapturedChunk> chunks;
            /// <summary>
            /// The range of chunks within each group
            /// </summary>
            [ReadOnly] public NativeArray<ChunkGroupRange> groupRanges;
        }

        /// <summary>
        /// A job to capture chunks from an EntityQuery. Must be scheduled single-threaded. This is usually used by codegen.
        /// </summary>
        [BurstCompile]
        public struct CaptureChunksJob : IJobChunk
        {
            /// <summary>
            /// The destination buffer to record chunks. Preallocate with EntityQuery.CalculateChunkCountWithoutFiltering().
            /// </summary>
            public NativeList<CapturedChunk> capturedChunks;
            /// <summary>
            /// The destination buffer for the initial chunk groups (1 chunk per group). Preallocate with capturedChunks.Capacity.
            /// </summary>
            public NativeList<ChunkGroupRange> chunkGroupRanges;
            /// <summary>
            /// If true, all captured chunks are placed into a single group instead.
            /// </summary>
            public bool forceSingleGroup;

            int prefixSum;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                var entityCount = InternalSourceGen.StaticAPI.CountEntitiesInChunk(in chunk, useEnabledMask, in chunkEnabledMask);
                capturedChunks.AddNoResize(new CapturedChunk
                {
                    chunk                   = chunk,
                    chunkEnabledMask        = chunkEnabledMask,
                    enabledEntityCount      = entityCount,
                    firstEntityIndexInQuery = prefixSum,
                    unfilteredChunkIndex    = unfilteredChunkIndex,
                    useEnabledMask          = useEnabledMask,
                });
                prefixSum += entityCount;
                if (!forceSingleGroup || chunkGroupRanges.IsEmpty)
                    chunkGroupRanges.AddNoResize(new ChunkGroupRange { start = chunkGroupRanges.Length, count = 1 });
                else
                    chunkGroupRanges.ElementAt(0).count++;
            }
        }
    }
}

