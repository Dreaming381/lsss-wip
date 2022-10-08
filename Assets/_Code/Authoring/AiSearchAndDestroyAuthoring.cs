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
            AddComponent(new AiShipRadarEntity { shipRadar = GetEntity(authoring.shipRadar) });

            AddComponent(new AiSearchAndDestroyPersonalityInitializerValues
            {
                targetLeadDistanceMinMax = authoring.targetLeadDistanceMinMax
            });

            AddComponent<AiSearchAndDestroyPersonality>();
            AddComponent<AiSearchAndDestroyOutput>();
        }
    }
}

