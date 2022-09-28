using Latios;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace Lsss
{
    [BurstCompile]
    [RequireMatchingQueriesForUpdate]
    public partial struct AiExploreSystem : ISystem, ISystemNewScene
    {
        struct AiRng : IComponentData
        {
            public Rng rng;
        }

        public void OnNewScene(ref SystemState state)
        {
            state.GetSceneBlackboardEntity().AddComponentData(new AiRng { rng = new Rng("AiExploreSystem") });
        }

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
        }
        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var rng                                                           = state.GetSceneBlackboardEntity().GetComponentData<AiRng>().rng.Shuffle();
            state.GetSceneBlackboardEntity().SetComponentData(new AiRng { rng = rng });
            new Job
            {
                arenaRadius = state.GetSceneBlackboardEntity().GetComponentData<ArenaRadius>().radius,
                rng         = rng
            }.ScheduleParallel();
        }

        [BurstCompile]
        [WithAll(typeof(AiTag))]
        partial struct Job : IJobEntity
        {
            public float arenaRadius;
            public Rng   rng;

            public void Execute([EntityInQueryIndex] int entityInQueryIndex, ref AiExploreOutput output, ref AiExploreState state, in AiExplorePersonality personality,
                                in Translation translation)
            {
                if (math.distancesq(translation.Value, state.wanderPosition) < personality.wanderDestinationRadius * personality.wanderDestinationRadius)
                {
                    var   random         = rng.GetSequence(entityInQueryIndex);
                    float maxValidRadius = math.min(personality.wanderPositionSearchRadius, arenaRadius - math.length(translation.Value));
                    if (maxValidRadius < personality.wanderDestinationRadius)
                    {
                        float edge           = math.lerp(personality.wanderDestinationRadius, personality.wanderPositionSearchRadius, 0.1f);
                        state.wanderPosition = edge * math.normalize(-translation.Value) + translation.Value;
                    }
                    else
                    {
                        float radius         = random.NextFloat(0f, maxValidRadius);
                        state.wanderPosition = random.NextFloat3Direction() * radius + translation.Value;
                    }
                }
                output.wanderPosition      = state.wanderPosition;
                output.wanderPositionValid = true;
            }
        }
    }
}

