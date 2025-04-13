using System;
using Latios.Unsafe;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

using static Unity.Entities.SystemAPI;

namespace Latios.Calligraphics.Systems
{
    [RequireMatchingQueriesForUpdate]
    [DisableAutoCreation]
    [BurstCompile]
    public unsafe partial struct GenerateGlyphsSystem : ISystem
    {
        LatiosWorldUnmanaged latiosWorld;
        EntityQuery          m_query;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            latiosWorld = state.GetLatiosWorldUnmanaged();
            m_query     = state.Fluent().With<CalliByte, TextBaseConfiguration>(true).WithEnabled<CalliByteChangedFlag>(true).With<RenderGlyph>(false).Build();

            latiosWorld.worldBlackboardEntity.AddOrSetCollectionComponentAndDisposeOld(new GlyphTable
            {
                entries          = new NativeList<GlyphTable.Entry>(Allocator.Persistent),
                glyphHashToIdMap = new NativeHashMap<ulong, uint>(1024, Allocator.Persistent),
            });
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var fontTable                 = latiosWorld.worldBlackboardEntity.GetCollectionComponent<FontTable>(true);
            var glyphTable                = latiosWorld.worldBlackboardEntity.GetCollectionComponent<GlyphTable>(false);
            var shapeStream               = new NativeStream(m_query.CalculateChunkCountWithoutFiltering(), state.WorldUpdateAllocator);
            var newGlyphRequestsBlocklist = new UnsafeParallelBlockList(UnsafeUtility.SizeOf<MissingGlyph>(), 128, state.WorldUpdateAllocator);

            var jh = new ShapeJob
            {
                fontTable                 = fontTable,
                glyphTable                = glyphTable,
                configHandle              = GetComponentTypeHandle<TextBaseConfiguration>(true),
                calliByteHandle           = GetBufferTypeHandle<CalliByte>(true),
                shapeStream               = shapeStream.AsWriter(),
                newGlyphRequestsBlocklist = newGlyphRequestsBlocklist
            }.ScheduleParallel(m_query, state.Dependency);

            var missingGlyphsToAdd = new NativeList<MissingGlyphPtr>(state.WorldUpdateAllocator);

            jh = new AllocateNewGlyphsJob
            {
                fontTable                 = fontTable,
                glyphTable                = glyphTable,
                newGlyphRequestsBlocklist = newGlyphRequestsBlocklist,
                missingGlyphsToAdd        = missingGlyphsToAdd
            }.Schedule(jh);

            jh = new PopulateNewGlyphsJob
            {
                missingGlyphs = missingGlyphsToAdd.AsDeferredJobArray(),
                fontTable     = fontTable,
                glyphEntries  = glyphTable.entries.AsDeferredJobArray(),
            }.Schedule(missingGlyphsToAdd, 4, jh);

            state.Dependency = new GenerateRenderGlyphsJob
            {
                fontTable         = fontTable,
                glyphTable        = glyphTable,
                configHandle      = GetComponentTypeHandle<TextBaseConfiguration>(true),
                calliByteHandle   = GetBufferTypeHandle<CalliByte>(true),
                shapeStream       = shapeStream.AsReader(),
                renderGlyphHandle = GetBufferTypeHandle<RenderGlyph>(false)
            }.ScheduleParallel(m_query, jh);
        }

        struct MissingGlyph
        {
            // Todo: Identifying info
            public ulong        hash;
            public RenderFormat format;
        }

        struct MissingGlyphPtr : IComparable<MissingGlyphPtr>
        {
            public MissingGlyph* ptr;

            public int CompareTo(MissingGlyphPtr other)
            {
                return ptr->hash.CompareTo(other.ptr->hash);
            }
        }

        [BurstCompile]
        struct ShapeJob : IJobChunk
        {
            [ReadOnly] public FontTable                                  fontTable;
            [ReadOnly] public GlyphTable                                 glyphTable;
            [ReadOnly] public ComponentTypeHandle<TextBaseConfiguration> configHandle;
            [ReadOnly] public BufferTypeHandle<CalliByte>                calliByteHandle;
            public NativeStream.Writer                                   shapeStream;
            public UnsafeParallelBlockList                               newGlyphRequestsBlocklist;

            UnsafeHashSet<ulong> newGlyphsFoundSoFar;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                if (!newGlyphsFoundSoFar.IsCreated)
                    newGlyphsFoundSoFar = new UnsafeHashSet<ulong>(512, Allocator.Temp);

                shapeStream.BeginForEachIndex(unfilteredChunkIndex);

                var configs          = (TextBaseConfiguration*)chunk.GetRequiredComponentDataPtrRO(ref configHandle);
                var calliByteBuffers = chunk.GetBufferAccessor(ref calliByteHandle);

                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                while (enumerator.NextEntityIndex(out var entityIndex))
                {
                    var config      = configs[entityIndex];
                    var calliString = new CalliString(calliByteBuffers[entityIndex]);

                    // Todo: Shape
                }

                shapeStream.EndForEachIndex();
            }
        }

        [BurstCompile]
        struct AllocateNewGlyphsJob : IJob
        {
            [ReadOnly] public FontTable        fontTable;
            public GlyphTable                  glyphTable;
            public UnsafeParallelBlockList     newGlyphRequestsBlocklist;
            public NativeList<MissingGlyphPtr> missingGlyphsToAdd;

            public void Execute()
            {
                // Deduplicate
                var requestCount          = newGlyphRequestsBlocklist.Count();
                var uniqueMissingGlyphMap = new UnsafeHashMap<ulong, MissingGlyphPtr>(requestCount, Allocator.Temp);
                var enumerator            = newGlyphRequestsBlocklist.GetEnumerator();
                while (enumerator.MoveNext())
                {
                    var ptr                                                      = (MissingGlyph*)enumerator.GetCurrentPtr();
                    var hash                                                     = ptr->hash;
                    uniqueMissingGlyphMap.TryAdd(hash, new MissingGlyphPtr { ptr = ptr });
                }

                // Sort for determinism to improve debugging experience
                missingGlyphsToAdd.Capacity = uniqueMissingGlyphMap.Count;
                uint nextIndex              = (uint)glyphTable.glyphHashToIdMap.Count;
                foreach (var pair in uniqueMissingGlyphMap)
                {
                    missingGlyphsToAdd.AddNoResize(pair.Value);
                    var nextId = nextIndex;
                    Bits.SetBits(ref nextId, 30, 2, (uint)pair.Value.ptr->format);
                    glyphTable.glyphHashToIdMap.Add(pair.Value.ptr->hash, nextId);
                    nextIndex++;
                }
            }
        }

        [BurstCompile]
        struct PopulateNewGlyphsJob : IJobParallelForDefer
        {
            [ReadOnly] public NativeArray<MissingGlyphPtr>                             missingGlyphs;
            [ReadOnly] public FontTable                                                fontTable;
            [NativeDisableParallelForRestriction] public NativeArray<GlyphTable.Entry> glyphEntries;

            public void Execute(int i)
            {
                ref var missingGlyph = ref *missingGlyphs[i].ptr;

                var newEntry = new GlyphTable.Entry
                {
                    // Todo:
                };
                var baseIndex               = glyphEntries.Length - missingGlyphs.Length;
                glyphEntries[baseIndex + i] = newEntry;
            }
        }

        [BurstCompile]
        struct GenerateRenderGlyphsJob : IJobChunk
        {
            [ReadOnly] public FontTable                                  fontTable;
            [ReadOnly] public GlyphTable                                 glyphTable;
            [ReadOnly] public ComponentTypeHandle<TextBaseConfiguration> configHandle;
            [ReadOnly] public BufferTypeHandle<CalliByte>                calliByteHandle;
            [ReadOnly] public NativeStream.Reader                        shapeStream;
            public BufferTypeHandle<RenderGlyph>                         renderGlyphHandle;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                shapeStream.BeginForEachIndex(unfilteredChunkIndex);

                var configs            = (TextBaseConfiguration*)chunk.GetRequiredComponentDataPtrRO(ref configHandle);
                var calliByteBuffers   = chunk.GetBufferAccessor(ref calliByteHandle);
                var renderGlyphBuffers = chunk.GetBufferAccessor(ref renderGlyphHandle);

                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                while (enumerator.NextEntityIndex(out var entityIndex))
                {
                    var config       = configs[entityIndex];
                    var calliString  = new CalliString(calliByteBuffers[entityIndex]);
                    var renderGlyphs = renderGlyphBuffers[entityIndex];

                    // Todo: Generate glyphs
                }

                shapeStream.EndForEachIndex();
            }
        }
    }
}

