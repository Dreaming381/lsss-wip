using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Lsss.Authoring
{
    [DisallowMultipleComponent]
    [AddComponentMenu("LSSS/AI/Radar")]
    public class AiShipRadarAuthoring : MonoBehaviour
    {
        public float fieldOfView                    = 90f;
        public float range                          = 100f;
        public float preferredTargetDistance        = 15f;
        public float friendlyFireDisableRange       = 100f;
        public float friendlyFireDisableFieldOfView = 20f;
        public float enemyCrossHairsRange           = 50f;
        public float enemyCrossHairsFieldOfView     = 10f;
        public bool  biasCrossHairsUsingRootForward = false;

        private void OnDrawGizmos()
        {
            var mBack = Gizmos.matrix;
            if (!biasCrossHairsUsingRootForward)
                Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color      = Color.green;
            Gizmos.DrawFrustum(float3.zero, friendlyFireDisableFieldOfView, friendlyFireDisableRange, 0.01f, 1f);
            Gizmos.color = Color.red;
            Gizmos.DrawFrustum(float3.zero, enemyCrossHairsFieldOfView,     enemyCrossHairsRange,     0.01f, 1f);
            Gizmos.matrix = transform.localToWorldMatrix;

            Gizmos.color = Color.blue;
            Gizmos.DrawFrustum(float3.zero, fieldOfView, range, 0.01f, 1f);

            Gizmos.color = Color.white;
            float3 bias  = biasCrossHairsUsingRootForward ? transform.root.forward : transform.forward;
            bias         = transform.InverseTransformDirection(bias);
            Gizmos.DrawRay(float3.zero, bias * range);
            Gizmos.matrix = mBack;
        }
    }

    public class AiShipRadarBaker : Baker<AiShipRadarAuthoring>
    {
        public override void Bake(AiShipRadarAuthoring authoring)
        {
            var        entity                         = GetEntity(TransformUsageFlags.Renderable);
            quaternion crossHairsForwardDirectionBias = quaternion.identity;
            if (authoring.biasCrossHairsUsingRootForward)
            {
                var transform                  = GetComponent<Transform>();
                crossHairsForwardDirectionBias = quaternion.LookRotationSafe(transform.InverseTransformDirection(transform.root.forward), transform.up);
            }
            AddComponent(entity, new AiShipRadar
            {
                distance                             = authoring.range,
                cosFov                               = math.cos(math.radians(authoring.fieldOfView) / 2f),
                preferredTargetDistance              = authoring.preferredTargetDistance,
                friendCrossHairsDistanceFilter       = authoring.friendlyFireDisableRange,
                friendCrossHairsCosFovFilter         = math.cos(math.radians(authoring.friendlyFireDisableFieldOfView) / 2f),
                nearestEnemyCrossHairsDistanceFilter = authoring.enemyCrossHairsRange,
                nearestEnemyCrossHairsCosFovFilter   = math.cos(math.radians(authoring.enemyCrossHairsFieldOfView) / 2f),
                crossHairsForwardDirectionBias       = crossHairsForwardDirectionBias
            });
            AddComponent<AiShipRadarScanResults>(       entity);
            AddComponent<AiRadarTag>(                   entity);
            AddComponent<AiShipRadarNeedsFullScanFlag>( entity);
            AddComponent<AiShipRadarRequests>(          entity);
        }
    }
}

