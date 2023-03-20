using Latios;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

using static Unity.Entities.SystemAPI;

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
            var jobA               = new JobA();
            jobA.scanResultsLookup = GetComponentLookup<AiShipRadarScanResults>(true);
            jobA.ScheduleParallel();
            new JobB().ScheduleParallel();
        }

        [BurstCompile]
        [WithAll(typeof(AiTag))]
        partial struct JobA : IJobEntity
        {
            [ReadOnly] public ComponentLookup<AiShipRadarScanResults> scanResultsLookup;
            public void Execute(ref AiSearchAndDestroyOutput output, in AiSearchAndDestroyPersonality personality, in AiShipRadarEntity shipRadarEntity)
            {
                // !!!!!!!!!UNITY FIX THIS!!!!!!!!!!!!!!!!
                // var scanResults = GetComponent<AiShipRadarScanResults>(shipRadarEntity.shipRadar);
                //var scanResults = GetComponentLookup<AiShipRadarScanResults>(true)[shipRadarEntity.shipRadar];
                var scanResults = scanResultsLookup[shipRadarEntity.shipRadar];
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
            }
        }

        [BurstCompile]
        [WithAll(typeof(AiRadarTag))]
        partial struct JobB : IJobEntity
        {
            public void Execute(ref AiShipRadar radar, in AiShipRadarScanResults results)
            {
                radar.target = results.target;
            }
        }
    }
}

