using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Lsss.Authoring
{
    [DisallowMultipleComponent]
    [RequiresEntityConversion]
    public class WallAuthoring : MonoBehaviour, IConvertGameObjectToEntity
    {
        public float damage = 100f;

        public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
        {
            dstManager.AddComponentData(entity, new Damage { damage = damage });
            dstManager.AddComponent<WallTag>(entity);
        }
    }
}

