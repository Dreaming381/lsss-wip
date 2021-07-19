using Latios;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace Lsss
{
    public class AiSearchAndDestroyInitializePersonalitySystem : SubSystem
    {
        struct AiRng : IComponentData
        {
            public Rng rng;
        }

        EntityQuery m_query;

        public override void OnNewScene() => sceneBlackboardEntity.AddComponentData(new AiRng { rng = new Rng("AiSearchAndDestroyInitializePersonalitySystem") });

        protected override void OnUpdate()
        {
            var rng                                                = sceneBlackboardEntity.GetComponentData<AiRng>().rng.Update();
            sceneBlackboardEntity.SetComponentData(new AiRng { rng = rng });

            var ecb = latiosWorld.syncPoint.CreateEntityCommandBuffer();

            Entities.WithAll<AiTag>().WithStoreEntityQueryInField(ref m_query).ForEach((int entityInQueryIndex, ref AiSearchAndDestroyPersonality personality,
                                                                                        in AiSearchAndDestroyPersonalityInitializerValues initalizer) =>
            {
                var random                     = rng.GetSequence(entityInQueryIndex);
                personality.targetLeadDistance = random.NextFloat(initalizer.targetLeadDistanceMinMax.x, initalizer.targetLeadDistanceMinMax.y);
            }).ScheduleParallel();

            ecb.RemoveComponent<AiSearchAndDestroyPersonalityInitializerValues>(m_query);
        }
    }
}

