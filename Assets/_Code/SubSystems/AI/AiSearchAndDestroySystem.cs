using Latios;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace Lsss
{
    [BurstCompile]
    public partial struct AiSearchAndDestroySystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
        }
        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var scanCdfe = state.GetComponentDataFromEntity<AiShipRadarScanResults>(true);
            state.Entities.WithAll<AiTag>().ForEach((ref AiSearchAndDestroyOutput output, in AiSearchAndDestroyPersonality personality, in AiShipRadarEntity shipRadarEntity) =>
            {
                var scanResults = scanCdfe[shipRadarEntity.shipRadar];
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
            }).WithReadOnly(scanCdfe).ScheduleParallel();

            state.Entities.WithAll<AiRadarTag>().ForEach((ref AiShipRadar radar, in AiShipRadarScanResults results) =>
            {
                radar.target = results.target;
            }).ScheduleParallel();
        }
    }
}

