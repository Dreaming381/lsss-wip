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
    [RequireMatchingQueriesForUpdate]
    [BurstCompile]
    public partial struct AiExploreInitializePersonalitySystem : ISystem, ISystemNewScene
    {
        EntityQuery m_query;

        LatiosWorldUnmanaged latiosWorld;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            latiosWorld = state.GetLatiosWorldUnmanaged();
            m_query     =
                QueryBuilder().WithAllRW<AiExplorePersonality, AiExploreState>().WithAll<AiExplorePersonalityInitializerValues, WorldTransform, AiTag>().Build();
        }

        public void OnNewScene(ref SystemState state) => state.InitSystemRng("AiExploreInitializePersonalitySystem");

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = latiosWorld.syncPoint.CreateEntityCommandBuffer();

            float arenaRadius = latiosWorld.sceneBlackboardEntity.GetComponentData<ArenaRadius>().radius;
            new Job
            {
                rng         = state.GetJobRng(),
                arenaRadius = arenaRadius
            }.ScheduleParallel(m_query);

            ecb.RemoveComponent<AiExplorePersonalityInitializerValues>(m_query.ToEntityArray(Allocator.Temp));
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }

        [BurstCompile]
        partial struct Job : IJobEntity, IJobEntityChunkBeginEnd
        {
            public SystemRng rng;
            public float     arenaRadius;

            public bool OnChunkBegin(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                rng.BeginChunk(unfilteredChunkIndex);
                return true;
            }

            public void Execute(ref AiExplorePersonality personality,
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

            public void OnChunkEnd(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask, bool chunkWasExecuted)
            {
            }
        }
    }
}

