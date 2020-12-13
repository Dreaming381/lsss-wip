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

        //Todo: Add CollisionLayerSettings if necessary.

        public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
        {
            dstManager.AddComponentData(entity, new ArenaRadius { radius = radius });
            dstManager.AddComponent<ArenaTag>(entity);
        }
    }
}

