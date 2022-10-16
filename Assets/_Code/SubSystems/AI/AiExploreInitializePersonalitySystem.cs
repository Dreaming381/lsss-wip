using Latios;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace Lsss
{
    [RequireMatchingQueriesForUpdate]
    [BurstCompile]
    public partial struct AiExploreInitializePersonalitySystem : ISystem, ISystemNewScene
    {
        struct AiRng : IComponentData
        {
            public Rng rng;
        }

        EntityQuery m_query;

        LatiosWorldUnmanaged latiosWorld;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            latiosWorld = state.GetLatiosWorldUnmanaged();
            m_query     =
                SystemAPI.QueryBuilder().WithAllRW<AiExplorePersonality, AiExploreState>().WithAll<AiExplorePersonalityInitializerValues, Translation, Rotation, AiTag>().Build();
        }

        public void OnNewScene(ref SystemState state) => latiosWorld.sceneBlackboardEntity.AddComponentData(new AiRng { rng = new Rng("AiExploreInitializePersonalitySystem") });

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var rng                                                            = latiosWorld.sceneBlackboardEntity.GetComponentData<AiRng>().rng.Shuffle();
            latiosWorld.sceneBlackboardEntity.SetComponentData(new AiRng { rng = rng });

            var ecb = latiosWorld.syncPoint.CreateEntityCommandBuffer();

            float arenaRadius = latiosWorld.sceneBlackboardEntity.GetComponentData<ArenaRadius>().radius;
            new Job { rng     = rng, arenaRadius = arenaRadius }.ScheduleParallel(m_query);

            ecb.RemoveComponentForEntityQuery<AiExplorePersonalityInitializerValues>(m_query);
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }

        [BurstCompile]
        partial struct Job : IJobEntity
        {
            public Rng   rng;
            public float arenaRadius;

            public void Execute([EntityInQueryIndex] int entityInQueryIndex,
                                ref AiExplorePersonality personality,
                                ref AiExploreState state,
                                in AiExplorePersonalityInitializerValues initalizer,
                                in Translation trans,
                                in Rotation rot)
            {
                var random                             = rng.GetSequence(entityInQueryIndex);
                personality.spawnForwardDistance       = random.NextFloat(initalizer.spawnForwardDistanceMinMax.x, initalizer.spawnForwardDistanceMinMax.y);
                personality.wanderDestinationRadius    = random.NextFloat(initalizer.wanderDestinationRadiusMinMax.x, initalizer.wanderDestinationRadiusMinMax.y);
                personality.wanderPositionSearchRadius = random.NextFloat(initalizer.wanderPositionSearchRadiusMinMax.x, initalizer.wanderPositionSearchRadiusMinMax.y);

                var targetPosition   = math.forward(rot.Value) * (personality.spawnForwardDistance + personality.wanderDestinationRadius) + trans.Value;
                var radius           = math.length(targetPosition);
                state.wanderPosition = math.select(targetPosition, targetPosition * arenaRadius / radius, radius > arenaRadius);
            }
        }
    }
}

