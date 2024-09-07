using Latios;
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
    public partial struct AiSearchAndDestroyInitializePersonalitySystem : ISystem, ISystemNewScene
    {
        EntityQuery m_query;

        LatiosWorldUnmanaged latiosWorld;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            m_query = QueryBuilder().WithAllRW<AiSearchAndDestroyPersonality>().WithAll<AiSearchAndDestroyPersonalityInitializerValues, AiTag>().Build();

            latiosWorld = state.GetLatiosWorldUnmanaged();
        }

        public void OnNewScene(ref SystemState state) => state.InitSystemRng("AiSearchAndDestroyInitializePersonalitySystem");

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = latiosWorld.syncPoint.CreateEntityCommandBuffer();

            new Job
            {
                rng = state.GetJobRng(),
            }.ScheduleParallel(m_query);

            ecb.RemoveComponent<AiSearchAndDestroyPersonalityInitializerValues>(m_query.ToEntityArray(Allocator.Temp));
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
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

