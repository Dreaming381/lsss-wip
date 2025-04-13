using Latios.Unsafe;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

using static Unity.Entities.SystemAPI;

// Note: This system uses a custom DoubleRewindableAllocators, because it needs the allocations
// to survive for two whole updates.

namespace Latios.Calligraphics.Systems
{
    [RequireMatchingQueriesForUpdate]
    [DisableAutoCreation]
    [BurstCompile]
    public unsafe partial struct TextPrepassSystem : ISystem
    {
        LatiosWorldUnmanaged                  latiosWorld;
        EntityQuery                           m_query;
        EntityQuery                           m_newQuery;
        EntityQuery                           m_deadQuery;
        DoubleRewindableAllocators            newPreviousCalliBytesAllocator;
        NativeList<PreviousCalliBytesToApply> toApply;

        public void OnCreate(ref SystemState state)
        {
            latiosWorld                    = state.GetLatiosWorldUnmanaged();
            newPreviousCalliBytesAllocator = new DoubleRewindableAllocators(Allocator.Persistent, 256 * 256);
            m_query                        = state.Fluent().With<CalliByte, TextBaseConfiguration>(true).With<CalliByteChangedFlag>(false).Build();
            m_query.AddChangedVersionFilter(ComponentType.ReadOnly<CalliByte>());
            m_newQuery  = state.Fluent().With<CalliByte, TextBaseConfiguration, CalliByteChangedFlag>(true).Without<PreviousCalliByte>().Build();
            m_deadQuery = state.Fluent().With<PreviousCalliByte>(true).Without<CalliByte>().Build();
        }

        public void OnDestroy(ref SystemState state)
        {
            state.CompleteDependency();
            newPreviousCalliBytesAllocator.Dispose();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            newPreviousCalliBytesAllocator.Update();

            if (!m_deadQuery.IsEmptyIgnoreFilter)
            {
                state.Dependency = new RecordDeadJob
                {
                    ecb          = latiosWorld.syncPoint.CreateEntityCommandBuffer().AsParallelWriter(),
                    entityHandle = GetEntityTypeHandle()
                }.ScheduleParallel(m_deadQuery, state.Dependency);
            }

            if (toApply.IsCreated && !toApply.IsEmpty)
            {
                state.Dependency = new PatchPreviousJob
                {
                    toApply = toApply.AsArray(),
                    lookup  = GetBufferLookup<PreviousCalliByte>(false)
                }.ScheduleParallel(toApply.Length, 1, state.Dependency);
            }

            var allocator = newPreviousCalliBytesAllocator.Allocator.Handle;
            var newCount  = m_newQuery.CalculateChunkCountWithoutFiltering();
            toApply       = new NativeList<PreviousCalliBytesToApply>(newCount, allocator);

            var accb         = latiosWorld.syncPoint.CreateAddComponentsCommandBuffer(AddComponentsDestroyedEntityResolution.DropData);
            state.Dependency = new PrepassJob
            {
                accb                       = accb.AsParallelWriter(),
                calliByteChangedFlagHandle = GetComponentTypeHandle<CalliByteChangedFlag>(false),
                calliByteHandle            = GetBufferTypeHandle<CalliByte>(true),
                entityHandle               = GetEntityTypeHandle(),
                lastSystemVersion          = state.LastSystemVersion,
                newBufferAllocator         = allocator,
                newBuffersList             = toApply.AsParallelWriter(),
                previousCalliByteHandle    = GetBufferTypeHandle<PreviousCalliByte>(false)
            }.ScheduleParallel(m_query, state.Dependency);
        }

        struct PreviousCalliBytesToApply
        {
            public byte*  ptr;
            public Entity target;
            public int    byteCount;
        }

        [BurstCompile]
        struct RecordDeadJob : IJobChunk
        {
            [ReadOnly] public EntityTypeHandle        entityHandle;
            public EntityCommandBuffer.ParallelWriter ecb;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                var entities = chunk.GetNativeArray(entityHandle);
                ecb.RemoveComponent(unfilteredChunkIndex, entities, new TypePack<PreviousCalliByte, PreviousRenderGlyph, ResidentRange>());
            }
        }

        [BurstCompile]
        struct PatchPreviousJob : IJobFor
        {
            public NativeArray<PreviousCalliBytesToApply>                                toApply;
            [NativeDisableParallelForRestriction] public BufferLookup<PreviousCalliByte> lookup;

            public void Execute(int i)
            {
                var element = toApply[i];
                if (lookup.TryGetBuffer(element.target, out var buffer))
                {
                    buffer.ResizeUninitialized(element.byteCount);
                    UnsafeUtility.MemCpy(buffer.GetUnsafePtr(), element.ptr, element.byteCount);
                }
            }
        }

        [BurstCompile]
        struct PrepassJob : IJobChunk
        {
            [ReadOnly] public EntityTypeHandle                           entityHandle;
            [ReadOnly] public BufferTypeHandle<CalliByte>                calliByteHandle;
            [ReadOnly] public ComponentTypeHandle<TextBaseConfiguration> textConfigHandle;
            public BufferTypeHandle<PreviousCalliByte>                   previousCalliByteHandle;
            public ComponentTypeHandle<CalliByteChangedFlag>             calliByteChangedFlagHandle;
            public NativeList<PreviousCalliBytesToApply>.ParallelWriter  newBuffersList;
            public AddComponentsCommandBuffer.ParallelWriter             accb;

            public AllocatorManager.AllocatorHandle newBufferAllocator;
            public uint                             lastSystemVersion;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                if (!chunk.DidChange(ref calliByteHandle, lastSystemVersion))
                {
                    chunk.SetComponentEnabledForAll(ref calliByteChangedFlagHandle, false);
                    return;
                }

                Entity*     entities        = default;
                EnabledMask changeFlags     = default;
                var         configs         = chunk.GetRequiredComponentDataPtrRO(ref textConfigHandle);
                var         currentBuffers  = chunk.GetBufferAccessor(ref calliByteHandle);
                var         previousBuffers = chunk.GetBufferAccessor(ref previousCalliByteHandle);
                bool        areNewBuffers   = previousBuffers.Length == 0;
                if (areNewBuffers)
                    chunk.SetComponentEnabledForAll(ref calliByteChangedFlagHandle, true);
                else
                {
                    changeFlags = chunk.GetEnabledMask(ref calliByteChangedFlagHandle);
                    entities    = chunk.GetEntityDataPtrRO(entityHandle);
                }

                for (int i = 0; i < chunk.Count; i++)
                {
                    var current = currentBuffers[i];
                    if (areNewBuffers)
                    {
                        if (!current.IsEmpty)
                        {
                            var newToAdd = new PreviousCalliBytesToApply
                            {
                                byteCount = current.Length,
                                target    = entities[i],
                                ptr       = AllocatorManager.Allocate<byte>(newBufferAllocator, current.Length),
                            };
                            UnsafeUtility.MemCpy(newToAdd.ptr, current.GetUnsafeReadOnlyPtr(), current.Length);
                            newBuffersList.AddNoResize(newToAdd);
                        }
                        accb.Add(entities[i], unfilteredChunkIndex);
                    }
                    else
                    {
                        var previous = previousBuffers[i];
                        if (current.Length == previous.Length &&
                            (current.Length == 0 || UnsafeUtility.MemCmp(current.GetUnsafeReadOnlyPtr(), previous.GetUnsafeReadOnlyPtr(), current.Length) == 0))
                        {
                            changeFlags[i] = false;
                            continue;
                        }
                        previous.Clear();
                        if (current.Length > 0)
                            previous.AddRange(current.AsNativeArray().Reinterpret<PreviousCalliByte>());
                        changeFlags[i] = true;
                    }

                    var calliString = new CalliString(current);
                    // Todo: Parse XML tags and scan for font info.
                }
            }
        }
    }
}

