using Latios;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace Lsss
{
    [BurstCompile]
    public partial struct AiEvaluateGoalsSystem : ISystem
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
            new JobA().ScheduleParallel();
            new JobB().ScheduleParallel();
            new JobC().ScheduleParallel();
            new JobD().ScheduleParallel();
        }

        [BurstCompile]
        [WithAll(typeof(AiTag))]
        partial struct JobA : IJobEntity
        {
            public void Execute(ref AiGoalOutput output, in AiSearchAndDestroyOutput searchAndDestroy, in AiExploreOutput explore)
            {
                output.flyTowardsPosition    = math.select(explore.wanderPosition, searchAndDestroy.flyTowardsPosition, searchAndDestroy.isPositionValid);
                output.useAggressiveSteering = searchAndDestroy.isPositionValid;
                output.isValid               = searchAndDestroy.isPositionValid || explore.wanderPositionValid;
                output.fire                  = searchAndDestroy.fire;
            }
        }

        [BurstCompile]
        [WithAll(typeof(AiTag))]
        [WithNone(typeof(AiSearchAndDestroyOutput))]
        partial struct JobB : IJobEntity
        {
            public void Execute(ref AiGoalOutput output, in AiExploreOutput explore)
            {
                output.flyTowardsPosition    = math.select(0f, explore.wanderPosition, explore.wanderPositionValid);
                output.useAggressiveSteering = false;
                output.isValid               = explore.wanderPositionValid;
                output.fire                  = false;
            }
        }

        [BurstCompile]
        [WithAll(typeof(AiTag))]
        [WithNone(typeof(AiExploreOutput))]
        partial struct JobC : IJobEntity
        {
            public void Execute(ref AiGoalOutput output, in AiSearchAndDestroyOutput searchAndDestroy)
            {
                output.flyTowardsPosition    = math.select(0f, searchAndDestroy.flyTowardsPosition, searchAndDestroy.isPositionValid);
                output.useAggressiveSteering = searchAndDestroy.isPositionValid;
                output.isValid               = searchAndDestroy.isPositionValid;
                output.fire                  = searchAndDestroy.fire;
            }
        }

        [BurstCompile]
        [WithAll(typeof(AiTag))]
        [WithNone(typeof(AiSearchAndDestroyOutput))]
        [WithNone(typeof(AiExploreOutput))]
        partial struct JobD : IJobEntity
        {
            public void Execute(ref AiGoalOutput output)
            {
                output.isValid = false;
                output.fire    = false;
            }
        }
    }
}

