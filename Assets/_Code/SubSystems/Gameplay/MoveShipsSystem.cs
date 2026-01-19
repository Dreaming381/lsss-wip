using Latios;
using Latios.Psyshock;
using Latios.Transforms;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

using static Unity.Entities.SystemAPI;

namespace Lsss
{
    [BurstCompile]
    [RequireMatchingQueriesForUpdate]
    public partial struct MoveShipsSystem : ISystem
    {
        LatiosWorldUnmanaged latiosWorld;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            latiosWorld = state.GetLatiosWorldUnmanaged();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var dt          = Time.DeltaTime;
            var arenaRadius = latiosWorld.sceneBlackboardEntity.GetComponentData<ArenaRadius>().radius;

            new Job
            {
                transformHandle = new TransformAspectRootHandle(SystemAPI.GetComponentLookup<WorldTransform>(false),
                                                                SystemAPI.GetBufferTypeHandle<EntityInHierarchy>(true),
                                                                SystemAPI.GetEntityStorageInfoLookup()),
                dt          = dt,
                arenaRadius = arenaRadius,
            }.ScheduleParallel();
        }

        [BurstCompile]
        [WithAll(typeof(ShipTag), typeof(WorldTransform))]
        partial struct Job : IJobEntity, IJobEntityChunkBeginEnd
        {
            public TransformAspectRootHandle transformHandle;
            public float                     dt;
            public float                     arenaRadius;

            public void Execute([EntityIndexInChunk] int indexInChunk,
                                ref Speed speed,
                                ref ShipBoostTank boostTank,
                                in ShipSpeedStats stats,
                                in ShipDesiredActions desiredActions)
            {
                var transform = transformHandle[indexInChunk];

                // Rotation
                var oldRotation         = transform.worldRotation;
                var turn                = desiredActions.turn * stats.turnSpeed * dt;
                turn.y                  = -turn.y;
                float3 up               = math.mul(oldRotation, new float3(0f, 1f, 0f));
                turn.x                  = math.select(turn.x, -turn.x, up.y < 0f);
                var xAxisRot            = quaternion.Euler(turn.y, 0f, 0f);
                var yAxisRot            = quaternion.Euler(0f, turn.x, 0f);
                var newRotation         = math.mul(oldRotation, xAxisRot);
                newRotation             = math.mul(yAxisRot, newRotation);
                transform.worldRotation = newRotation;

                // Speed
                bool isBoosting = desiredActions.boost && boostTank.boost > 0f;

                speed.speed = Physics.StepVelocityWithInput(desiredActions.gas,
                                                            speed.speed,
                                                            math.select(stats.acceleration, stats.boostAcceleration, isBoosting),
                                                            stats.deceleration,
                                                            math.select(stats.topSpeed, stats.boostSpeed, isBoosting),
                                                            stats.acceleration,
                                                            stats.deceleration,
                                                            stats.reverseSpeed,
                                                            dt);

                // Translation
                var   position          = transform.worldPosition + transform.forwardDirection * speed.speed * dt;
                float distanceToOrigin  = math.length(position);
                transform.worldPosition = math.select(position, arenaRadius / distanceToOrigin * position, distanceToOrigin > arenaRadius);

                // Boost Tank
                boostTank.boost += math.select(stats.boostRechargeRate, -stats.boostDepleteRate, isBoosting) * dt;
                boostTank.boost  = math.min(boostTank.boost, stats.boostCapacity);
            }

            public bool OnChunkBegin(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                transformHandle.SetupChunk(in chunk);
                return true;
            }

            public void OnChunkEnd(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask, bool chunkWasExecuted)
            {
            }
        }
    }
}

