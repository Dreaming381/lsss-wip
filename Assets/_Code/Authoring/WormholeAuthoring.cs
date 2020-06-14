using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Lsss.Authoring
{
    [DisallowMultipleComponent]
    [RequiresEntityConversion]
    public class WormholeAuthoring : MonoBehaviour, IConvertGameObjectToEntity
    {
        public WormholeAuthoring otherEnd;

        public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
        {
            dstManager.AddComponentData(entity, new WormholeDestination { wormholeDestination = conversionSystem.TryGetPrimaryEntity(otherEnd) });
            dstManager.AddComponent<WormholeTag>(entity);
        }
    }
}

