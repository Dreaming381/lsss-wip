using Latios.Transforms.Authoring;
using Unity.Entities;
using UnityEngine;

namespace Lsss.Authoring
{
    [DisallowMultipleComponent]
    [AddComponentMenu("LSSS/Objects/Bullet")]
    public class BulletAuthoring : MonoBehaviour
    {
        public float travelDistance  = 100f;
        public float speed           = 25f;
        public float damage          = 10f;
        public float fadeOutDuration = 0.1f;
    }

    public class BulletBaker : Baker<BulletAuthoring>
    {
        public override void Bake(BulletAuthoring authoring)
        {
            var   entity                                                                   = GetEntity(TransformUsageFlags.Dynamic);
            float timeToLive                                                               = authoring.travelDistance / authoring.speed;
            AddComponent(                 entity, new TimeToLive { timeToLive              = timeToLive });
            AddComponent(                 entity, new Speed { speed                        = authoring.speed });
            AddComponent(                 entity, new Damage { damage                      = authoring.damage });
            AddComponent(                 entity, new TimeToLiveFadeStart { fadeTimeWindow = authoring.fadeOutDuration });
            AddComponent(                 entity, new FadeProperty { fade                  = 1f });
            AddComponent<PreviousRequest>(entity);
            AddComponent<BulletFirer>(    entity);
            AddComponent<BulletTag>(      entity);
        }

        [BakingType]
        struct PreviousRequest : IRequestPreviousTransform { }
    }
}

