using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Lsss.Authoring
{
    [DisallowMultipleComponent]
    [RequiresEntityConversion]
    public class AiShipRadarAuthoring : MonoBehaviour, IConvertGameObjectToEntity
    {
        public float fieldOfView                         = 90f;
        public float range                               = 100f;
        public float preferredTargetDistance             = 15f;
        public float friendlyFireDisableRange            = 100f;
        public float friendlyFireDisableFieldOfView      = 20f;
        public float additionalEnemyDetectionRange       = 50f;
        public float additionalEnemyDetectionFieldOfView = 10f;

        public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
        {
            dstManager.AddComponentData(entity, new AiShipRadar
            {
                distance                             = range,
                cosFov                               = math.cos(math.radians(fieldOfView)),
                preferredTargetDistance              = preferredTargetDistance,
                friendCrossHairsDistanceFilter       = friendlyFireDisableRange,
                friendCrossHairsCosFovFilter         = math.cos(math.radians(friendlyFireDisableFieldOfView)),
                nearestEnemyCrossHairsDistanceFilter = additionalEnemyDetectionRange,
                nearestEnemyCrossHairsCosFovFilter   = math.cos(math.radians(additionalEnemyDetectionFieldOfView))
            });
            dstManager.AddComponent<AiShipRadarScanResults>(entity);
            dstManager.AddComponent<AiRadarTag>(            entity);
        }
    }
}

