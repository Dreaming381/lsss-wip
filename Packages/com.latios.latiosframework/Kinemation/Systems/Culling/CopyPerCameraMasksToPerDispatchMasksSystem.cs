using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Latios.Kinemation
{
    [RequireMatchingQueriesForUpdate]
    [DisableAutoCreation]
    [BurstCompile]
    public partial struct CopyPerCameraMasksToPerDispatchMasksSystem : ISystem, ILatiosApi
    {
        EntityQuery m_renderMetaQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            this.OnCreateForLatios(ref state);
            m_renderMetaQuery = state.Fluent().With<ChunkPerCameraCullingMask, ChunkHeader>(true).With<ChunkPerDispatchCullingMask>(false).Build();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var api          = this.GetApi(ref state);
            state.Dependency = new RenderJob().Inject(api).ScheduleParallel(m_renderMetaQuery, state.Dependency);
        }

        [BurstCompile]
        partial struct RenderJob : IJobChunk, IInjectable
        {
            [ReadOnly, Inject] ComponentTypeHandle<ChunkPerCameraCullingMask> perCameraHandle;
            [Inject] ComponentTypeHandle<ChunkPerDispatchCullingMask>         perDispatchHandle;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                var perCamera   = chunk.GetNativeArray(ref perCameraHandle).Reinterpret<ChunkPerDispatchCullingMask>();
                var perDispatch = chunk.GetNativeArray(ref perDispatchHandle);
                perDispatch.CopyFrom(perCamera);
            }
        }
    }
}

