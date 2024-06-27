using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

using static Unity.Entities.SystemAPI;

namespace Lsss
{
    [BurstCompile]
    public partial struct AiUpdateRadarScanRequestsSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            new Job
            {
                requestsLookup = GetComponentLookup<AiShipRadarRequests>(false),
                dt             = Time.DeltaTime
            }.ScheduleParallel();
        }

        [BurstCompile]
        [WithAll(typeof(AiTag))]
        [WithAll(typeof(AiSearchAndDestroyPersonality))]
        partial struct Job : IJobEntity
        {
            [NativeDisableParallelForRestriction] public ComponentLookup<AiShipRadarRequests> requestsLookup;
            public float                                                                      dt;

            public void Execute(in ShipReloadTime gunState, in AiShipRadarEntity radarEntity)
            {
                var canFireNextFrame = dt >= math.select(gunState.bulletReloadTime, gunState.clipReloadTime, gunState.bulletsRemaining == 0);

                requestsLookup.GetRefRW(radarEntity.shipRadar).ValueRW.requestFriendAndNearestEnemy = canFireNextFrame;
            }
        }
    }
}

