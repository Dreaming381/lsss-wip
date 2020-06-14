using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Lsss.Authoring
{
    [DisallowMultipleComponent]
    [RequiresEntityConversion]
    public class AiBrainAuthoring : MonoBehaviour, IConvertGameObjectToEntity
    {
        public float distanceToTravelAfterSpawn = 10f;
        public float destinationRadius          = 5f;
        public float targetLeadDistance         = 15f;

        public AiShipRadarAuthoring shipRadar;

        public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
        {
            dstManager.AddComponentData(entity, new AiPersonality
            {
                spawnForwardDistance = distanceToTravelAfterSpawn,
                destinationRadius    = destinationRadius,
                targetLeadDistance   = targetLeadDistance
            });

            dstManager.AddComponentData(entity, new AiBrain
            {
                shipRadar = conversionSystem.GetPrimaryEntity(shipRadar)
            });

            dstManager.AddComponent<AiDestination>(   entity);
            dstManager.AddComponent<AiWantsToFire>(   entity);

            dstManager.AddComponent<AiInitializeTag>( entity);
            dstManager.AddComponent<AiTag>(           entity);
        }
    }
}

