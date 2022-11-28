using Latios;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

using static Unity.Entities.SystemAPI;

namespace Lsss
{
    [BurstCompile]
    [RequireMatchingQueriesForUpdate]
    public partial struct AiExploreSystem : ISystem, ISystemNewScene
    {
        LatiosWorldUnmanaged latiosWorld;

        public void OnNewScene(ref SystemState state) => state.EntityManager.AddComponentData(state.SystemHandle, new SystemRng("AiExploreSystem"));

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
                rng         = GetComponentRW<SystemRng>(state.SystemHandle).ValueRW.Shuffle(),
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

            public void Execute(ref AiExploreOutput output, ref AiExploreState state, in AiExplorePersonality personality, in Translation translation)
            {
                if (math.distancesq(translation.Value, state.wanderPosition) < personality.wanderDestinationRadius * personality.wanderDestinationRadius)
                {
                    float maxValidRadius = math.min(personality.wanderPositionSearchRadius, arenaRadius - math.length(translation.Value));
                    if (maxValidRadius < personality.wanderDestinationRadius)
                    {
                        float edge           = math.lerp(personality.wanderDestinationRadius, personality.wanderPositionSearchRadius, 0.1f);
                        state.wanderPosition = edge * math.normalize(-translation.Value) + translation.Value;
                    }
                    else
                    {
                        float radius         = rng.NextFloat(0f, maxValidRadius);
                        state.wanderPosition = rng.NextFloat3Direction() * radius + translation.Value;
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

