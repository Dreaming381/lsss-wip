using Latios.Psyshock;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Lsss
{
    [DisallowMultipleComponent]
    [AddComponentMenu("LSSS/Objects/Arena")]
    public class ArenaAuthoring : MonoBehaviour
    {
        public float radius;
    }

    public class ArenaBaker : Baker<ArenaAuthoring>
    {
        public override void Bake(ArenaAuthoring authoring)
        {
            AddComponent(new ArenaRadius { radius = authoring.radius });
            AddComponent<ArenaTag>();

            float cornerAlongAxis = math.sqrt((authoring.radius * authoring.radius) / 3f);

            if (cornerAlongAxis <= 250f)
            {
                AddComponent(new ArenaCollisionSettings
                {
                    settings = BuildCollisionLayerConfig.defaultSettings
                });
            }
            else
            {
                int subdivisions = Mathf.CeilToInt(cornerAlongAxis / 250f) * 2;
                AddComponent(new ArenaCollisionSettings
                {
                    settings = new CollisionLayerSettings
                    {
                        worldAabb                = new Aabb(-cornerAlongAxis, cornerAlongAxis),
                        worldSubdivisionsPerAxis = new int3(1, subdivisions, subdivisions)
                    }
                });
            }
        }
    }
}

