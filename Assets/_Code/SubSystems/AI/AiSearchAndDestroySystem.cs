using Latios;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace Lsss
{
    public partial class AiSearchAndDestroySystem : SubSystem
    {
        protected override void OnUpdate()
        {
            Entities.WithAll<AiTag>().ForEach((ref AiSearchAndDestroyOutput output, in AiSearchAndDestroyPersonality personality, in AiShipRadarEntity shipRadarEntity) =>
            {
                var scanResults = GetComponent<AiShipRadarScanResults>(shipRadarEntity.shipRadar);
                output.fire     = !scanResults.friendFound && scanResults.nearestEnemy != Entity.Null;

                if (scanResults.target != Entity.Null)
                {
                    output.flyTowardsPosition = math.forward(scanResults.targetTransform.rot) * personality.targetLeadDistance + scanResults.targetTransform.pos;
                    output.isPositionValid    = true;
                }
                else
                {
                    output.isPositionValid = false;
                }
            }).ScheduleParallel();

            Entities.WithAll<AiRadarTag>().ForEach((ref AiShipRadar radar, in AiShipRadarScanResults results) =>
            {
                radar.target = results.target;
            }).ScheduleParallel();
        }
    }
}

