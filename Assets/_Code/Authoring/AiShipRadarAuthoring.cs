using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Lsss.Authoring
{
    [DisallowMultipleComponent]
    [AddComponentMenu("LSSS/AI/Radar")]
    public class AiShipRadarAuthoring : MonoBehaviour, IConvertGameObjectToEntity
    {
        public float fieldOfView                    = 90f;
        public float range                          = 100f;
        public float preferredTargetDistance        = 15f;
        public float friendlyFireDisableRange       = 100f;
        public float friendlyFireDisableFieldOfView = 20f;
        public float enemyCrossHairsRange           = 50f;
        public float enemyCrossHairsFieldOfView     = 10f;
        public bool  biasCrossHairsUsingRootForward = false;

        public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
        {
            quaternion crossHairsForwardDirectionBias = quaternion.identity;
            if (biasCrossHairsUsingRootForward)
                crossHairsForwardDirectionBias = quaternion.LookRotationSafe(transform.InverseTransformDirection(transform.root.forward), transform.up);
            dstManager.AddComponentData(entity, new AiShipRadar
            {
                distance                             = range,
                cosFov                               = math.cos(math.radians(fieldOfView) / 2f),
                preferredTargetDistance              = preferredTargetDistance,
                friendCrossHairsDistanceFilter       = friendlyFireDisableRange,
                friendCrossHairsCosFovFilter         = math.cos(math.radians(friendlyFireDisableFieldOfView) / 2f),
                nearestEnemyCrossHairsDistanceFilter = enemyCrossHairsRange,
                nearestEnemyCrossHairsCosFovFilter   = math.cos(math.radians(enemyCrossHairsFieldOfView) / 2f),
                crossHairsForwardDirectionBias       = crossHairsForwardDirectionBias
            });
            dstManager.AddComponent<AiShipRadarScanResults>(entity);
            dstManager.AddComponent<AiRadarTag>(            entity);
        }

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
}

