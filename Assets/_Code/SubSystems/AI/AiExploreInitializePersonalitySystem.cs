using Latios;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace Lsss
{
    public partial class AiExploreInitializePersonalitySystem : SubSystem
    {
        struct AiRng : IComponentData
        {
            public Rng rng;
        }

        EntityQuery m_query;

        public override void OnNewScene() => sceneBlackboardEntity.AddComponentData(new AiRng { rng = new Rng("AiExploreInitializePersonalitySystem") });

        protected override void OnUpdate()
        {
            var rng                                                = sceneBlackboardEntity.GetComponentData<AiRng>().rng.Shuffle();
            sceneBlackboardEntity.SetComponentData(new AiRng { rng = rng });

            var ecb = latiosWorld.syncPoint.CreateEntityCommandBuffer();

            float arenaRadius = sceneBlackboardEntity.GetComponentData<ArenaRadius>().radius;
            Entities.WithAll<AiTag>().WithStoreEntityQueryInField(ref m_query).ForEach((int entityInQueryIndex,
                                                                                        ref AiExplorePersonality personality,
                                                                                        ref AiExploreState state,
                                                                                        in AiExplorePersonalityInitializerValues initalizer,
                                                                                        in Translation trans,
                                                                                        in Rotation rot) =>
            {
                var random                             = rng.GetSequence(entityInQueryIndex);
                personality.spawnForwardDistance       = random.NextFloat(initalizer.spawnForwardDistanceMinMax.x, initalizer.spawnForwardDistanceMinMax.y);
                personality.wanderDestinationRadius    = random.NextFloat(initalizer.wanderDestinationRadiusMinMax.x, initalizer.wanderDestinationRadiusMinMax.y);
                personality.wanderPositionSearchRadius = random.NextFloat(initalizer.wanderPositionSearchRadiusMinMax.x, initalizer.wanderPositionSearchRadiusMinMax.y);

                var targetPosition   = math.forward(rot.Value) * (personality.spawnForwardDistance + personality.wanderDestinationRadius) + trans.Value;
                var radius           = math.length(targetPosition);
                state.wanderPosition = math.select(targetPosition, targetPosition * arenaRadius / radius, radius > arenaRadius);
            }).ScheduleParallel();

            ecb.RemoveComponentForEntityQuery<AiExplorePersonalityInitializerValues>(m_query);
        }
    }
}

