using Latios;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Lsss
{
    [BurstCompile]
    public partial struct AiUpdateRadarScanRequestsSystem : ISystem, ILatiosApi
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            this.OnCreateForLatios(ref state);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var api = this.GetApi(ref state);
            new Job
            {
                dt = api.deltaTime
            }.Inject(api).ScheduleParallel();
        }

        [BurstCompile]
        [WithAll(typeof(AiTag))]
        [WithAll(typeof(AiSearchAndDestroyPersonality))]
        partial struct Job : IJobEntity, IInjectable
        {
            [NativeDisableParallelForRestriction, Inject] ComponentLookup<AiShipRadarRequests> requestsLookup;
            public float                                                                       dt;

            public void Execute(in ShipReloadTime gunState, in AiShipRadarEntity radarEntity)
            {
                var canFireNextFrame = dt >= math.select(gunState.bulletReloadTime, gunState.clipReloadTime, gunState.bulletsRemaining == 0);

                requestsLookup.GetRefRW(radarEntity.shipRadar).ValueRW.requestFriendAndNearestEnemy = canFireNextFrame;
            }
        }
    }
}

