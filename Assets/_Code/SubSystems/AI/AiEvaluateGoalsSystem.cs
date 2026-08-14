using Latios;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace Lsss
{
    [BurstCompile]
    public partial struct AiEvaluateGoalsSystem : ISystem, ILatiosApi
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
            new JobA().ScheduleParallel(api);
            new JobB().ScheduleParallel(api);
            new JobC().ScheduleParallel(api);
            new JobD().ScheduleParallel(api);
        }

        [BurstCompile]
        [With(typeof(AiTag))]
        partial struct JobA : IJobEach
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
        [With(typeof(AiTag))]
        [Without(typeof(AiSearchAndDestroyOutput))]
        partial struct JobB : IJobEach
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
        [With(typeof(AiTag))]
        [Without(typeof(AiExploreOutput))]
        partial struct JobC : IJobEach
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
        [With(typeof(AiTag))]
        [Without(typeof(AiSearchAndDestroyOutput))]
        [Without(typeof(AiExploreOutput))]
        partial struct JobD : IJobEach
        {
            public void Execute(ref AiGoalOutput output)
            {
                output.isValid = false;
                output.fire    = false;
            }
        }
    }
}

