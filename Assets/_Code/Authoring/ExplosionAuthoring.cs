using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace Lsss.Authoring
{
    [DisallowMultipleComponent]
    [RequiresEntityConversion]
    public class ExplosionAuthoring : MonoBehaviour, IConvertGameObjectToEntity
    {
        public float damage             = 100f;
        public float radius             = 10f;
        public float expansionDuration  = 0.1f;
        public float fadeOutDuration    = 0.1f;
        public float totalExplosionTime = 3f;

        public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
        {
            dstManager.AddComponentData(entity, new Damage { damage                      = damage });
            dstManager.AddComponentData(entity, new TimeToLive { timeToLive              = totalExplosionTime });
            dstManager.AddComponentData(entity, new TimeToLiveFadeStart { fadeTimeWindow = fadeOutDuration });
            dstManager.AddComponentData(entity, new FadeProperty { fade                  = 1f });
            dstManager.AddComponentData(entity, new ExplosionStats
            {
                radius        = radius,
                expansionRate = 1f / expansionDuration
            });
            if (dstManager.HasComponent<NonUniformScale>(entity))
                dstManager.RemoveComponent<NonUniformScale>(entity);
            if (!dstManager.HasComponent<Scale>(entity))
                dstManager.AddComponent<Scale>(entity);
            dstManager.SetComponentData(entity, new Scale { Value = 0f });

            dstManager.AddComponent<ExplosionTag>(entity);
        }
    }
}

