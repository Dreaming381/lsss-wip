using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace Latios.Systems
{
    [UpdateInGroup(typeof(LatiosWorldSyncGroup))]
    [DisableAutoCreation]
    [BurstCompile]
    public partial struct TickedLocalSetupSystem : ISystem, ILatiosApi
    {
        internal float tickDeltaTime;
        internal bool  snapInputToTick;  // When true, we typically only simulate one tick per frame (assuming faster framerate than tickrate), but inputs may not be applied as quickly.

        float timeInTick;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            var api = this.OnCreateForLatios(ref state);

            tickDeltaTime = 1f / 30f;
            timeInTick    = 0f;

            api.worldBlackboardEntity.AddComponentData(new TickingState { previousEvaluatedTick = -1 });
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var api        = this.GetApi(ref state);
            var oldState   = api.worldBlackboardEntity.GetComponentData<TickingState>();
            var dt         = api.deltaTime;
            int rollovers  = 0;
            timeInTick    += dt;
            while (timeInTick > tickDeltaTime)
            {
                rollovers++;
                timeInTick -= tickDeltaTime;
            }

            if (rollovers == 0)
            {
                // We did not advance the tick. Roll back.
                var newState                = oldState;
                newState.deltaTime          = tickDeltaTime;
                newState.ticksThisFrame     = 1;
                newState.firstTickThisFrame = newState.tick;
                newState.inputTick          = newState.tick;
                newState.inputTickFraction  = oldState.finalTickFraction;
                newState.finalTickFraction  = timeInTick / tickDeltaTime;
                newState.frameCounter++;
                newState.previousEvaluatedTick = oldState.tick;
                api.worldBlackboardEntity.SetComponentData(newState);
            }
            else if (snapInputToTick || oldState.finalTickFraction > 0.9999f)
            {
                var newState                 = oldState;
                newState.elapsedTime        += tickDeltaTime;
                newState.deltaTime           = tickDeltaTime;
                newState.ticksThisFrame      = rollovers;
                newState.firstTickThisFrame  = oldState.tick + 1;
                newState.newTick             = newState.firstTickThisFrame;
                newState.inputTick           = newState.firstTickThisFrame;
                newState.inputTickFraction   = 0f;
                newState.finalTickFraction   = timeInTick / tickDeltaTime;
                newState.frameCounter++;
                newState.previousEvaluatedTick = oldState.tick;
                api.worldBlackboardEntity.SetComponentData(newState);
            }
            else
            {
                var newState                 = oldState;
                newState.elapsedTime        += tickDeltaTime;
                newState.deltaTime           = tickDeltaTime;
                newState.ticksThisFrame      = rollovers + 1;
                newState.firstTickThisFrame  = oldState.tick;
                newState.newTick             = newState.firstTickThisFrame + 1;
                newState.inputTick           = newState.firstTickThisFrame;
                newState.inputTickFraction   = oldState.finalTickFraction;
                newState.finalTickFraction   = timeInTick / tickDeltaTime;
                newState.frameCounter++;
                newState.previousEvaluatedTick = oldState.tick;
                api.worldBlackboardEntity.SetComponentData(newState);
            }
        }
    }
}

