using Latios;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace Lsss
{
    public class AiExploreInitializePersonalitySystem : SubSystem
    {
        struct Rng : IComponentData
        {
            public Random random;
        }

        EntityQuery m_query;

        protected override void OnUpdate()
        {
            if (!sceneBlackboardEntity.HasComponent<Rng>())
                sceneBlackboardEntity.AddComponentData(new Rng { random = new Random(34910143) });

            Entity sbe = sceneBlackboardEntity;

            var ecb = latiosWorld.syncPoint.CreateEntityCommandBuffer();

            float arenaRadius = sceneBlackboardEntity.GetComponentData<ArenaRadius>().radius;
            Entities.WithAll<AiTag>().WithStoreEntityQueryInField(ref m_query).ForEach((ref AiExplorePersonality personality,
                                                                                        ref AiExploreState state,
                                                                                        in AiExplorePersonalityInitializerValues initalizer,
                                                                                        in Translation trans,
                                                                                        in Rotation rot) =>
            {
                var random                             = GetComponent<Rng>(sbe).random;
                personality.spawnForwardDistance       = random.NextFloat(initalizer.spawnForwardDistanceMinMax.x, initalizer.spawnForwardDistanceMinMax.y);
                personality.wanderDestinationRadius    = random.NextFloat(initalizer.wanderDestinationRadiusMinMax.x, initalizer.wanderDestinationRadiusMinMax.y);
                personality.wanderPositionSearchRadius = random.NextFloat(initalizer.wanderPositionSearchRadiusMinMax.x, initalizer.wanderPositionSearchRadiusMinMax.y);
                SetComponent(sbe, new Rng { random     = random });

                var targetPosition   = math.forward(rot.Value) * (personality.spawnForwardDistance + personality.wanderDestinationRadius) + trans.Value;
                var radius           = math.length(targetPosition);
                state.wanderPosition = math.select(targetPosition, targetPosition * arenaRadius / radius, radius > arenaRadius);
            }).Schedule();

            ecb.RemoveComponent<AiExplorePersonalityInitializerValues>(m_query);
        }
    }
}

