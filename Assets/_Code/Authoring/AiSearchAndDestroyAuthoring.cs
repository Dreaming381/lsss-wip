using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Lsss.Authoring
{
    [DisallowMultipleComponent]
    [AddComponentMenu("LSSS/AI/Search and Destroy Module")]
    public class AiSearchAndDestroyAuthoring : MonoBehaviour, IConvertGameObjectToEntity
    {
        public float2               targetLeadDistanceMinMax = new float2(0f, 5f);
        public AiShipRadarAuthoring shipRadar;

        public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
        {
            if (!dstManager.HasComponent<AiShipRadarEntity>(entity))
            {
                dstManager.AddComponentData(entity, new AiShipRadarEntity { shipRadar = conversionSystem.GetPrimaryEntity(shipRadar) });
            }

            dstManager.AddComponentData(entity, new AiSearchAndDestroyPersonalityInitializerValues
            {
                targetLeadDistanceMinMax = targetLeadDistanceMinMax
            });

            dstManager.AddComponent<AiSearchAndDestroyPersonality>(entity);
            dstManager.AddComponent<AiSearchAndDestroyOutput>(     entity);
        }
    }
}

