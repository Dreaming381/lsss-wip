using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;

namespace Latios.Kinemation.Systems
{
    [RequireMatchingQueriesForUpdate]
    [DisableAutoCreation]
    [BurstCompile]
    public partial struct ClearPerFrameCullingMasksSystem : ISystem, ILatiosApi
    {
        EntityQuery m_renderMetaQuery;
        EntityQuery m_skeletonMetaQuery;

        public void OnCreate(ref SystemState state)
        {
            this.OnCreateForLatios(ref state);
            m_renderMetaQuery   = state.Fluent().With<ChunkPerFrameCullingMask>(false).With<ChunkPerCameraCullingMask>(false).With<ChunkHeader>(true).Build();
            m_skeletonMetaQuery = state.Fluent().With<RenderVisibilityFeedbackFlag>(false).With<SkeletonRootTag>(true).Build();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var api          = this.GetApi(ref state);
            state.Dependency = new ClearRenderJob
            {
                lastSystemVersion = state.LastSystemVersion
            }.Inject(api).ScheduleParallel(m_renderMetaQuery, state.Dependency);

            if (!m_skeletonMetaQuery.IsEmptyIgnoreFilter)
            {
                state.Dependency = new ClearSkeletonsJob().Inject(api).ScheduleParallel(m_skeletonMetaQuery, state.Dependency);
            }
        }

        [BurstCompile]
        partial struct ClearRenderJob : IJobChunk, IInjectable
        {
            [Inject] ComponentTypeHandle<ChunkPerFrameCullingMask>    frameHandle;
            [Inject] ComponentTypeHandle<ChunkPerDispatchCullingMask> dispatchHandle;
            public uint                                               lastSystemVersion;

            public unsafe void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                if (chunk.DidChange(ref frameHandle, lastSystemVersion))
                {
                    var ptr = chunk.GetComponentDataPtrRW(ref frameHandle);
                    UnsafeUtility.MemClear(ptr, sizeof(ChunkPerFrameCullingMask) * chunk.Count);
                }
                if (chunk.DidChange(ref dispatchHandle, lastSystemVersion))
                {
                    var ptr = chunk.GetComponentDataPtrRW(ref dispatchHandle);
                    UnsafeUtility.MemClear(ptr, sizeof(ChunkPerDispatchCullingMask) * chunk.Count);
                }
            }
        }

        [BurstCompile]
        partial struct ClearSkeletonsJob : IJobChunk, IInjectable
        {
            [Inject] ComponentTypeHandle<RenderVisibilityFeedbackFlag> flagHandle;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                chunk.SetComponentEnabledForAll(ref flagHandle, false);
            }
        }
    }
}

