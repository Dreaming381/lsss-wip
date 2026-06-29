using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace Latios.Transforms
{
    [RequireMatchingQueriesForUpdate]
    [DisableAutoCreation]
    [BurstCompile]
    public partial struct TickedMotionHistorySystem : ISystem
    {
        LatiosWorldUnmanaged latiosWorld;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            latiosWorld = state.GetLatiosWorldUnmanaged();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
        }

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

