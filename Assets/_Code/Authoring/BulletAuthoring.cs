using Unity.Entities;
using UnityEngine;

namespace Lsss.Authoring
{
    [DisallowMultipleComponent]
    public class BulletAuthoring : MonoBehaviour, IConvertGameObjectToEntity
    {
        public float travelDistance  = 100f;
        public float speed           = 25f;
        public float damage          = 10f;
        public float fadeOutDuration = 0.1f;

        public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
        {
            float timeToLive                                                             = travelDistance / speed;
            dstManager.AddComponentData(entity, new TimeToLive { timeToLive              = timeToLive });
            dstManager.AddComponentData(entity, new Speed { speed                        = speed });
            dstManager.AddComponentData(entity, new Damage { damage                      = damage });
            dstManager.AddComponentData(entity, new TimeToLiveFadeStart { fadeTimeWindow = fadeOutDuration });
            dstManager.AddComponentData(entity, new FadeProperty { fade                  = 1f });
            dstManager.AddComponent<BulletPreviousPosition>(entity);
            dstManager.AddComponent<BulletFirer>(           entity);
            dstManager.AddComponent<BulletTag>(             entity);
        }
    }
}

