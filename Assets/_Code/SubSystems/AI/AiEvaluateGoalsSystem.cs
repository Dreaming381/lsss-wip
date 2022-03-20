using System.Security.Cryptography;
using Latios;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine.SceneManagement;

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
            state.Entities.WithAll<AiTag>().ForEach((ref AiGoalOutput output, in AiSearchAndDestroyOutput searchAndDestroy, in AiExploreOutput explore) =>
            {
                output.flyTowardsPosition    = math.select(explore.wanderPosition, searchAndDestroy.flyTowardsPosition, searchAndDestroy.isPositionValid);
                output.useAggressiveSteering = searchAndDestroy.isPositionValid;
                output.isValid               = searchAndDestroy.isPositionValid || explore.wanderPositionValid;
                output.fire                  = searchAndDestroy.fire;
            }).ScheduleParallel();

            state.Entities.WithAll<AiTag>().WithNone<AiSearchAndDestroyOutput>().ForEach((ref AiGoalOutput output, in AiExploreOutput explore) =>
            {
                output.flyTowardsPosition    = math.select(0f, explore.wanderPosition, explore.wanderPositionValid);
                output.useAggressiveSteering = false;
                output.isValid               = explore.wanderPositionValid;
                output.fire                  = false;
            }).ScheduleParallel();

            state.Entities.WithAll<AiTag>().WithNone<AiExploreOutput>().ForEach((ref AiGoalOutput output, in AiSearchAndDestroyOutput searchAndDestroy) =>
            {
                output.flyTowardsPosition    = math.select(0f, searchAndDestroy.flyTowardsPosition, searchAndDestroy.isPositionValid);
                output.useAggressiveSteering = searchAndDestroy.isPositionValid;
                output.isValid               = searchAndDestroy.isPositionValid;
                output.fire                  = searchAndDestroy.fire;
            }).ScheduleParallel();

            state.Entities.WithAll<AiTag>().WithNone<AiSearchAndDestroyOutput, AiExploreOutput>().ForEach((ref AiGoalOutput output) =>
            {
                output.isValid = false;
                output.fire    = false;
            }).ScheduleParallel();
        }
    }
}

