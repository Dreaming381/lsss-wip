using Latios.Psyshock;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Lsss
{
    [DisallowMultipleComponent]
    [AddComponentMenu("LSSS/Objects/Arena")]
    public class ArenaAuthoring : MonoBehaviour, IConvertGameObjectToEntity
    {
        public float radius;

        public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
        {
            dstManager.AddComponentData(entity, new ArenaRadius { radius = radius });
            dstManager.AddComponent<ArenaTag>(entity);

            float cornerAlongAxis = math.sqrt((radius * radius) / 3f);

            if (cornerAlongAxis <= 250f)
            {
                dstManager.AddComponentData(entity, new ArenaCollisionSettings
                {
                    settings = BuildCollisionLayerConfig.defaultSettings
                });
            }
            else
            {
                int subdivisions = Mathf.CeilToInt(cornerAlongAxis / 250f) * 2;
                dstManager.AddComponentData(entity, new ArenaCollisionSettings
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

