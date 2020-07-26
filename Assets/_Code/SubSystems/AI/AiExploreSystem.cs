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
        struct Rng : IComponentData
        {
            public Random random;
        }

        protected override void OnUpdate()
        {
            if (!sceneGlobalEntity.HasComponentData<Rng>())
                sceneGlobalEntity.AddComponentData(new Rng { random = new Random(06117105) });

            float  arenaRadius = sceneGlobalEntity.GetComponentData<ArenaRadius>().radius;
            Entity sge         = sceneGlobalEntity;

            Entities.WithAll<AiTag>().ForEach((ref AiExploreOutput output, ref AiExploreState state, in AiExplorePersonality personality, in Translation translation) =>
            {
                if (math.distancesq(translation.Value, state.wanderPosition) < personality.wanderDestinationRadius * personality.wanderDestinationRadius)
                {
                    var   random         = GetComponent<Rng>(sge).random;
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

                    SetComponent(sge, new Rng { random = random });
                }
                output.wanderPosition      = state.wanderPosition;
                output.wanderPositionValid = true;
            }).Schedule();
        }
    }
}

