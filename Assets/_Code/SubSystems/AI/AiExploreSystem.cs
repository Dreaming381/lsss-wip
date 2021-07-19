using Latios;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace Lsss
{
    public class AiExploreSystem : SubSystem
    {
        struct AiRng : IComponentData
        {
            public Rng rng;
        }

        public override void OnNewScene() => sceneBlackboardEntity.AddComponentData(new AiRng { rng = new Rng("AiExploreSystem") });

        protected override void OnUpdate()
        {
            float arenaRadius                                      = sceneBlackboardEntity.GetComponentData<ArenaRadius>().radius;
            var   rng                                              = sceneBlackboardEntity.GetComponentData<AiRng>().rng.Update();
            sceneBlackboardEntity.SetComponentData(new AiRng { rng = rng });

            Entities.WithAll<AiTag>().ForEach((int entityInQueryIndex, ref AiExploreOutput output, ref AiExploreState state, in AiExplorePersonality personality,
                                               in Translation translation) =>
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
            }).ScheduleParallel();
        }
    }
}

