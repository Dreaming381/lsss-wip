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
            AddComponent(new Damage { damage = authoring.damage });
            AddComponent<WallTag>();
        }
    }
}

