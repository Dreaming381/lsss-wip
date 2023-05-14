using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Lsss.Authoring
{
    [DisallowMultipleComponent]
    [AddComponentMenu("LSSS/Objects/Obstacle")]
    public class WallAuthoring : MonoBehaviour
    {
        public float damage = 100f;
    }

    public class WallBaker : Baker<WallAuthoring>
    {
        public override void Bake(WallAuthoring authoring)
        {
            var entity                                        = GetEntity(TransformUsageFlags.Renderable);
            AddComponent(         entity, new Damage { damage = authoring.damage });
            AddComponent<WallTag>(entity);
        }
    }
}

