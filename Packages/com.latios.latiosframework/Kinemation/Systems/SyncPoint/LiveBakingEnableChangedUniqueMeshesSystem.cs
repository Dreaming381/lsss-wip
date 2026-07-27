using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace Latios.Kinemation.Systems
{
    [RequireMatchingQueriesForUpdate]
    [DisableAutoCreation]
    [BurstCompile]
    public partial struct LiveBakingEnableChangedUniqueMeshesSystem : ISystem, ILatiosApi
    {
        EntityQuery m_query;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            this.OnCreateForLatios(ref state);
            m_query = state.Fluent().With<LiveBakedTag>(true).With<UniqueMeshConfig>(false).Build();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var api           = this.GetApi(ref state);
            state.Dependency = new Job
            {
                lastSystemVersion = api.worldBlackboardEntity.GetComponentData<SystemVersionBeforeLiveBake>().version,
            }.Inject(api).ScheduleParallel(m_query, state.Dependency);
        }

        [BurstCompile]
        partial struct Job : IJobChunk, IInjectable
        {
            [ReadOnly, Inject] BufferTypeHandle<UniqueMeshPosition> posHandle;
            [ReadOnly, Inject] BufferTypeHandle<UniqueMeshNormal>   normHandle;
            [ReadOnly, Inject] BufferTypeHandle<UniqueMeshTangent>  tanHandle;
            [ReadOnly, Inject] BufferTypeHandle<UniqueMeshColor>    colHandle;
            [ReadOnly, Inject] BufferTypeHandle<UniqueMeshUv0xy>    uv0Handle;
            [ReadOnly, Inject] BufferTypeHandle<UniqueMeshUv3xyz>   uv3Handle;
            [ReadOnly, Inject] BufferTypeHandle<UniqueMeshIndex>    indexHandle;
            [ReadOnly, Inject] BufferTypeHandle<UniqueMeshSubmesh>  submeshHandle;
            [Inject] ComponentTypeHandle<UniqueMeshConfig>           configHandle;

            public uint lastSystemVersion;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                bool anythingChanged = chunk.DidOrderChange(lastSystemVersion) ||
                                       chunk.DidChange(ref configHandle, lastSystemVersion) ||
                                       chunk.DidChange(ref posHandle, lastSystemVersion) ||
                                       chunk.DidChange(ref normHandle, lastSystemVersion) ||
                                       chunk.DidChange(ref tanHandle, lastSystemVersion) ||
                                       chunk.DidChange(ref colHandle, lastSystemVersion) ||
                                       chunk.DidChange(ref uv0Handle, lastSystemVersion) ||
                                       chunk.DidChange(ref uv3Handle, lastSystemVersion) ||
                                       chunk.DidChange(ref indexHandle, lastSystemVersion) ||
                                       chunk.DidChange(ref submeshHandle, lastSystemVersion);
                if (anythingChanged)
                    chunk.SetComponentEnabledForAll(ref configHandle, true);
            }
        }
    }
}

