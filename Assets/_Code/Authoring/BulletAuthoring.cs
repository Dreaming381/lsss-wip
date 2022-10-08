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
            float timeToLive                                      = authoring.travelDistance / authoring.speed;
            AddComponent(new TimeToLive { timeToLive              = timeToLive });
            AddComponent(new Speed { speed                        = authoring.speed });
            AddComponent(new Damage { damage                      = authoring.damage });
            AddComponent(new TimeToLiveFadeStart { fadeTimeWindow = authoring.fadeOutDuration });
            AddComponent(new FadeProperty { fade                  = 1f });
            AddComponent<BulletPreviousPosition>();
            AddComponent<BulletFirer>();
            AddComponent<BulletTag>();
        }
    }
}

