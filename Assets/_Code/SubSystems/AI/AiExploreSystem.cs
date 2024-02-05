using Latios;
using Latios.Transforms;
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
    [RequireMatchingQueriesForUpdate]
    public partial struct AiExploreSystem : ISystem, ISystemNewScene
    {
        LatiosWorldUnmanaged latiosWorld;

        public void OnNewScene(ref SystemState state) => state.InitSystemRng("AiExploreSystem");

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            latiosWorld = state.GetLatiosWorldUnmanaged();
        }
        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            new Job
            {
                arenaRadius = latiosWorld.sceneBlackboardEntity.GetComponentData<ArenaRadius>().radius,
                rng         = state.GetJobRng(),
            }.ScheduleParallel();
        }

        [BurstCompile]
        [WithAll(typeof(AiTag))]
        partial struct Job : IJobEntity, IJobEntityChunkBeginEnd
        {
            public float     arenaRadius;
            public SystemRng rng;

            public bool OnChunkBegin(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                rng.BeginChunk(unfilteredChunkIndex);
                return true;
            }

            public void Execute(ref AiExploreOutput output, ref AiExploreState state, in AiExplorePersonality personality, in WorldTransform worldTransform)
            {
                if (math.distancesq(worldTransform.position, state.wanderPosition) < personality.wanderDestinationRadius * personality.wanderDestinationRadius)
                {
                    float maxValidRadius = math.min(personality.wanderPositionSearchRadius, arenaRadius - math.length(worldTransform.position));
                    if (maxValidRadius < personality.wanderDestinationRadius)
                    {
                        float edge           = math.lerp(personality.wanderDestinationRadius, personality.wanderPositionSearchRadius, 0.1f);
                        state.wanderPosition = edge * math.normalize(-worldTransform.position) + worldTransform.position;
                    }
                    else
                    {
                        float radius         = rng.NextFloat(0f, maxValidRadius);
                        state.wanderPosition = rng.NextFloat3Direction() * radius + worldTransform.position;
                    }
                }
                output.wanderPosition      = state.wanderPosition;
                output.wanderPositionValid = true;
            }

            public void OnChunkEnd(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask, bool chunkWasExecuted)
            {
            }
        }
    }
}

