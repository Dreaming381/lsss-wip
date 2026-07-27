using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Exposed;
using Unity.Jobs;
using Unity.Mathematics;

namespace Latios.Kinemation.Systems
{
    [RequireMatchingQueriesForUpdate]
    [DisableAutoCreation]
    [BurstCompile]
    public partial struct SetRenderVisibilityFeedbackFlagsSystem : ISystem, ILatiosApi
    {
        EntityQuery m_query;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            this.OnCreateForLatios(ref state);
            m_query = state.Fluent().With<RenderVisibilityFeedbackFlag>(false).With<ChunkPerFrameCullingMask>(true, true).Build();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var api           = this.GetApi(ref state);
            state.Dependency  = new Job().Inject(api).ScheduleParallel(m_query, state.Dependency);
        }

        [BurstCompile]
        partial struct Job : IJobChunk, IInjectable
        {
            [ReadOnly, Inject] ComponentTypeHandle<ChunkPerFrameCullingMask> m_maskHandle;
            [Inject] ComponentTypeHandle<RenderVisibilityFeedbackFlag>       m_flagHandle;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                var              enabled = chunk.GetEnabledMask(ref m_flagHandle);
                ref readonly var mask    = ref chunk.GetChunkComponentRefRO(ref m_maskHandle).ValueRO;

                for (int i = 0; i < math.min(chunk.Count, 64); i++)
                {
                    if (enabled[i] != mask.lower.IsSet(i))
                        enabled[i] = mask.lower.IsSet(i);
                }

                for (int i = 0; i < chunk.Count - 64; i++)
                {
                    if (enabled[i + 64] != mask.upper.IsSet(i))
                        enabled[i + 64] = mask.upper.IsSet(i);
                }
            }
        }
    }
}
