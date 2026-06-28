using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

using static Unity.Entities.SystemAPI;

namespace Latios.Systems
{
    [DisableAutoCreation]
    [BurstCompile]
    public partial struct TickLocalSetupSystem : ISystem
    {
        internal float tickDeltaTime;
        internal bool  snapInputToTick;  // When true, we typically only simulate one tick per frame (assuming faster framerate than tickrate), but inputs may not be applied as quickly.

        LatiosWorldUnmanaged latiosWorld;

        float timeInTick;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            latiosWorld = state.GetLatiosWorldUnmanaged();

            tickDeltaTime = 1f;
            timeInTick    = 0f;

            latiosWorld.worldBlackboardEntity.AddComponentData(new TickingState { previousEvaluatedTick = -1 });
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var oldState   = latiosWorld.worldBlackboardEntity.GetComponentData<TickingState>();
            var dt         = Time.DeltaTime;
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
                latiosWorld.worldBlackboardEntity.SetComponentData(newState);
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
                latiosWorld.worldBlackboardEntity.SetComponentData(newState);
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
                latiosWorld.worldBlackboardEntity.SetComponentData(newState);
            }
        }
    }
}

