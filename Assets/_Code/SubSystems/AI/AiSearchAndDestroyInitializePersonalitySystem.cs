using Latios;
using Latios.Calci;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

using static Unity.Entities.SystemAPI;

namespace Lsss
{
    [BurstCompile]
    public partial struct AiSearchAndDestroyInitializePersonalitySystem : ISystem, ILatiosApi, ISystemNewScene
    {
        EntityQuery m_query;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            this.OnCreateForLatios(ref state);
            m_query = QueryBuilder().WithAllRW<AiSearchAndDestroyPersonality>().WithAll<AiSearchAndDestroyPersonalityInitializerValues, AiTag>().Build();
        }

        public void OnNewScene(ref SystemState state) => state.InitSystemRng("AiSearchAndDestroyInitializePersonalitySystem");

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var api = this.GetApi(ref state);
            var ecb = api.syncPoint.CreateEntityCommandBuffer();

            new Job
            {
                rng = state.GetJobRng(),
            }.ScheduleParallel(m_query);

            ecb.RemoveComponent<AiSearchAndDestroyPersonalityInitializerValues>(m_query.ToEntityArray(Allocator.Temp));
        }

        [BurstCompile]
        partial struct Job : IJobEntity, IJobEntityChunkBeginEnd
        {
            public SystemRng rng;

            public bool OnChunkBegin(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                rng.BeginChunk(unfilteredChunkIndex);
                return true;
            }

            public void Execute(ref AiSearchAndDestroyPersonality personality, in AiSearchAndDestroyPersonalityInitializerValues initalizer)
            {
                personality.targetLeadDistance = rng.NextFloat(initalizer.targetLeadDistanceMinMax.x, initalizer.targetLeadDistanceMinMax.y);
            }

            public void OnChunkEnd(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask, bool chunkWasExecuted)
            {
            }
        }
    }
}

