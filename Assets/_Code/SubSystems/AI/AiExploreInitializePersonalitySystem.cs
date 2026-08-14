using Latios;
using Latios.Calci;
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
    [RequireMatchingQueriesForUpdate]
    [BurstCompile]
    public partial struct AiExploreInitializePersonalitySystem : ISystem, ILatiosApi, ISystemNewScene
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            this.OnCreateForLatios(ref state);
        }

        public void OnNewScene(ref SystemState state) => state.InitSystemRng("AiExploreInitializePersonalitySystem");

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var api = this.GetApi(ref state);
            var ecb = api.syncPoint.CreateEntityCommandBuffer();

            float arenaRadius = api.sceneBlackboardEntity.GetComponentData<ArenaRadius>().radius;
            new Job
            {
                arenaRadius = arenaRadius
            }.ScheduleParallel(api);

            ecb.RemoveComponent<AiExplorePersonalityInitializerValues>(api.GetDefaultQuery<Job>().ToEntityArray(Allocator.Temp));
        }

        [With(typeof(AiTag))]
        [BurstCompile]
        partial struct Job : IJobEach
        {
            public float arenaRadius;

            public void Execute(RngEach rng,
                                ref AiExplorePersonality personality,
                                ref AiExploreState state,
                                in AiExplorePersonalityInitializerValues initalizer,
                                in WorldTransform worldTransform)
            {
                personality.spawnForwardDistance       = rng.NextFloat(initalizer.spawnForwardDistanceMinMax.x, initalizer.spawnForwardDistanceMinMax.y);
                personality.wanderDestinationRadius    = rng.NextFloat(initalizer.wanderDestinationRadiusMinMax.x, initalizer.wanderDestinationRadiusMinMax.y);
                personality.wanderPositionSearchRadius = rng.NextFloat(initalizer.wanderPositionSearchRadiusMinMax.x, initalizer.wanderPositionSearchRadiusMinMax.y);

                var targetPosition   = worldTransform.forwardDirection * (personality.spawnForwardDistance + personality.wanderDestinationRadius) + worldTransform.position;
                var radius           = math.length(targetPosition);
                state.wanderPosition = math.select(targetPosition, targetPosition * arenaRadius / radius, radius > arenaRadius);
            }
        }
    }
}

