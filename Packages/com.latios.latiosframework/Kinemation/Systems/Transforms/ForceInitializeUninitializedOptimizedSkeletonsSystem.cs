using Latios.Transforms;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;

namespace Latios.Kinemation.Systems
{
#if !LATIOS_TRANSFORMS_UNITY
    [RequireMatchingQueriesForUpdate]
    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
    [UpdateInGroup(typeof(Latios.Transforms.Systems.ExportToGameObjectTransformsEndSimulationSuperSystem), OrderFirst = true)]
    [DisableAutoCreation]
    [BurstCompile]
    public partial struct ForceInitializeUninitializedOptimizedSkeletonsSystem : ISystem, ILatiosApi
    {
        EntityQuery m_query;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            this.OnCreateForLatios(ref state);
            m_query = state.Fluent().With<OptimizedSkeletonHierarchyBlobReference>(true).With<OptimizedSkeletonState, OptimizedBoneTransform, WorldTransform>(false)
                      .Without<OptimizedSkeletonTag>().Build();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var api          = this.GetApi(ref state);
            var job          = new Job().Inject(api);
            var jh           = job.transformAspectHandleAccess.ScheduleChunkCaptureForQuery(m_query, state.Dependency);
            jh               = job.transformAspectHandleAccess.ScheduleChunkGrouping(jh);
            state.Dependency = job.GetTransformsScheduler().ScheduleParallel(jh);
        }

#if LATIOS_BURST_DETERMINISM
        [BurstCompile(FloatMode = FloatMode.Deterministic)]
#else
        [BurstCompile]
#endif
        partial struct Job : IJobChunk, IJobChunkParallelTransform, IInjectable
        {
            [Inject] TransformAspectParallelChunkHandle                                     transformHandle;
            [ReadOnly, Inject] ComponentTypeHandle<OptimizedSkeletonHierarchyBlobReference> hierarchyHandle;
            [ReadOnly, Inject] BufferTypeHandle<DependentSkinnedMesh>                       skinnedMeshesHandle;
            [ReadOnly, Inject] ComponentLookup<Socket>                                      socketLookup;
            [Inject] ComponentTypeHandle<OptimizedSkeletonState>                            stateHandle;
            [Inject] BufferTypeHandle<OptimizedBoneTransform>                               boneTransformHandle;

            public ref TransformAspectParallelChunkHandle transformAspectHandleAccess => ref transformHandle.RefAccess();

            public unsafe void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                transformHandle.OnChunkBegin(in chunk, unfilteredChunkIndex, useEnabledMask, in chunkEnabledMask);

                var hierarchies        = chunk.GetNativeArray(ref hierarchyHandle);
                var boneBuffers        = chunk.GetBufferAccessor(ref boneTransformHandle);
                var states             = chunk.GetNativeArray(ref stateHandle);
                var skinnedMeshBuffers = chunk.GetBufferAccessor(ref skinnedMeshesHandle);

                for (int i = 0; i < chunk.Count; i++)
                {
                    var                                            boneBuffer       = boneBuffers[i];
                    DynamicBuffer<OptimizedBoneInertialBlendState> dummyBlendBuffer = default;

                    _ = new OptimizedSkeletonAspect(transformHandle[i],
                                                    ref socketLookup,
                                                    new RefRO<OptimizedSkeletonHierarchyBlobReference>(hierarchies, i),
                                                    new RefRW<OptimizedSkeletonState>(states, i),
                                                    ref boneBuffer,
                                                    ref dummyBlendBuffer,
                                                    skinnedMeshBuffers.Length > 0 ? skinnedMeshBuffers[i] : default);
                }
            }
        }
    }

#else

    [RequireMatchingQueriesForUpdate]
    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
    [UpdateInGroup(typeof(Unity.Transforms.TransformSystemGroup))]
    [UpdateBefore(typeof(UpdateSocketsSystem))]
    [DisableAutoCreation]
    [BurstCompile]
    public partial struct ForceInitializeUninitializedOptimizedSkeletonsSystem : ISystem, ILatiosApi
    {
        EntityQuery m_query;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            this.OnCreateForLatios(ref state);
            m_query = state.Fluent().With<OptimizedSkeletonHierarchyBlobReference, Unity.Transforms.LocalToWorld>(true).With<OptimizedSkeletonState, OptimizedBoneTransform>(false)
                      .Without<OptimizedSkeletonTag>().Build();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var job = new Job().Inject(api);
            state.Dependency = job.ScheduleParallel(m_query, state.Dependency);
        }

#if LATIOS_BURST_DETERMINISM
        [BurstCompile(FloatMode = FloatMode.Deterministic)]
#else
        [BurstCompile]
#endif
        partial struct Job : IJobChunk, IInjectable
        {
            [ReadOnly, Inject] ComponentTypeHandle<OptimizedSkeletonHierarchyBlobReference> hierarchyHandle;
            [ReadOnly, Inject] ComponentTypeHandle<Unity.Transforms.LocalToWorld> transformHandle;
            [Inject] ComponentTypeHandle<OptimizedSkeletonState> stateHandle;
            [Inject] BufferTypeHandle<OptimizedBoneTransform> boneTransformHandle;

            public unsafe void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                var ltws        = chunk.GetComponentDataPtrRO(ref transformHandle);
                var hierarchies = chunk.GetNativeArray(ref hierarchyHandle);
                var boneBuffers = chunk.GetBufferAccessor(ref boneTransformHandle);
                var states      = chunk.GetNativeArray(ref stateHandle);

                for (int i = 0; i < chunk.Count; i++)
                {
                    var boneBuffer       = boneBuffers[i];
                    DynamicBuffer<OptimizedBoneInertialBlendState> dummyBlendBuffer = default;

                    _ = new OptimizedSkeletonAspect(in ltws[i],
                                                    new RefRO<OptimizedSkeletonHierarchyBlobReference>(hierarchies, i),
                                                    new RefRW<OptimizedSkeletonState>(states, i),
                                                    ref boneBuffer,
                                                    ref dummyBlendBuffer,
                                                    default);
                }
            }
        }
    }
#endif
}

