using Latios;
using Latios.Psyshock;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace Lsss
{
    [BurstCompile]
    public partial struct MoveShipsSystem : ISystem
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
            var dt          = state.Time.DeltaTime;
            var arenaRadius = state.GetSceneBlackboardEntity().GetComponentData<ArenaRadius>().radius;

            state.Entities.WithAll<ShipTag>().ForEach((ref Translation translation,
                                                       ref Rotation rotation,
                                                       ref Speed speed,
                                                       ref ShipBoostTank boostTank,
                                                       in ShipSpeedStats stats,
                                                       in ShipDesiredActions desiredActions) =>
            {
                //Rotation
                var oldRotation = rotation.Value;
                var turn        = desiredActions.turn * stats.turnSpeed * dt;
                turn.y          = -turn.y;
                float3 up       = math.mul(oldRotation, new float3(0f, 1f, 0f));
                turn.x          = math.select(turn.x, -turn.x, up.y < 0f);
                var xAxisRot    = quaternion.Euler(turn.y, 0f, 0f);
                var yAxisRot    = quaternion.Euler(0f, turn.x, 0f);
                var newRotation = math.mul(oldRotation, xAxisRot);
                newRotation     = math.mul(yAxisRot, newRotation);
                rotation.Value  = newRotation;

                //Speed
                bool isBoosting = desiredActions.boost && boostTank.boost > 0f;
                bool isReverse  = speed.speed < 0f;

                speed.speed = Physics.StepVelocityWithInput(desiredActions.gas,
                                                            speed.speed,
                                                            math.select(stats.acceleration, stats.boostAcceleration, isBoosting),
                                                            stats.deceleration,
                                                            math.select(stats.topSpeed, stats.boostSpeed, isBoosting),
                                                            stats.acceleration,
                                                            stats.deceleration,
                                                            stats.reverseSpeed,
                                                            dt);

                //Translation
                translation.Value      += math.forward(newRotation) * speed.speed * dt;
                float distanceToOrigin  = math.length(translation.Value);
                translation.Value       = math.select(translation.Value, arenaRadius / distanceToOrigin * translation.Value, distanceToOrigin > arenaRadius);

                //Boost Tank
                boostTank.boost += math.select(stats.boostRechargeRate, -stats.boostDepleteRate, isBoosting) * dt;
                boostTank.boost  = math.min(boostTank.boost, stats.boostCapacity);
            }).ScheduleParallel();
        }
    }
}

