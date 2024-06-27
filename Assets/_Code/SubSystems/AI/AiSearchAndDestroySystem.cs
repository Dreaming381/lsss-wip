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
        public void OnUpdate(ref SystemState state)
        {
            var jobA               = new JobA();
            jobA.scanResultsLookup = GetComponentLookup<AiShipRadarScanResults>(true);
            jobA.ScheduleParallel();

            //var stats            = new NativeReference<Stats>(state.WorldUpdateAllocator, NativeArrayOptions.ClearMemory);
            //new StatsJob { stats = stats }.Schedule();
            //state.Dependency     = new LogJob { stats = stats }.Schedule(state.Dependency);

            new JobB().ScheduleParallel();
        }

        [BurstCompile]
        [WithAll(typeof(AiTag))]
        partial struct JobA : IJobEntity
        {
            [ReadOnly] public ComponentLookup<AiShipRadarScanResults> scanResultsLookup;

            public void Execute(ref AiSearchAndDestroyOutput output, in AiSearchAndDestroyPersonality personality, in AiShipRadarEntity shipRadarEntity)
            {
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

        struct Stats
        {
            public int fullScanCount;
            public int requestedScanCount;
            public int wanderingCount;
            public int promotedScanCount;
            public int friendBlockedCount;
        }

        [BurstCompile]
        [WithAll(typeof(AiRadarTag))]
        [WithAll(typeof(AiShipRadarNeedsFullScanFlag))]
        partial struct StatsJob : IJobEntity
        {
            public NativeReference<Stats> stats;

            public void Execute(in AiShipRadarRequests requests, in AiShipRadar radar, in AiShipRadarScanResults results)
            {
                var s = stats.Value;
                s.fullScanCount++;
                if (requests.requestFriendAndNearestEnemy)
                {
                    s.requestedScanCount++;
                    if (radar.target == Entity.Null)
                        s.wanderingCount++;
                }
                else if (radar.target == Entity.Null)
                    s.promotedScanCount++;
                if (requests.requestFriendAndNearestEnemy && results.friendFound)
                    s.friendBlockedCount++;
                stats.Value = s;
            }
        }

        [BurstCompile]
        struct LogJob : IJob
        {
            public NativeReference<Stats> stats;

            public void Execute()
            {
                var s = stats.Value;
                UnityEngine.Debug.Log(
                    $"full scans: {s.fullScanCount}, full scan requests: {s.requestedScanCount}, wandering: {s.wanderingCount} promoted scans: {s.promotedScanCount}, requested scans resulting in friend blocks: {s.friendBlockedCount}");
            }
        }
    }
}

