#if !LATIOS_TRANSFORMS_UNITY
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

using static Unity.Entities.SystemAPI;

namespace Latios.Transforms.Systems
{
    [UpdateInGroup(typeof(Latios.Systems.TickedUpdateHistorySuperSystem))]
    [RequireMatchingQueriesForUpdate]
    [DisableAutoCreation]
    [BurstCompile]
    public partial struct TickedMotionHistorySystem : ISystem
    {
        LatiosWorldUnmanaged latiosWorld;

        EntityQuery m_newRootsJobQuery;
        EntityQuery m_newChildrenJobQuery;
        EntityQuery m_allQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            latiosWorld = state.GetLatiosWorldUnmanaged();

            m_newRootsJobQuery = state.Fluent().With<TickedWorldTransform, TickedPreviousTransform, EntityInHierarchy>(true).Build();
            m_newRootsJobQuery.AddChangedVersionFilter(ComponentType.ReadOnly<TickedWorldTransform>());
            m_newRootsJobQuery.AddChangedVersionFilter(ComponentType.ReadOnly<TickedPreviousTransform>());
            m_newRootsJobQuery.AddOrderVersionFilter();

            m_newChildrenJobQuery = state.Fluent().With<TickedWorldTransform, TickedPreviousTransform, RootReference>(true).Build();
            m_newChildrenJobQuery.AddChangedVersionFilter(ComponentType.ReadOnly<TickedWorldTransform>());
            m_newChildrenJobQuery.AddChangedVersionFilter(ComponentType.ReadOnly<TickedPreviousTransform>());
            m_newChildrenJobQuery.AddOrderVersionFilter();

            m_allQuery = state.Fluent().With<TickedWorldTransform, TickedPreviousTransform>(true).Build();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var tickingState = latiosWorld.worldBlackboardEntity.GetComponentData<TickingState>();
            if (tickingState.discardPreviousTick)
            {
                // Revert
                var rootCount = m_newRootsJobQuery.CalculateEntityCountWithoutFiltering();
                var rootsSet  = new NativeParallelHashSet<Entity>(rootCount, state.WorldUpdateAllocator);
                var jh        = new DetectNewRootsJob
                {
                    entityHandle            = GetEntityTypeHandle(),
                    previousTransformHandle = GetComponentTypeHandle<TickedPreviousTransform>(),
                    rootsSet                = rootsSet.AsParallelWriter()
                }.ScheduleParallel(m_newRootsJobQuery, state.Dependency);
                var childChunkCount = m_newChildrenJobQuery.CalculateChunkCountWithoutFiltering();
                var stream          = new NativeStream(childChunkCount, state.WorldUpdateAllocator);
                jh                  = new CaptureNewChildrenJob
                {
                    entityInHierarchyCleanupLookup = GetBufferLookup<EntityInHierarchyCleanup>(true),
                    entityInHierarchyLookup        = GetBufferLookup<EntityInHierarchy>(true),
                    esil                           = GetEntityStorageInfoLookup(),
                    previousTransformHandle        = GetComponentTypeHandle<TickedPreviousTransform>(true),
                    previousTransformLookup        = GetComponentLookup<TickedPreviousTransform>(true),
                    rootReferenceHandle            = GetComponentTypeHandle<RootReference>(true),
                    rootsSet                       = rootsSet,
                    stream                         = stream.AsWriter(),
                    worldTransformHandle           = GetComponentTypeHandle<TickedWorldTransform>(true),
                    worldTransformLookup           = GetComponentLookup<TickedWorldTransform>(true),
                }.ScheduleParallel(m_newRootsJobQuery, jh);
                m_allQuery.ResetFilter();
                m_allQuery.AddChangedVersionFilter(ComponentType.ReadOnly<TickedWorldTransform>());
                jh = new RestoreExistingFromHistoryJob
                {
                    entityInHierarchyCleanupLookup    = GetBufferLookup<EntityInHierarchyCleanup>(true),
                    entityInHierarchyLookup           = GetBufferLookup<EntityInHierarchy>(true),
                    previousLocalTransformCacheHandle = GetComponentTypeHandle<TickedPreviousLocalTransformCache>(true),
                    previousTransformHandle           = GetComponentTypeHandle<TickedPreviousTransform>(true),
                    rootReferenceHandle               = GetComponentTypeHandle<RootReference>(true),
                    worldTransformHandle              = GetComponentTypeHandle<TickedWorldTransform>(false),
                }.ScheduleParallel(m_allQuery, jh);
                jh = new PropoagateNewChildrenAfterExistingRestoreJob
                {
                    entityHandle          = GetEntityTypeHandle(),
                    stream                = stream.AsReader(),
                    transformAspectLookup = new TickedTransformAspectLookup(GetComponentLookup<TickedWorldTransform>(false),
                                                                            GetComponentLookup<RootReference>(true),
                                                                            GetBufferLookup<EntityInHierarchy>(       true),
                                                                            GetBufferLookup<EntityInHierarchyCleanup>(true),
                                                                            GetEntityStorageInfoLookup())
                }.Schedule(jh);
                m_allQuery.ResetFilter();
                m_allQuery.AddChangedVersionFilter(ComponentType.ReadOnly<TickedWorldTransform>());
                m_allQuery.AddChangedVersionFilter(ComponentType.ReadOnly<TickedPreviousTransform>());
                m_allQuery.AddOrderVersionFilter();
                state.Dependency = new InitUninitializedHistoryAfterRestoreJob
                {
                    entityInHierarchyCleanupLookup    = GetBufferLookup<EntityInHierarchyCleanup>(true),
                    entityInHierarchyLookup           = GetBufferLookup<EntityInHierarchy>(true),
                    previousLocalTransformCacheHandle = GetComponentTypeHandle<TickedPreviousLocalTransformCache>(false),
                    previousTransformHandle           = GetComponentTypeHandle<TickedPreviousTransform>(false),
                    rootReferenceHandle               = GetComponentTypeHandle<RootReference>(true),
                    twoAgoTransformHandle             = GetComponentTypeHandle<TickedTwoAgoTransform>(false),
                    worldTransformHandle              = GetComponentTypeHandle<TickedWorldTransform>(true),
                }.ScheduleParallel(m_allQuery, jh);
            }
            else
            {
                // Advance
                m_allQuery.ResetFilter();
                state.Dependency = new AdvanceHistoryJob
                {
                    entityInHierarchyCleanupLookup    = GetBufferLookup<EntityInHierarchyCleanup>(true),
                    entityInHierarchyLookup           = GetBufferLookup<EntityInHierarchy>(true),
                    lastSystemVersion                 = state.LastSystemVersion,
                    previousLocalTransformCacheHandle = GetComponentTypeHandle<TickedPreviousLocalTransformCache>(false),
                    previousTransformHandle           = GetComponentTypeHandle<TickedPreviousTransform>(false),
                    rootReferenceHandle               = GetComponentTypeHandle<RootReference>(true),
                    twoAgoTransformHandle             = GetComponentTypeHandle<TickedTwoAgoTransform>(false),
                    worldTransformHandle              = GetComponentTypeHandle<TickedWorldTransform>(true),
                }.ScheduleParallel(m_allQuery, state.Dependency);
            }
        }

        #region Revert to Previous
        struct CapturedChildChunk
        {
            public ArchetypeChunk chunk;
            public int            countInChunk;
        }

        struct CapturedChild
        {
            public int                               indexInChunk;
            public TransformQvvs                     newWorldTransform;
            public TickedPreviousLocalTransformCache newLocalTransform;
        }

        // Requires change version on TickedWorldTransform, change version on TickedPreviousTransform, and order version
        [BurstCompile]
        struct DetectNewRootsJob : IJobChunk
        {
            [ReadOnly] public EntityTypeHandle                             entityHandle;
            [ReadOnly] public ComponentTypeHandle<TickedPreviousTransform> previousTransformHandle;
            public NativeParallelHashSet<Entity>.ParallelWriter            rootsSet;

            public unsafe void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                var entities   = chunk.GetEntityDataPtrRO(entityHandle);
                var transforms = chunk.GetComponentDataPtrRO(ref previousTransformHandle);
                for (int i = 0; i < chunk.Count; i++)
                {
                    if (transforms[i].rotation.value.Equals(float4.zero))
                        rootsSet.Add(entities[i]);
                }
            }
        }

        // Requires change version on TickedWorldTransform, change version on TickedPreviousTransform, and order version
        [BurstCompile]
        struct CaptureNewChildrenJob : IJobChunk
        {
            [ReadOnly] public ComponentTypeHandle<RootReference>           rootReferenceHandle;
            [ReadOnly] public BufferLookup<EntityInHierarchy>              entityInHierarchyLookup;
            [ReadOnly] public BufferLookup<EntityInHierarchyCleanup>       entityInHierarchyCleanupLookup;
            [ReadOnly] public ComponentTypeHandle<TickedWorldTransform>    worldTransformHandle;
            [ReadOnly] public ComponentTypeHandle<TickedPreviousTransform> previousTransformHandle;
            [ReadOnly] public ComponentLookup<TickedWorldTransform>        worldTransformLookup;
            [ReadOnly] public ComponentLookup<TickedPreviousTransform>     previousTransformLookup;
            [ReadOnly] public EntityStorageInfoLookup                      esil;
            [ReadOnly] public NativeParallelHashSet<Entity>                rootsSet;
            public NativeStream.Writer                                     stream;

            public unsafe void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                var                transforms           = chunk.GetComponentDataPtrRO(ref worldTransformHandle);
                var                prevs                = chunk.GetComponentDataPtrRO(ref previousTransformHandle);
                var                rootRefs             = chunk.GetComponentDataPtrRO(ref rootReferenceHandle);
                CapturedChildChunk capturedChunkDefault = default;
                ref var            capturedChunk        = ref capturedChunkDefault;
                bool               chunkHasWrites       = false;
                for (int i = 0; i < chunk.Count; i++)
                {
                    if (prevs[i].rotation.value.Equals(float4.zero))
                    {
                        // Note: Checking the hashset here is fast, but it means we drop support for reassigning the parent outside of ticking.
                        // If needed, we can simply drop this check since we check parent history down below. But that would mean a hierarchy
                        // and previous transform fetch for every descendant entity in a newly-instantiated hierarchy.
                        var rootRef = rootRefs[i];
                        if (rootsSet.Contains(rootRef.rootEntity))
                            continue; // The root is new, meaning the whole hierarchy is new.

                        var handle = rootRef.ToHandle(ref entityInHierarchyLookup, ref entityInHierarchyCleanupLookup);
                        var parent = handle.FindParent(esil);
                        if (parent.isNull)
                            continue; // There's no parent being reverted, so there's nothing that needs to be propagated.

                        var parentPrevious = previousTransformLookup[parent.entity];
                        if (parentPrevious.rotation.value.Equals(float4.zero))
                            continue; // The parent is also new. We'll clean up this entity via propagation.

                        var parentTransform = worldTransformLookup[parent.entity];
                        if (parentTransform.worldTransform.Equals(parentPrevious.worldTransform))
                            continue; // No change to revert.

                        if (!chunkHasWrites)
                        {
                            stream.BeginForEachIndex(unfilteredChunkIndex);
                            capturedChunk              = ref stream.Allocate<CapturedChildChunk>();
                            capturedChunk.chunk        = chunk;
                            capturedChunk.countInChunk = 0;
                            chunkHasWrites             = true;
                        }

                        var localBackup    = WorldLocalOps.CopyTickedLocalToCache(in handle);
                        var worldTransform = transforms[i].worldTransform;
                        WorldLocalOps.PropagateTransform(in parentPrevious.worldTransform, in parentTransform.worldTransform, ref worldTransform, in parent, in handle, true);
                        var localTransform = WorldLocalOps.CopyTickedLocalToCache(in handle);
                        WorldLocalOps.RestoreFromTickedLocalCache(in handle, in localBackup);

                        stream.Write(new CapturedChild
                        {
                            indexInChunk      = i,
                            newLocalTransform = localTransform,
                            newWorldTransform = worldTransform
                        });
                        capturedChunk.countInChunk++;
                    }
                }

                if (chunkHasWrites)
                    stream.EndForEachIndex();
            }
        }

        // Requires change version on TickedWorldTransform
        [BurstCompile]
        struct RestoreExistingFromHistoryJob : IJobChunk
        {
            [ReadOnly] public ComponentTypeHandle<RootReference>                     rootReferenceHandle;
            [ReadOnly] public BufferLookup<EntityInHierarchy>                        entityInHierarchyLookup;
            [ReadOnly] public BufferLookup<EntityInHierarchyCleanup>                 entityInHierarchyCleanupLookup;
            [ReadOnly] public ComponentTypeHandle<TickedPreviousTransform>           previousTransformHandle;
            [ReadOnly] public ComponentTypeHandle<TickedPreviousLocalTransformCache> previousLocalTransformCacheHandle;
            public ComponentTypeHandle<TickedWorldTransform>                         worldTransformHandle;

            public unsafe void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                var  rootReferences      = chunk.GetComponentDataPtrRO(ref rootReferenceHandle);
                var  prevWorlds          = chunk.GetComponentDataPtrRO(ref previousTransformHandle);
                var  prevLocals          = rootReferences != null? chunk.GetComponentDataPtrRO(ref previousLocalTransformCacheHandle) : null;
                var  worldTransforms     = chunk.GetComponentDataPtrRO(ref worldTransformHandle);
                bool wroteWorldTransform = false;

                for (int i = 0; i < chunk.Count; i++)
                {
                    if (prevWorlds[i].rotation.value.Equals(float4.zero))
                        continue;
                    if (!wroteWorldTransform)
                        wroteWorldTransform           = prevWorlds[i].worldTransform.Equals(worldTransforms[i].worldTransform);
                    worldTransforms[i].worldTransform = prevWorlds[i].worldTransform;

                    if (prevLocals != null)
                    {
                        var handle    = rootReferences[i].ToHandle(ref entityInHierarchyLookup, ref entityInHierarchyCleanupLookup);
                        prevLocals[i] = WorldLocalOps.CopyTickedLocalToCache(in handle);
                    }
                }
                if (wroteWorldTransform)
                    chunk.GetComponentDataPtrRW(ref worldTransformHandle);
            }
        }

        [BurstCompile]
        struct PropoagateNewChildrenAfterExistingRestoreJob : IJob
        {
            [ReadOnly] public NativeStream.Reader stream;
            [ReadOnly] public EntityTypeHandle    entityHandle;
            public TickedTransformAspectLookup    transformAspectLookup;

            public unsafe void Execute()
            {
                NativeList<TickedTransformBatchWriteCommand>  commands = new NativeList<TickedTransformBatchWriteCommand>(Allocator.Temp);
                NativeList<TickedPreviousLocalTransformCache> caches   = new NativeList<TickedPreviousLocalTransformCache>(Allocator.Temp);
                for (int chunkIndex = 0; chunkIndex < stream.ForEachCount; chunkIndex++)
                {
                    stream.BeginForEachIndex(chunkIndex);
                    while (stream.RemainingItemCount > 0)
                    {
                        var capturedChunk = stream.Read<CapturedChildChunk>();
                        var entities      = capturedChunk.chunk.GetEntityDataPtrRO(entityHandle);

                        for (int i = 0; i < capturedChunk.countInChunk; i++)
                        {
                            var captured        = stream.Read<CapturedChild>();
                            var transformAspect = transformAspectLookup[entities[captured.indexInChunk]];
                            commands.Add(TickedTransformBatchWriteCommand.SetWorldTransform(transformAspect, in captured.newWorldTransform));
                            caches.Add(captured.newLocalTransform);
                        }
                    }
                    stream.EndForEachIndex();
                }
                commands.ApplyTransforms();
                for (int i = 0; i < commands.Length; i++)
                {
                    var handle = commands[i].aspect.entityInHierarchyHandle;
                    WorldLocalOps.RestoreFromTickedLocalCache(in handle, caches[i]);
                }
            }
        }

        // Requires change version on TickedWorldTransform, change version on TickedPreviousTransform, and order version
        [BurstCompile]
        struct InitUninitializedHistoryAfterRestoreJob : IJobChunk
        {
            [ReadOnly] public ComponentTypeHandle<RootReference>          rootReferenceHandle;
            [ReadOnly] public BufferLookup<EntityInHierarchy>             entityInHierarchyLookup;
            [ReadOnly] public BufferLookup<EntityInHierarchyCleanup>      entityInHierarchyCleanupLookup;
            [ReadOnly] public ComponentTypeHandle<TickedWorldTransform>   worldTransformHandle;
            public ComponentTypeHandle<TickedPreviousTransform>           previousTransformHandle;
            public ComponentTypeHandle<TickedPreviousLocalTransformCache> previousLocalTransformCacheHandle;
            public ComponentTypeHandle<TickedTwoAgoTransform>             twoAgoTransformHandle;

            public unsafe void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                var  prevs          = chunk.GetComponentDataPtrRO(ref previousTransformHandle);
                var  twoAgos        = chunk.GetComponentDataPtrRO(ref twoAgoTransformHandle);
                var  rootReferences = chunk.GetComponentDataPtrRO(ref rootReferenceHandle);
                bool initPrevious   = false;
                bool initTwoAgos    = false;
                for (int i = chunk.Count - 1; i >= 0; i--)
                {
                    if (prevs[i].rotation.value.Equals(float4.zero))
                    {
                        initPrevious = true;
                        break;
                    }
                }
                if (initPrevious)
                {
                    var currents = chunk.GetComponentDataPtrRO(ref worldTransformHandle);
                    chunk.GetComponentDataPtrRW(ref previousTransformHandle);
                    for (int i = 0; i < chunk.Count; i++)
                    {
                        if (prevs[i].rotation.value.Equals(float4.zero))
                        {
                            prevs[i].worldTransform = currents[i].worldTransform;
                        }
                    }
                    if (rootReferences != null)
                    {
                        var caches = chunk.GetComponentDataPtrRW(ref previousLocalTransformCacheHandle);
                        for (int i = 0; i < chunk.Count; i++)
                        {
                            var handle = rootReferences[i].ToHandle(ref entityInHierarchyLookup, ref entityInHierarchyCleanupLookup);
                            caches[i]  = WorldLocalOps.CopyTickedLocalToCache(in handle);
                        }
                    }
                }
                if (twoAgos != null)
                {
                    for (int i = chunk.Count - 1; i >= 0; i--)
                    {
                        if (twoAgos[i].rotation.value.Equals(float4.zero))
                        {
                            initTwoAgos = true;
                            break;
                        }
                    }
                }
                if (initTwoAgos)
                {
                    chunk.GetComponentDataPtrRW(ref twoAgoTransformHandle);
                    for (int i = 0; i < chunk.Count; i++)
                    {
                        if (twoAgos[i].rotation.value.Equals(float4.zero))
                        {
                            twoAgos[i].worldTransform = prevs[i].worldTransform;
                        }
                    }
                }
            }
        }
        #endregion

        [BurstCompile]
        struct AdvanceHistoryJob : IJobChunk
        {
            [ReadOnly] public ComponentTypeHandle<TickedWorldTransform>   worldTransformHandle;
            [ReadOnly] public ComponentTypeHandle<RootReference>          rootReferenceHandle;
            [ReadOnly] public BufferLookup<EntityInHierarchy>             entityInHierarchyLookup;
            [ReadOnly] public BufferLookup<EntityInHierarchyCleanup>      entityInHierarchyCleanupLookup;
            public ComponentTypeHandle<TickedPreviousTransform>           previousTransformHandle;
            public ComponentTypeHandle<TickedPreviousLocalTransformCache> previousLocalTransformCacheHandle;
            public ComponentTypeHandle<TickedTwoAgoTransform>             twoAgoTransformHandle;
            public uint                                                   lastSystemVersion;

            public unsafe void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                bool updatePrevious = chunk.DidChange(ref worldTransformHandle, lastSystemVersion);
                bool updateTwoAgo   = chunk.Has(ref twoAgoTransformHandle) && DidChangeLastFrame(chunk.GetChangeVersion(ref previousTransformHandle));
                bool checkForInit   = chunk.DidOrderChange(lastSystemVersion);

                if (!updatePrevious && checkForInit)
                {
                    var prevs = chunk.GetComponentDataPtrRO(ref previousTransformHandle);
                    for (int i = chunk.Count - 1; i >= 0; i--)
                    {
                        if (prevs[i].rotation.value.Equals(float4.zero))
                        {
                            updatePrevious = true;
                            break;
                        }
                    }
                }
                if (!updateTwoAgo && checkForInit)
                {
                    var                   twoAgos = chunk.GetComponentDataPtrRO(ref twoAgoTransformHandle);
                    TickedWorldTransform* current = null;
                    for (int i = chunk.Count - 1; i >= 0; i--)
                    {
                        if (twoAgos[i].rotation.value.Equals(float4.zero))
                        {
                            if (current == null)
                            {
                                chunk.GetComponentDataPtrRW(ref twoAgoTransformHandle);
                                current = chunk.GetComponentDataPtrRO(ref worldTransformHandle);
                            }
                            twoAgos[i].worldTransform = current[i].worldTransform;
                        }
                    }
                }

                if (updatePrevious)
                {
                    var currents = chunk.GetNativeArray(ref worldTransformHandle).Reinterpret<TransformQvvs>();
                    var prevs    = chunk.GetNativeArray(ref previousTransformHandle).Reinterpret<TransformQvvs>();

                    if (updateTwoAgo)
                    {
                        var twoAgos = chunk.GetNativeArray(ref twoAgoTransformHandle).Reinterpret<TransformQvvs>();
                        twoAgos.CopyFrom(prevs);
                    }

                    prevs.CopyFrom(currents);

                    var rootReferences = chunk.GetComponentDataPtrRO(ref rootReferenceHandle);
                    if (rootReferences != null)
                    {
                        var caches = chunk.GetComponentDataPtrRW(ref previousLocalTransformCacheHandle);
                        for (int i = 0; i < chunk.Count; i++)
                        {
                            var handle = rootReferences[i].ToHandle(ref entityInHierarchyLookup, ref entityInHierarchyCleanupLookup);
                            caches[i]  = WorldLocalOps.CopyTickedLocalToCache(in handle);
                        }
                    }
                }
                else if (updateTwoAgo)
                {
                    var prevs   = chunk.GetRequiredComponentDataPtrRO(ref previousTransformHandle);
                    var twoAgos = chunk.GetRequiredComponentDataPtrRW(ref twoAgoTransformHandle);

                    UnsafeUtility.MemCpy(twoAgos, prevs, UnsafeUtility.SizeOf<TransformQvvs>() * chunk.Count);
                }
            }

            bool DidChangeLastFrame(uint storedVersion)
            {
                // When a system runs for the first time, everything is considered changed.
                if (lastSystemVersion == 0)
                    return true;
                // Supporting wrap around for version numbers, change must be bigger than last system run.
                // (Never detect change of something the system itself changed)
                return (int)(storedVersion - lastSystemVersion) >= 0;
            }
        }
    }
}
#endif

