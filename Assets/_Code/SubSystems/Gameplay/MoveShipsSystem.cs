using Latios;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace Lsss
{
    public class MoveShipsSystem : SubSystem
    {
        protected override void OnUpdate()
        {
            var dt          = Time.DeltaTime;
            var arenaRadius = sceneBlackboardEntity.GetComponentData<ArenaRadius>().radius;

            Entities.WithAll<ShipTag>().ForEach((ref Translation translation,
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

                float gas          = math.saturate(desiredActions.gas);
                float maxSpeed     = math.select(stats.topSpeed, stats.boostSpeed, isBoosting) * gas;
                float maxAccel     = math.select(stats.acceleration, stats.boostAcceleration, isBoosting);
                float accel        = gas * maxAccel;
                accel              = math.select(accel, -stats.deceleration * 0.5f, speed.speed > maxSpeed);
                float decel        = math.saturate(-desiredActions.gas) * -stats.deceleration * 0.5f;
                float a            = accel + decel;
                float forwardSpeed = speed.speed + a * dt;
                forwardSpeed       = math.select(forwardSpeed, math.min(forwardSpeed, maxSpeed), a >= 0f);
                forwardSpeed       = math.select(forwardSpeed, math.max(forwardSpeed, maxSpeed), a < 0f);

                gas                 = math.saturate(-desiredActions.gas);
                maxSpeed            = stats.reverseSpeed * gas;
                accel               = gas * stats.acceleration;
                accel               = math.select(accel, -stats.deceleration * 0.5f, -speed.speed > maxSpeed);
                decel               = math.saturate(desiredActions.gas) * -stats.deceleration * 0.5f;
                a                   = accel + decel;
                float backwardSpeed = speed.speed - a * dt;
                backwardSpeed       = math.select(backwardSpeed, math.max(backwardSpeed, -maxSpeed), a >= 0f);
                backwardSpeed       = math.select(backwardSpeed, math.min(backwardSpeed, -maxSpeed), a < 0f);

                bool useBackward  = speed.speed < 0f;
                useBackward      |= speed.speed == 0f && desiredActions.gas < 0f;

                speed.speed = math.select(forwardSpeed, backwardSpeed, useBackward);

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

