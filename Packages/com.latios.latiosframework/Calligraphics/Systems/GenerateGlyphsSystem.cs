using System;
using HarfbuzzUnity;
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
                glyphHashToIdMap = new NativeHashMap<GlyphTable.Key, uint>(1024, Allocator.Persistent),
            });
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var fontTable                 = latiosWorld.worldBlackboardEntity.GetCollectionComponent<FontTable>(true);
            var glyphTable                = latiosWorld.worldBlackboardEntity.GetCollectionComponent<GlyphTable>(false);
            var chunkCount                = m_query.CalculateChunkCountWithoutFiltering();
            var shapeStream               = new ShapeStream(chunkCount, state.WorldUpdateAllocator);
            var newGlyphRequestsBlocklist = new UnsafeParallelBlockList(UnsafeUtility.SizeOf<MissingGlyph>(), 128, state.WorldUpdateAllocator);

            var jh = new ShapeJob
            {
                fontTable                 = fontTable,
                glyphTable                = glyphTable,
                configHandle              = GetComponentTypeHandle<TextBaseConfiguration>(true),
                calliByteHandle           = GetBufferTypeHandle<CalliByte>(true),
                shapeStream               = shapeStream.AsWriter(),
                newGlyphRequestsBlocklist = newGlyphRequestsBlocklist,
            }.ScheduleParallel(m_query, state.Dependency);

            var missingGlyphsToAdd = new NativeList<MissingGlyphPtr>(state.WorldUpdateAllocator);

            jh = new AllocateNewGlyphsJob
            {
                fontTable                 = fontTable,
                glyphTable                = glyphTable,
                newGlyphRequestsBlocklist = newGlyphRequestsBlocklist,
                missingGlyphsToAdd        = missingGlyphsToAdd,
            }.Schedule(jh);

            jh = new PopulateNewGlyphsJob
            {
                missingGlyphs = missingGlyphsToAdd.AsDeferredJobArray(),
                fontTable     = fontTable,
                glyphEntries  = glyphTable.entries.AsDeferredJobArray(),
            }.Schedule(missingGlyphsToAdd, 4, jh);

            state.Dependency = new GenerateRenderGlyphsJob
            {
                glyphTable                                 = glyphTable,
                configHandle                               = GetComponentTypeHandle<TextBaseConfiguration>(true),
                calliByteHandle                            = GetBufferTypeHandle<CalliByte>(true),
                shapeStream                                = shapeStream.AsReader(),
                renderGlyphHandle                          = GetBufferTypeHandle<RenderGlyph>(false),
                colorGradientCollectionBlobReferenceHandle = GetComponentTypeHandle<TextColorGradientCollectionBlobReference>(true),
                glyphMappingHandle                         = GetBufferTypeHandle<GlyphMappingElement>(false)
            }.ScheduleParallel(chunkCount, 1, jh);
        }

        struct MissingGlyph
        {
            public GlyphTable.Key glyphKey;
        }

        struct MissingGlyphPtr : IComparable<MissingGlyphPtr>
        {
            public MissingGlyph* ptr;

            public int CompareTo(MissingGlyphPtr other)
            {
                return ptr->glyphKey.CompareTo(other.ptr->glyphKey);
            }
        }

        [BurstCompile]
        struct ShapeJob : IJobChunk
        {
            [ReadOnly] public FontTable                                  fontTable;
            [ReadOnly] public GlyphTable                                 glyphTable;
            [ReadOnly] public ComponentTypeHandle<TextBaseConfiguration> configHandle;
            [ReadOnly] public BufferTypeHandle<CalliByte>                calliByteHandle;
            public ShapeStream.Writer                                    shapeStream;
            public UnsafeParallelBlockList                               newGlyphRequestsBlocklist;

            UnsafeHashSet<ulong> newGlyphsFoundSoFar;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                if (!newGlyphsFoundSoFar.IsCreated)
                    newGlyphsFoundSoFar = new UnsafeHashSet<ulong>(512, Allocator.Temp);

                shapeStream.BeginChunk(in chunk, unfilteredChunkIndex, useEnabledMask, chunkEnabledMask);

                var configs          = (TextBaseConfiguration*)chunk.GetRequiredComponentDataPtrRO(ref configHandle);
                var calliByteBuffers = chunk.GetBufferAccessor(ref calliByteHandle);

                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                while (enumerator.NextEntityIndex(out var entityIndex))
                {
                    var config      = configs[entityIndex];
                    var calliString = new CalliString(calliByteBuffers[entityIndex]);

                    // Todo: Shape
                }

                shapeStream.EndChunk();
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
                var uniqueMissingGlyphMap = new UnsafeHashMap<GlyphTable.Key, MissingGlyphPtr>(requestCount, Allocator.Temp);
                var enumerator            = newGlyphRequestsBlocklist.GetEnumerator();
                while (enumerator.MoveNext())
                {
                    var ptr                                                      = (MissingGlyph*)enumerator.GetCurrentPtr();
                    var hash                                                     = ptr->glyphKey;
                    uniqueMissingGlyphMap.TryAdd(hash, new MissingGlyphPtr { ptr = ptr });
                }

                // Sort for determinism to improve debugging experience
                missingGlyphsToAdd.Capacity = uniqueMissingGlyphMap.Count;
                uint nextIndex              = (uint)glyphTable.glyphHashToIdMap.Count;
                foreach (var pair in uniqueMissingGlyphMap)
                {
                    missingGlyphsToAdd.AddNoResize(pair.Value);
                    var nextId = nextIndex;
                    Bits.SetBits(ref nextId, 30, 2, (uint)pair.Value.ptr->glyphKey.format);
                    glyphTable.glyphHashToIdMap.Add(pair.Value.ptr->glyphKey, nextId);
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

            [NativeSetThreadIndex]
            int threadIndex;

            GlyphTable.Key lastKey;
            IntPtr         lastFont;
            bool           initialized;

            public void Execute(int i)
            {
                ref var missingGlyph = ref *missingGlyphs[i].ptr;
                var     font         = lastFont;

                if (!initialized || RequiresFontSetup(lastKey, missingGlyph.glyphKey))
                {
                    font             = fontTable.GetOrCreateFont(missingGlyph.glyphKey.faceIndex, threadIndex);
                    var samplingSize = missingGlyph.glyphKey.textureSize.GetSamplingSize();
                    Harfbuzz.hb_font_set_scale(font, samplingSize, samplingSize);
                }

                Harfbuzz.hb_font_get_glyph_extents(font, missingGlyph.glyphKey.glyphIndex, out var extents);

                var newEntry = new GlyphTable.Entry
                {
                    key      = missingGlyph.glyphKey,
                    refCount = 0,
                    x        = -1,
                    y        = -1,
                    z        = -1,
                    width    = (short)extents.width,
                    height   = (short)(-extents.height),  // For legacy reasons, Harfbuzz returns height as negative.
                    xBearing = (short)extents.x_bearing,
                    yBearing = (short)extents.y_bearing  // Harfbuzz is y-up
                };
                var baseIndex               = glyphEntries.Length - missingGlyphs.Length;
                glyphEntries[baseIndex + i] = newEntry;
            }

            bool RequiresFontSetup(GlyphTable.Key lastKey, GlyphTable.Key thisKey)
            {
                var a = lastKey.packed & 0xffffffffffff0000;
                var b = thisKey.packed & 0xffffffffffff0000;
                return a != b;
            }
        }

        [BurstCompile]
        struct GenerateRenderGlyphsJob : IJobFor
        {
            [ReadOnly] public GlyphTable                                                    glyphTable;
            [ReadOnly] public ComponentTypeHandle<TextBaseConfiguration>                    configHandle;
            [ReadOnly] public BufferTypeHandle<CalliByte>                                   calliByteHandle;
            [ReadOnly] public ComponentTypeHandle<TextColorGradientCollectionBlobReference> colorGradientCollectionBlobReferenceHandle;
            [ReadOnly] public ShapeStream.Reader                                            shapeStream;
            public BufferTypeHandle<RenderGlyph>                                            renderGlyphHandle;
            public BufferTypeHandle<GlyphMappingElement>                                    glyphMappingHandle;

            GlyphMappingWriter glyphMappingWriter;

            public void Execute(int unfilteredChunkIndex)
            {
                shapeStream.BeginChunk(unfilteredChunkIndex, out var chunk, out var entityCount);

                var configs            = (TextBaseConfiguration*)chunk.GetRequiredComponentDataPtrRO(ref configHandle);
                var calliByteBuffers   = chunk.GetBufferAccessor(ref calliByteHandle);
                var renderGlyphBuffers = chunk.GetBufferAccessor(ref renderGlyphHandle);
                var mappingBuffers     = chunk.GetBufferAccessor(ref glyphMappingHandle);
                var gradients          = chunk.GetComponentDataPtrRO(ref colorGradientCollectionBlobReferenceHandle);

                for (int i = 0; i < entityCount; i++)
                {
                    var entityIndex  = shapeStream.PeakEntity().entityIndexInChunk;
                    var config       = configs[entityIndex];
                    var renderGlyphs = renderGlyphBuffers[entityIndex];

                    // Todo: Initialize correct mask.
                    glyphMappingWriter.StartWriter(GlyphMappingMask.WriteMask.None);
                    var gradient = gradients != null ? gradients[entityIndex].blob.Value.gradients.AsSpan() : default;
                    GlyphGeneration.CreateRenderGlyphs(ref shapeStream, ref renderGlyphs, ref glyphMappingWriter, calliByteBuffers[entityIndex], in config, gradient, glyphTable);
                    if (mappingBuffers.Length > 0)
                    {
                        var mapping = mappingBuffers[entityIndex];
                        glyphMappingWriter.EndWriter(ref mapping, renderGlyphs.Length);
                    }
                }

                shapeStream.EndChunk();
            }
        }
    }
}

