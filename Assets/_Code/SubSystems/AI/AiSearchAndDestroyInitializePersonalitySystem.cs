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
    public partial struct AiSearchAndDestroyInitializePersonalitySystem : ISystem, ISystemNewScene
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
            m_query = SystemAPI.QueryBuilder().WithAllRW<AiSearchAndDestroyPersonality>().WithAll<AiSearchAndDestroyPersonalityInitializerValues, AiTag>().Build();

            latiosWorld = state.GetLatiosWorldUnmanaged();
        }

        public void OnNewScene(ref SystemState state) => latiosWorld.sceneBlackboardEntity.AddComponentData(new AiRng {
            rng = new Rng("AiSearchAndDestroyInitializePersonalitySystem")
        });

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var rng                                                            = latiosWorld.sceneBlackboardEntity.GetComponentData<AiRng>().rng.Shuffle();
            latiosWorld.sceneBlackboardEntity.SetComponentData(new AiRng { rng = rng });

            var ecb = latiosWorld.syncPoint.CreateEntityCommandBuffer();

            new Job { rng = rng }.ScheduleParallel(m_query);

            ecb.RemoveComponentForEntityQuery<AiSearchAndDestroyPersonalityInitializerValues>(m_query);
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }

        [BurstCompile]
        partial struct Job : IJobEntity
        {
            public Rng rng;

            public void Execute([EntityInQueryIndex] int entityInQueryIndex, ref AiSearchAndDestroyPersonality personality,
                                in AiSearchAndDestroyPersonalityInitializerValues initalizer)
            {
                var random                     = rng.GetSequence(entityInQueryIndex);
                personality.targetLeadDistance = random.NextFloat(initalizer.targetLeadDistanceMinMax.x, initalizer.targetLeadDistanceMinMax.y);
            }
        }
    }
}

