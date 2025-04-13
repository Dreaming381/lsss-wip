using System;
using Latios.Psyshock;
using Latios.Unsafe;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Rendering;

using static Unity.Entities.SystemAPI;

namespace Latios.Calligraphics.Systems
{
    [RequireMatchingQueriesForUpdate]
    [DisableAutoCreation]
    [BurstCompile]
    public unsafe partial struct UpdateGlyphsRenderersSystem : ISystem
    {
        LatiosWorldUnmanaged                    latiosWorld;
        EntityQuery                             m_query;
        EntityQuery                             m_newQuery;
        EntityQuery                             m_deadQuery;
        DoubleRewindableAllocators              newPreviousRenderGlyphsAllocator;
        NativeList<PreviousRenderGlyphsToApply> toApply;

        public void OnCreate(ref SystemState state)
        {
            latiosWorld                      = state.GetLatiosWorldUnmanaged();
            newPreviousRenderGlyphsAllocator = new DoubleRewindableAllocators(Allocator.Persistent, 256 * 256);
            m_query                          = state.Fluent().WithAnyEnabled<RenderGlyph, AnimatedRenderGlyph>(true).With<GpuState, MaterialMeshIndex, RenderBounds>(false).Build();
            m_query.AddChangedVersionFilter(ComponentType.ReadOnly<CalliByte>());
            m_newQuery =
                state.Fluent().WithAnyEnabled<RenderGlyph, AnimatedRenderGlyph>(true).With<GpuState, MaterialMeshIndex, RenderBounds>(false).Without<PreviousRenderGlyph>().Build();
            m_deadQuery = state.Fluent().With<PreviousRenderGlyph>(true).Without<RenderGlyph, AnimatedRenderGlyph>().Build();
        }

        public void OnDestroy(ref SystemState state)
        {
            state.CompleteDependency();
            newPreviousRenderGlyphsAllocator.Dispose();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            newPreviousRenderGlyphsAllocator.Update();

            var refCountChangeBlocklist       = new UnsafeParallelBlockList(UnsafeUtility.SizeOf<RefCountChange>(), 256, state.WorldUpdateAllocator);
            var residentDeallocationBlocklist = new UnsafeParallelBlockList(UnsafeUtility.SizeOf<ResidentRange>(), 256, state.WorldUpdateAllocator);

            if (!m_deadQuery.IsEmptyIgnoreFilter)
            {
                state.Dependency = new RecordDeadJob
                {
                    rangeHandle                   = GetComponentTypeHandle<ResidentRange>(true),
                    previousRenderGlyphsHandle    = GetBufferTypeHandle<PreviousRenderGlyph>(true),
                    refCountChangeBlocklist       = refCountChangeBlocklist,
                    residentDeallocationBlocklist = residentDeallocationBlocklist
                }.ScheduleParallel(m_deadQuery, state.Dependency);
            }

            if (toApply.IsCreated && !toApply.IsEmpty)
            {
                state.Dependency = new PatchPreviousJob
                {
                    toApply                 = toApply.AsArray(),
                    previousLookup          = GetBufferLookup<PreviousRenderGlyph>(false),
                    refCountChangeBlocklist = refCountChangeBlocklist,
                }.ScheduleParallel(toApply.Length, 1, state.Dependency);
            }

            var allocator = newPreviousRenderGlyphsAllocator.Allocator.Handle;
            var newCount  = m_newQuery.CalculateChunkCountWithoutFiltering();
            toApply       = new NativeList<PreviousRenderGlyphsToApply>(newCount, allocator);

            state.Dependency = new UpdateChangedGlyphsJob
            {
                animatedRenderGlyphHandle     = GetBufferTypeHandle<AnimatedRenderGlyph>(true),
                entityHandle                  = GetEntityTypeHandle(),
                gpuStateHandle                = GetComponentTypeHandle<GpuState>(false),
                lastSystemVersion             = state.LastSystemVersion,
                materialMeshInfoHandle        = GetComponentTypeHandle<MaterialMeshInfo>(false),
                newBufferAllocator            = allocator,
                newBuffersList                = toApply.AsParallelWriter(),
                previousRenderGlyphHandle     = GetBufferTypeHandle<PreviousRenderGlyph>(false),
                refCountChangeBlocklist       = refCountChangeBlocklist,
                renderBoundsHandle            = GetComponentTypeHandle<RenderBounds>(false),
                renderGlyphHandle             = GetBufferTypeHandle<RenderGlyph>(true),
                residentDeallocationBlocklist = residentDeallocationBlocklist,
                residentRangeHandle           = GetComponentTypeHandle<ResidentRange>(false)
            }.ScheduleParallel(m_query, state.Dependency);

            var atlasTable = latiosWorld.worldBlackboardEntity.GetCollectionComponent<AtlasTable>(false);
            var glyphTable = latiosWorld.worldBlackboardEntity.GetCollectionComponent<GlyphTable>(false);
            var gpuTable   = latiosWorld.worldBlackboardEntity.GetCollectionComponent<GlyphGpuTable>(false);

            var jhA = new ApplyRefCountDeltasToGlyphTableJob
            {
                atlasTable              = atlasTable,
                glyphTable              = glyphTable,
                refCountChangeBlocklist = refCountChangeBlocklist
            }.Schedule(state.Dependency);

            var jhB = new DeallocateResidentsJob
            {
                gpuTable                      = gpuTable,
                residentDeallocationBlocklist = residentDeallocationBlocklist
            }.Schedule(state.Dependency);

            state.Dependency = JobHandle.CombineDependencies(jhA, jhB);
        }

        struct PreviousRenderGlyphsToApply
        {
            public RenderGlyph* ptr;
            public Entity       target;
            public int          glyphCount;
        }

        struct RefCountChange
        {
            public uint glyphEntryId;
            public int  refCountDelta;
        }

        struct RefCountChangePtr
        {
            public RefCountChange* ptr;
        }

        [BurstCompile]
        struct RecordDeadJob : IJobChunk
        {
            [ReadOnly] public ComponentTypeHandle<ResidentRange>    rangeHandle;
            [ReadOnly] public BufferTypeHandle<PreviousRenderGlyph> previousRenderGlyphsHandle;
            public UnsafeParallelBlockList                          residentDeallocationBlocklist;
            public UnsafeParallelBlockList                          refCountChangeBlocklist;

            [NativeSetThreadIndex] int             threadIndex;
            UnsafeHashMap<uint, RefCountChangePtr> threadRefCountChangeMap;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                if (!threadRefCountChangeMap.IsCreated)
                    threadRefCountChangeMap = new UnsafeHashMap<uint, RefCountChangePtr>(1024, Allocator.Temp);

                var ranges = (ResidentRange*)chunk.GetRequiredComponentDataPtrRO(ref rangeHandle);
                for (int i = 0; i < chunk.Count; i++)
                {
                    if (ranges[i].count > 0)
                        residentDeallocationBlocklist.Write(ranges[i], threadIndex);
                }
                var glyphsBuffers = chunk.GetBufferAccessor(ref previousRenderGlyphsHandle);
                for (int index = 0; index < chunk.Count; index++)
                {
                    var glyphs = glyphsBuffers[index].Reinterpret<RenderGlyph>().AsNativeArray();
                    for (int i = 0; i < glyphs.Length; i++)
                    {
                        var id = glyphs[i].glyphEntryId;
                        if (threadRefCountChangeMap.TryGetValue(id, out var ptr))
                        {
                            ptr.ptr->refCountDelta--;
                        }
                        else
                        {
                            var newPtr = new RefCountChangePtr { ptr = (RefCountChange*)refCountChangeBlocklist.Allocate(threadIndex) };
                            newPtr.ptr->glyphEntryId                 = id;
                            newPtr.ptr->refCountDelta                = -1;
                            threadRefCountChangeMap.Add(id, newPtr);
                        }
                    }
                }
            }
        }

        [BurstCompile]
        struct PatchPreviousJob : IJobFor
        {
            public NativeArray<PreviousRenderGlyphsToApply>                                toApply;
            [NativeDisableParallelForRestriction] public BufferLookup<PreviousRenderGlyph> previousLookup;
            public UnsafeParallelBlockList                                                 refCountChangeBlocklist;

            [NativeSetThreadIndex] int             threadIndex;
            UnsafeHashMap<uint, RefCountChangePtr> threadRefCountChangeMap;

            public void Execute(int index)
            {
                var element = toApply[index];
                if (previousLookup.TryGetBuffer(element.target, out var buffer))
                {
                    buffer.ResizeUninitialized(element.glyphCount);
                    UnsafeUtility.MemCpy(buffer.GetUnsafePtr(), element.ptr, element.glyphCount * UnsafeUtility.SizeOf<RenderGlyph>());
                }
                else
                {
                    // The entity died after one frame. That wasn't enough time for it to become resident,
                    // so we only care about glyph ref counts.
                    var glyphs = element.ptr;
                    for (int i = 0; i < element.glyphCount; i++)
                    {
                        var id = glyphs[i].glyphEntryId;
                        if (threadRefCountChangeMap.TryGetValue(id, out var ptr))
                        {
                            ptr.ptr->refCountDelta--;
                        }
                        else
                        {
                            var newPtr = new RefCountChangePtr { ptr = (RefCountChange*)refCountChangeBlocklist.Allocate(threadIndex) };
                            newPtr.ptr->glyphEntryId                 = id;
                            newPtr.ptr->refCountDelta                = -1;
                            threadRefCountChangeMap.Add(id, newPtr);
                        }
                    }

                    if (!threadRefCountChangeMap.IsCreated)
                        threadRefCountChangeMap = new UnsafeHashMap<uint, RefCountChangePtr>(1024, Allocator.Temp);
                }
            }
        }

        [BurstCompile]
        struct UpdateChangedGlyphsJob : IJobChunk
        {
            [ReadOnly] public EntityTypeHandle                            entityHandle;
            [ReadOnly] public BufferTypeHandle<RenderGlyph>               renderGlyphHandle;
            [ReadOnly] public BufferTypeHandle<AnimatedRenderGlyph>       animatedRenderGlyphHandle;
            public BufferTypeHandle<PreviousRenderGlyph>                  previousRenderGlyphHandle;
            public ComponentTypeHandle<GpuState>                          gpuStateHandle;
            public ComponentTypeHandle<ResidentRange>                     residentRangeHandle;
            public ComponentTypeHandle<MaterialMeshInfo>                  materialMeshInfoHandle;
            public ComponentTypeHandle<RenderBounds>                      renderBoundsHandle;
            public UnsafeParallelBlockList                                refCountChangeBlocklist;
            public UnsafeParallelBlockList                                residentDeallocationBlocklist;
            public NativeList<PreviousRenderGlyphsToApply>.ParallelWriter newBuffersList;

            public AllocatorManager.AllocatorHandle newBufferAllocator;
            public uint                             lastSystemVersion;

            [NativeSetThreadIndex] int             threadIndex;
            UnsafeHashMap<uint, RefCountChangePtr> threadRefCountChangeMap;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                if (!threadRefCountChangeMap.IsCreated)
                    threadRefCountChangeMap = new UnsafeHashMap<uint, RefCountChangePtr>(1024, Allocator.Temp);

                var animatedGlyphBuffers       = chunk.GetBufferAccessor(ref animatedRenderGlyphHandle);
                var glyphBuffers               = animatedGlyphBuffers.Length > 0 ? chunk.GetBufferAccessor(ref renderGlyphHandle) : default;
                var previousRenderGlyphBuffers = chunk.GetBufferAccessor(ref previousRenderGlyphHandle);

                var gpuStates = (GpuState*)chunk.GetRequiredComponentDataPtrRW(ref gpuStateHandle);

                if (previousRenderGlyphBuffers.Length == 0)
                {
                    // These are new glyphs
                    chunk.SetComponentEnabledForAll(ref gpuStateHandle, true);
                    var entities     = chunk.GetEntityDataPtrRO(entityHandle);
                    var mmis         = (MaterialMeshInfo*)chunk.GetRequiredComponentDataPtrRW(ref materialMeshInfoHandle);
                    var renderBounds = (RenderBounds*)chunk.GetRequiredComponentDataPtrRW(ref renderBoundsHandle);
                    for (int i = 0; i < chunk.Count; i++)
                    {
                        var glyphs = animatedGlyphBuffers.Length > 0 ? animatedGlyphBuffers[i].Reinterpret<RenderGlyph>() : glyphBuffers[i];
                        if (!glyphs.IsEmpty)
                        {
                            var newToAdd = new PreviousRenderGlyphsToApply
                            {
                                glyphCount = glyphs.Length,
                                target     = entities[i],
                                ptr        = AllocatorManager.Allocate<RenderGlyph>(newBufferAllocator, glyphs.Length)
                            };
                            UnsafeUtility.MemCpy(newToAdd.ptr, glyphs.GetUnsafeReadOnlyPtr(), glyphs.Length * UnsafeUtility.SizeOf<RenderGlyph>());
                            newBuffersList.AddNoResize(newToAdd);
                        }
                        gpuStates[i].state = GpuState.State.Uncommitted;
                        UpdateRefCounts(glyphs.AsNativeArray().AsReadOnlySpan(), 1);
                        UpdateBaseRenderingData(ref renderBounds[i], ref mmis[i], glyphs.AsNativeArray().AsReadOnlySpan());
                    }

                    return;
                }

                // These entities were encountered last frame
                chunk.SetComponentEnabledForAll(ref gpuStateHandle, false);
                var gpuStateMask = chunk.GetEnabledMask(ref gpuStateHandle);

                bool glyphsChanged = animatedGlyphBuffers.Length > 0 ? chunk.DidChange(ref animatedRenderGlyphHandle, lastSystemVersion) : chunk.DidChange(ref renderGlyphHandle,
                                                                                                                                                           lastSystemVersion);
                if (!glyphsChanged)
                {
                    // Nothing changed. Promote any dynamic glyph buffers to resident and exit.
                    for (int i = 0; i < chunk.Count; i++)
                    {
                        if (gpuStates[i].state == GpuState.State.Dynamic)
                        {
                            gpuStates[i].state = GpuState.State.DynamicPromoteToResident;
                            gpuStateMask[i]    = true;
                        }
                    }
                    return;
                }

                // At this point, we are dealing with potentially changed glyphs on entities we have seen before.
                var residentRanges = previousRenderGlyphBuffers.Length > 0 ? chunk.GetComponentDataPtrRW(ref residentRangeHandle) : null;

                {
                    var mmis         = (MaterialMeshInfo*)chunk.GetRequiredComponentDataPtrRW(ref materialMeshInfoHandle);
                    var renderBounds = (RenderBounds*)chunk.GetRequiredComponentDataPtrRW(ref renderBoundsHandle);

                    for (int i = 0; i < chunk.Count; i++)
                    {
                        var glyphs         = animatedGlyphBuffers.Length > 0 ? animatedGlyphBuffers[i].Reinterpret<RenderGlyph>() : glyphBuffers[i];
                        var previousGlyphs = previousRenderGlyphBuffers[i];
                        if (glyphs.Length == previousGlyphs.Length &&
                            (glyphs.Length == 0 ||
                             UnsafeUtility.MemCmp(glyphs.GetUnsafeReadOnlyPtr(), previousGlyphs.GetUnsafeReadOnlyPtr(), glyphs.Length * UnsafeUtility.SizeOf<RenderGlyph>()) == 0))
                        {
                            // Nothing changed. Promote dynamic to resident if necessary.
                            if (gpuStates[i].state == GpuState.State.Dynamic)
                            {
                                gpuStates[i].state = GpuState.State.DynamicPromoteToResident;
                                gpuStateMask[i]    = true;
                            }
                            continue;
                        }

                        // Something changed. Figure out if we need to deallocate or just do an update.
                        if (glyphs.Length != previousGlyphs.Length && gpuStates[i].state == GpuState.State.Resident)
                        {
                            // We need to deallocate.
                            residentDeallocationBlocklist.Write(residentRanges[i], threadIndex);
                            residentRanges[i] = default;
                        }
                        // Reset the state, update ref counts, and copy previousGlyphs
                        gpuStates[i].state = GpuState.State.Uncommitted;
                        gpuStateMask[i]    = true;
                        UpdateRefCounts(previousGlyphs.Reinterpret<RenderGlyph>().AsNativeArray().AsReadOnlySpan(), -1);
                        UpdateRefCounts(glyphs.AsNativeArray().AsReadOnlySpan(),                                    1);
                        previousGlyphs.Clear();
                        previousGlyphs.AddRange(glyphs.Reinterpret<PreviousRenderGlyph>().AsNativeArray());
                        UpdateBaseRenderingData(ref renderBounds[i], ref mmis[i], glyphs.AsNativeArray().AsReadOnlySpan());
                    }
                }
            }

            void UpdateRefCounts(ReadOnlySpan<RenderGlyph> glyphs, int delta)
            {
                for (int i = 0; i < glyphs.Length; i++)
                {
                    var id = glyphs[i].glyphEntryId;
                    if (threadRefCountChangeMap.TryGetValue(id, out var ptr))
                    {
                        ptr.ptr->refCountDelta += delta;
                    }
                    else
                    {
                        var newPtr = new RefCountChangePtr { ptr = (RefCountChange*)refCountChangeBlocklist.Allocate(threadIndex) };
                        newPtr.ptr->glyphEntryId                 = id;
                        newPtr.ptr->refCountDelta                = delta;
                        threadRefCountChangeMap.Add(id, newPtr);
                    }
                }
            }

            void UpdateBaseRenderingData(ref RenderBounds bounds, ref MaterialMeshInfo mmi, in ReadOnlySpan<RenderGlyph> glyphs)
            {
                float4 min = float.MaxValue;
                float4 max = float.MinValue;
                for (int i = 0; i < glyphs.Length; i++)
                {
                    var glyph  = glyphs[i];
                    var bottom = new float4(glyph.blPosition, glyph.brPosition);
                    var top    = new float4(glyph.tlPosition, glyph.trPosition);
                    min        = math.min(min, math.min(top, bottom));
                    max        = math.max(max, math.max(top, bottom));
                }
                var aabb = new Aabb(new float3(math.min(min.xy, min.zw), 0f), new float3(math.max(max.xy, max.zw), 0f));

                Physics.GetCenterExtents(aabb, out var center, out var extents);
                if (glyphs.Length == 0)
                {
                    center  = 0f;
                    extents = 0f;
                }
                bounds = new RenderBounds { Value = new AABB { Center = center, Extents = extents } };;

                if (glyphs.Length <= 8)
                    mmi.SubMesh = 0;
                else if (glyphs.Length <= 64)
                    mmi.SubMesh = 1;
                else if (glyphs.Length <= 512)
                    mmi.SubMesh = 2;
                else if (glyphs.Length <= 4096)
                    mmi.SubMesh = 3;
                else if (glyphs.Length <= 16384)
                    mmi.SubMesh = 4;
                else
                {
                    UnityEngine.Debug.LogWarning("Glyphs in RenderGlyph buffer exceeds max capacity of 16384 and will be truncated.");
                    mmi.SubMesh = 4;
                }
            }
        }

        [BurstCompile]
        struct ApplyRefCountDeltasToGlyphTableJob : IJob
        {
            public UnsafeParallelBlockList refCountChangeBlocklist;
            public GlyphTable              glyphTable;
            public AtlasTable              atlasTable;

            public void Execute()
            {
                var count                  = refCountChangeBlocklist.Count();
                var atlasRemovalCandidates = new UnsafeHashSet<uint>(count, Allocator.Temp);
                var enumerator             = refCountChangeBlocklist.GetEnumerator();
                while (enumerator.MoveNext())
                {
                    var     delta     = enumerator.GetCurrentAsRef<RefCountChange>();
                    ref var entry     = ref glyphTable.GetEntryRW(delta.glyphEntryId);
                    var     oldCount  = delta.refCountDelta;
                    entry.refCount   += delta.refCountDelta;
                    if (entry.isInAtlas)
                    {
                        // There can be duplicate entry IDs. So it is possible we decrease the ref count to 0, only to increase it again.
                        // Rather than preduplicate them, we add and remove from a hashset. We only consider entries in the atlas that
                        // had a zero-nonzero change, which makes the set smaller and requires far fewer random accesses.
                        bool wasEmpty = oldCount <= 0;
                        bool isEmpty  = entry.refCount <= 0;
                        if (wasEmpty && !isEmpty)
                            atlasRemovalCandidates.Remove(delta.glyphEntryId);
                        else if (!wasEmpty && isEmpty)
                            atlasRemovalCandidates.Add(delta.glyphEntryId);
                    }
                }

                // We know for sure that these entry IDs are no longer referenced. Therefore, we can actually remove them.
                var entriesToRemove = atlasRemovalCandidates.ToNativeArray(Allocator.Temp);
                entriesToRemove.Sort();  // Determinism for debugging
                foreach (var id in entriesToRemove)
                {
                    ref var entry = ref glyphTable.GetEntryRW(id);
                    atlasTable.Free(id, entry.width, entry.height, entry.x, entry.y, entry.z);
                    entry.x = -1;
                    entry.y = -1;
                    entry.z = -1;
                }
            }
        }

        [BurstCompile]
        struct DeallocateResidentsJob : IJob
        {
            public UnsafeParallelBlockList residentDeallocationBlocklist;
            public GlyphGpuTable           gpuTable;

            public void Execute()
            {
                gpuTable.residentGaps.AddRange(gpuTable.dispatchDynamicGaps.AsArray());
                gpuTable.dispatchDynamicGaps.Clear();
                var enumerator = residentDeallocationBlocklist.GetEnumerator();
                while (enumerator.MoveNext())
                {
                    var range = enumerator.GetCurrentAsRef<ResidentRange>();
                    gpuTable.residentGaps.Add(new uint2(range.start, range.count));
                }

                var totals            = gpuTable.totals.Value;
                totals.resident       = GapAllocator.CoellesceGaps(gpuTable.residentGaps, totals.resident);
                gpuTable.totals.Value = totals;
            }
        }
    }
}

