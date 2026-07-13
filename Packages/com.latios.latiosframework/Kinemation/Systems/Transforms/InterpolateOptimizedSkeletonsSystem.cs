using Latios.Transforms;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

using static Unity.Entities.SystemAPI;

namespace Latios.Kinemation.Systems
{
#if !LATIOS_TRANSFORMS_UNITY
    [UpdateInGroup(typeof(Latios.Systems.TickedInterpolateSuperSystem))]
    [RequireMatchingQueriesForUpdate]
    [DisableAutoCreation]
    [BurstCompile]
    public partial struct InterpolateOptimizedSkeletonsSystem : ISystem
    {
        LatiosWorldUnmanaged latiosWorld;
        EntityQuery          m_query;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            latiosWorld = state.GetLatiosWorldUnmanaged();
            m_query     = state.Fluent().With<InterpolateOptimizedSkeletonTag, TickedOptimizedSkeletonState, TickedOptimizedBoneTransform>(true)
                          .With<OptimizedSkeletonHierarchyBlobReference, DependentSkinnedMesh>(true).With<OptimizedSkeletonState, OptimizedBoneTransform,
                                                                                                          WorldTransform>(false).Build();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var job = new Job
            {
                boneTransformHandle = GetBufferTypeHandle<OptimizedBoneTransform>(false),
                factor              = latiosWorld.worldBlackboardEntity.GetComponentData<TickingState>().finalTickFraction,
                hierarchyHandle     = GetComponentTypeHandle<OptimizedSkeletonHierarchyBlobReference>(true),
                skinnedMeshesHandle = GetBufferTypeHandle<DependentSkinnedMesh>(true),
                socketLookup        = GetComponentLookup<Socket>(true),
                stateHandle         = GetComponentTypeHandle<OptimizedSkeletonState>(false),
                tickedBonesHandle   = GetBufferTypeHandle<TickedOptimizedBoneTransform>(true),
                tickedStateHandle   = GetComponentTypeHandle<TickedOptimizedSkeletonState>(true),
                transformHandle     = new TransformAspectParallelChunkHandle(GetComponentLookup<WorldTransform>(false),
                                                                             GetComponentTypeHandle<RootReference>(true),
                                                                             GetBufferLookup<EntityInHierarchy>(       true),
                                                                             GetBufferLookup<EntityInHierarchyCleanup>(true),
                                                                             GetEntityStorageInfoLookup(),
                                                                             ref state)
            };
            var jh           = job.transformHandle.ScheduleChunkCaptureForQuery(m_query, state.Dependency);
            jh               = job.transformHandle.ScheduleChunkGrouping(jh);
            state.Dependency = job.GetTransformsScheduler().ScheduleParallel(jh);
        }

        [BurstCompile]
        struct Job : IJobChunk, IJobChunkParallelTransform
        {
            [ReadOnly] public ComponentTypeHandle<TickedOptimizedSkeletonState>            tickedStateHandle;
            [ReadOnly] public BufferTypeHandle<TickedOptimizedBoneTransform>               tickedBonesHandle;
            [ReadOnly] public ComponentTypeHandle<OptimizedSkeletonHierarchyBlobReference> hierarchyHandle;
            [ReadOnly] public BufferTypeHandle<DependentSkinnedMesh>                       skinnedMeshesHandle;
            [ReadOnly] public ComponentLookup<Socket>                                      socketLookup;
            public ComponentTypeHandle<OptimizedSkeletonState>                             stateHandle;
            public BufferTypeHandle<OptimizedBoneTransform>                                boneTransformHandle;
            public TransformAspectParallelChunkHandle                                      transformHandle;
            public float                                                                   factor;

            public ref TransformAspectParallelChunkHandle transformAspectHandleAccess => ref transformHandle.RefAccess();

            public unsafe void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                transformHandle.OnChunkBegin(in chunk, unfilteredChunkIndex, useEnabledMask, in chunkEnabledMask);

                var hierarchies        = chunk.GetNativeArray(ref hierarchyHandle);
                var boneBuffers        = chunk.GetBufferAccessor(ref boneTransformHandle);
                var states             = chunk.GetNativeArray(ref stateHandle);
                var skinnedMeshBuffers = chunk.GetBufferAccessor(ref skinnedMeshesHandle);

                var tickedStates      = chunk.GetComponentDataPtrRO(ref tickedStateHandle);
                var tickedBoneBuffers = chunk.GetBufferAccessor(ref tickedBonesHandle);

                for (int i = 0; i < chunk.Count; i++)
                {
                    var                                            boneBuffer       = boneBuffers[i];
                    DynamicBuffer<OptimizedBoneInertialBlendState> dummyBlendBuffer = default;
                    var                                            skeletonAspect   = new OptimizedSkeletonAspect(transformHandle[i],
                                                                       ref socketLookup,
                                                                       new RefRO<OptimizedSkeletonHierarchyBlobReference>(hierarchies,
                                                                                                                          i),
                                                                       new RefRW<OptimizedSkeletonState>(states, i),
                                                                       ref boneBuffer,
                                                                       ref dummyBlendBuffer,
                                                                       skinnedMeshBuffers[i]);
                    var dstBones = skeletonAspect.rawLocalTransformsRW;

                    var tickedState = tickedStates[i];
                    var tickedBones = tickedBoneBuffers[i].AsNativeArray();
                    if (tickedBones.Length != dstBones.Length * 6)
                    {
                        if (tickedBones.Length == dstBones.Length)
                            continue;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
                        throw new System.InvalidOperationException(
                            $"TickedOptimizedBoneTransform buffer length {tickedBones.Length} does not match OptimizedBoneTransform buffer length {dstBones.Length * 6}");
#endif
                    }

                    var mask          = (byte)(tickedState.state & OptimizedSkeletonState.Flags.RotationMask);
                    var rotation      = OptimizedSkeletonState.PreviousFromMask[mask];
                    var previousBones = tickedBones.GetSubArray(rotation * 2 * dstBones.Length + dstBones.Length, dstBones.Length);
                    var currentBones  = previousBones;
                    if ((tickedState.state & OptimizedSkeletonState.Flags.IsDirty) == OptimizedSkeletonState.Flags.IsDirty)
                    {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                        if ((tickedState.state & OptimizedSkeletonState.Flags.NeedsSync) == OptimizedSkeletonState.Flags.NeedsSync)
                        {
                            throw new System.InvalidOperationException(
                                "Ticked optimized skeleton has not had its bones synced yet. You must call EndSamplingAndSync() prior to interpolation.");
                        }
#endif
                        rotation     = OptimizedSkeletonState.CurrentFromMask[mask];
                        currentBones = tickedBones.GetSubArray(rotation * 2 * dstBones.Length + dstBones.Length, dstBones.Length);
                    }

                    for (int j = 0; j < dstBones.Length; j++)
                    {
                        var prev      = previousBones[j];
                        var curr      = tickedBones[j];
                        var rot       = math.nlerp(prev.boneTransform.rotation, curr.boneTransform.rotation, factor);
                        var pos       = math.lerp(prev.boneTransform.position, curr.boneTransform.position, factor);
                        var scale     = math.lerp(prev.boneTransform.scale, curr.boneTransform.scale, factor);
                        var stretch   = math.lerp(prev.boneTransform.stretch, curr.boneTransform.stretch, factor);
                        var context32 = curr.boneTransform.context32;
                        dstBones[j]   = new TransformQvvs(pos, rot, scale, stretch, context32);
                    }
                    skeletonAspect.EndSamplingAndSync();
                }
            }
        }
    }
#else
    [UpdateInGroup(typeof(Latios.Systems.TickedInterpolateSuperSystem))]
    [RequireMatchingQueriesForUpdate]
    [DisableAutoCreation]
    [BurstCompile]
    public partial struct InterpolateOptimizedSkeletonsSystem : ISystem
    {
        LatiosWorldUnmanaged latiosWorld;
        EntityQuery m_query;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            latiosWorld = state.GetLatiosWorldUnmanaged();
            m_query     = state.Fluent().With<InterpolateOptimizedSkeletonTag, TickedOptimizedSkeletonState, TickedOptimizedBoneTransform>(true)
                          .With<OptimizedSkeletonHierarchyBlobReference, DependentSkinnedMesh, Unity.Transforms.LocalToWorld>(true).With<OptimizedSkeletonState,
                                                                                                                                         OptimizedBoneTransform>(false).Build();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var job = new Job
            {
                boneTransformHandle = GetBufferTypeHandle<OptimizedBoneTransform>(false),
                factor              = latiosWorld.worldBlackboardEntity.GetComponentData<TickingState>().finalTickFraction,
                hierarchyHandle     = GetComponentTypeHandle<OptimizedSkeletonHierarchyBlobReference>(true),
                stateHandle         = GetComponentTypeHandle<OptimizedSkeletonState>(false),
                tickedBonesHandle   = GetBufferTypeHandle<TickedOptimizedBoneTransform>(true),
                tickedStateHandle   = GetComponentTypeHandle<TickedOptimizedSkeletonState>(true),
                transformHandle     = GetComponentTypeHandle<Unity.Transforms.LocalToWorld>(true),
            };
            state.Dependency = job.ScheduleParallel(m_query, state.Dependency);
        }

        [BurstCompile]
        struct Job : IJobChunk, IJobChunkParallelTransform
        {
            [ReadOnly] public ComponentTypeHandle<TickedOptimizedSkeletonState> tickedStateHandle;
            [ReadOnly] public BufferTypeHandle<TickedOptimizedBoneTransform> tickedBonesHandle;
            [ReadOnly] public ComponentTypeHandle<OptimizedSkeletonHierarchyBlobReference> hierarchyHandle;
            [ReadOnly] public ComponentTypeHandle<Unity.Transforms.LocalToWorld> transformHandle;
            public ComponentTypeHandle<OptimizedSkeletonState> stateHandle;
            public BufferTypeHandle<OptimizedBoneTransform> boneTransformHandle;
            public float factor;

            public ref TransformAspectParallelChunkHandle transformAspectHandleAccess => ref transformHandle.RefAccess();

            public unsafe void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                var ltws        = chunk.GetComponentDataPtrRO(ref transformHandle);
                var hierarchies = chunk.GetNativeArray(ref hierarchyHandle);
                var boneBuffers = chunk.GetBufferAccessor(ref boneTransformHandle);
                var states      = chunk.GetNativeArray(ref stateHandle);

                var tickedStates      = chunk.GetComponentDataPtrRO(ref tickedStateHandle);
                var tickedBoneBuffers = chunk.GetBufferAccessor(ref tickedBonesHandle);

                for (int i = 0; i < chunk.Count; i++)
                {
                    var boneBuffer       = boneBuffers[i];
                    DynamicBuffer<OptimizedBoneInertialBlendState> dummyBlendBuffer = default;
                    var skeletonAspect   = new OptimizedSkeletonAspect(in transformHandle[i],
                                                                       new RefRO<OptimizedSkeletonHierarchyBlobReference>(hierarchies,
                                                                                                                          i),
                                                                       new RefRW<OptimizedSkeletonState>(states, i),
                                                                       ref boneBuffer,
                                                                       ref dummyBlendBuffer,
                                                                       default);
                    var dstBones = skeletonAspect.rawLocalTransformsRW;

                    var tickedState = tickedStates[i];
                    var tickedBones = tickedBoneBuffers[i].AsNativeArray();
                    if (tickedBones.Length != dstBones.Length * 6)
                    {
                        if (tickedBones.Length == dstBones.Length)
                            continue;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
                        throw new System.InvalidOperationException(
                            $"TickedOptimizedBoneTransform buffer length {tickedBones.Length} does not match OptimizedBoneTransform buffer length {dstBones.Length * 6}");
#endif
                    }

                    var mask          = (byte)(tickedState.state & OptimizedSkeletonState.Flags.RotationMask);
                    var rotation      = OptimizedSkeletonState.PreviousFromMask[mask];
                    var previousBones = tickedBones.GetSubArray(rotation * 2 * dstBones.Length + dstBones.Length, dstBones.Length);
                    var currentBones  = previousBones;
                    if ((tickedState.state & OptimizedSkeletonState.Flags.IsDirty) == OptimizedSkeletonState.Flags.IsDirty)
                    {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                        if ((tickedState.state & OptimizedSkeletonState.Flags.NeedsSync) == OptimizedSkeletonState.Flags.NeedsSync)
                        {
                            throw new System.InvalidOperationException(
                                "Ticked optimized skeleton has not had its bones synced yet. You must call EndSamplingAndSync() prior to interpolation.");
                        }
#endif
                        rotation     = OptimizedSkeletonState.CurrentFromMask[mask];
                        currentBones = tickedBones.GetSubArray(rotation * 2 * dstBones.Length + dstBones.Length, dstBones.Length);
                    }

                    for (int j = 0; j < dstBones.Length; j++)
                    {
                        var prev      = previousBones[j];
                        var curr      = tickedBones[j];
                        var rot       = math.nlerp(prev.boneTransform.rotation, curr.boneTransform.rotation, factor);
                        var pos       = math.lerp(prev.boneTransform.position, curr.boneTransform.position, factor);
                        var scale     = math.lerp(prev.boneTransform.scale, curr.boneTransform.scale, factor);
                        var stretch   = math.lerp(prev.boneTransform.stretch, curr.boneTransform.stretch, factor);
                        var context32 = curr.boneTransform.context32;
                        dstBones[j] = new TransformQvvs(pos, rot, scale, stretch, context32);
                    }
                    skeletonAspect.EndSamplingAndSync();
                }
            }
        }
    }
#endif
}

