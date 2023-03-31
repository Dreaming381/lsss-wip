using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace Lsss.Authoring
{
    [DisallowMultipleComponent]
    [AddComponentMenu("LSSS/Objects/Explosion")]
    public class ExplosionAuthoring : MonoBehaviour
    {
        public float damage             = 100f;
        public float radius             = 10f;
        public float expansionDuration  = 0.1f;
        public float fadeOutDuration    = 0.1f;
        public float totalExplosionTime = 3f;
    }

    public class ExplosionBaker : Baker<ExplosionAuthoring>
    {
        public override void Bake(ExplosionAuthoring authoring)
        {
            AddComponent(new Damage { damage                      = authoring.damage });
            AddComponent(new TimeToLive { timeToLive              = authoring.totalExplosionTime });
            AddComponent(new TimeToLiveFadeStart { fadeTimeWindow = authoring.fadeOutDuration });
            AddComponent(new FadeProperty { fade                  = 1f });
            AddComponent(new ExplosionStats
            {
                radius        = authoring.radius,
                expansionRate = 1f / authoring.expansionDuration
            });

            AddComponent<ExplosionTag>();
        }
    }
}

