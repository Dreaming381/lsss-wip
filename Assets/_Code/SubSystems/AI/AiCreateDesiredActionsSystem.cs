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
    public partial struct AiCreateDesiredActionsSystem : ISystem
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
            state.Entities.WithAll<AiTag>().ForEach((ref ShipDesiredActions finalActions, in AiGoalOutput goalData, in Translation trans,
                                                     in Rotation rot, in ShipSpeedStats speedStats, in Speed speed) =>
            {
                float2 destinationTurn = float2.zero;

                if (goalData.isValid)
                {
                    float3 desiredHeading      = goalData.flyTowardsPosition - trans.Value;
                    bool   destinationIsBehind = math.dot(desiredHeading, math.forward(rot.Value)) < 0f;

                    if (destinationIsBehind)
                    {
                        var desiredHeadingLocal = math.rotate(math.inverse(rot.Value), desiredHeading);
                        destinationTurn         = math.normalizesafe(desiredHeadingLocal.xy, new float2(1f, 0f));
                    }
                    else if (math.all(math.abs(desiredHeading) < 1E-7f))
                    {
                        destinationTurn = float2.zero;
                    }
                    else
                    {
                        float distanceFactor      = math.select(speed.speed / math.length(desiredHeading), 1f, goalData.useAggressiveSteering);
                        var   desiredHeadingLocal = math.normalizesafe(math.rotate(math.inverse(rot.Value), desiredHeading));
                        float angleToHeading      = math.acos(math.dot(desiredHeadingLocal, new float3(0f, 0f, 1f)));
                        float angleFactor         = angleToHeading / speedStats.turnSpeed;
                        destinationTurn           = distanceFactor * angleFactor * math.normalizesafe(desiredHeadingLocal.xy);
                        if (math.lengthsq(destinationTurn) > 1f)
                            destinationTurn = math.normalize(destinationTurn);
                    }
                }

                finalActions.gas   = 1f;
                finalActions.turn  = destinationTurn;
                finalActions.fire  = goalData.fire;
                finalActions.boost = false;
            }).ScheduleParallel();
        }
    }
}

