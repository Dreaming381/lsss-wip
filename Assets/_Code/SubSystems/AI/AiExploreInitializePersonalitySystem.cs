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

        BeginInitializationEntityCommandBufferSystem m_ecbSystem;

        EntityQuery m_query;

        protected override void OnCreate()
        {
            m_ecbSystem = World.GetExistingSystem<BeginInitializationEntityCommandBufferSystem>();
        }

        protected override void OnUpdate()
        {
            if (!sceneGlobalEntity.HasComponentData<Rng>())
                sceneGlobalEntity.AddComponentData(new Rng { random = new Random(34910143) });

            Entity sge = sceneGlobalEntity;

            var ecb = m_ecbSystem.CreateCommandBuffer();

            float arenaRadius = sceneGlobalEntity.GetComponentData<ArenaRadius>().radius;
            Entities.WithAll<AiTag>().WithStoreEntityQueryInField(ref m_query).ForEach((ref AiExplorePersonality personality,
                                                                                        ref AiExploreState state,
                                                                                        in AiExplorePersonalityInitializerValues initalizer,
                                                                                        in Translation trans,
                                                                                        in Rotation rot) =>
            {
                var random                             = GetComponent<Rng>(sge).random;
                personality.spawnForwardDistance       = random.NextFloat(initalizer.spawnForwardDistanceMinMax.x, initalizer.spawnForwardDistanceMinMax.y);
                personality.wanderDestinationRadius    = random.NextFloat(initalizer.wanderDestinationRadiusMinMax.x, initalizer.wanderDestinationRadiusMinMax.y);
                personality.wanderPositionSearchRadius = random.NextFloat(initalizer.wanderPositionSearchRadiusMinMax.x, initalizer.wanderPositionSearchRadiusMinMax.y);
                SetComponent(sge, new Rng { random     = random });

                var targetPosition   = math.forward(rot.Value) * (personality.spawnForwardDistance + personality.wanderDestinationRadius) + trans.Value;
                var radius           = math.length(targetPosition);
                state.wanderPosition = math.select(targetPosition, targetPosition * arenaRadius / radius, radius > arenaRadius);
            }).Schedule();

            ecb.RemoveComponent<AiExplorePersonalityInitializerValues>(m_query);
            m_ecbSystem.AddJobHandleForProducer(Dependency);
        }
    }
}

