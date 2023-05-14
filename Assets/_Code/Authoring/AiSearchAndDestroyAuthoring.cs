using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Lsss.Authoring
{
    [DisallowMultipleComponent]
    [AddComponentMenu("LSSS/AI/Search and Destroy Module")]
    public class AiSearchAndDestroyAuthoring : MonoBehaviour
    {
        public float2               targetLeadDistanceMinMax = new float2(0f, 5f);
        public AiShipRadarAuthoring shipRadar;
    }

    public class AiSearchAndDestroyBaker : Baker<AiSearchAndDestroyAuthoring>
    {
        public override void Bake(AiSearchAndDestroyAuthoring authoring)
        {
            var entity                                             = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new AiShipRadarEntity { shipRadar = GetEntity(authoring.shipRadar, TransformUsageFlags.Renderable) });

            AddComponent(entity, new AiSearchAndDestroyPersonalityInitializerValues
            {
                targetLeadDistanceMinMax = authoring.targetLeadDistanceMinMax
            });

            AddComponent<AiSearchAndDestroyPersonality>(entity);
            AddComponent<AiSearchAndDestroyOutput>(     entity);
        }
    }
}

