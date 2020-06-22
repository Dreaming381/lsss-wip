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
        public float             swarchschildRadius    = 0.1f;
        public float             maxW                  = 5f;
        public float             gravityWarpZoneRadius = 10f;

        public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
        {
            dstManager.AddComponentData(entity, new WormholeDestination { wormholeDestination = conversionSystem.TryGetPrimaryEntity(otherEnd) });
            dstManager.AddComponent<WormholeTag>(entity);

            dstManager.AddComponentData(entity, new GravityWarpZone
            {
                swarchschildRadius = swarchschildRadius,
                maxW               = maxW
            });
            dstManager.AddComponentData(entity, new GravityWarpZoneRadius { radius = gravityWarpZoneRadius });
            dstManager.AddComponent<GravityWarpZoneTag>(entity);
        }
    }
}

