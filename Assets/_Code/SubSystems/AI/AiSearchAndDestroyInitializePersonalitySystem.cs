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
        struct Rng : IComponentData
        {
            public Random random;
        }

        EntityQuery m_query;

        protected override void OnUpdate()
        {
            if (!sceneGlobalEntity.HasComponentData<Rng>())
                sceneGlobalEntity.AddComponentData(new Rng { random = new Random(5417) });

            Entity sge = sceneGlobalEntity;

            var ecb = latiosWorld.SyncPoint.CreateEntityCommandBuffer();

            Entities.WithAll<AiTag>().WithStoreEntityQueryInField(ref m_query).ForEach((ref AiSearchAndDestroyPersonality personality,
                                                                                        in AiSearchAndDestroyPersonalityInitializerValues initalizer) =>
            {
                var random                         = GetComponent<Rng>(sge).random;
                personality.targetLeadDistance     = random.NextFloat(initalizer.targetLeadDistanceMinMax.x, initalizer.targetLeadDistanceMinMax.y);
                SetComponent(sge, new Rng { random = random });
            }).Schedule();

            ecb.RemoveComponent<AiSearchAndDestroyPersonalityInitializerValues>(m_query);
        }
    }
}

