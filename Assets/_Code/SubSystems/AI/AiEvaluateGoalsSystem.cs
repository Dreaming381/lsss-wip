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
    public class AiEvaluateGoalsSystem : SubSystem
    {
        protected override void OnUpdate()
        {
            Entities.WithAll<AiTag>().ForEach((ref AiGoalOutput output, ref AiWantsToFire fire, in AiSearchAndDestroyOutput searchAndDestroy, in AiExploreOutput explore) =>
            {
                output.flyTowardsPosition    = math.select(explore.wanderPosition, searchAndDestroy.flyTowardsPosition, searchAndDestroy.isPositionValid);
                output.useAggressiveSteering = searchAndDestroy.isPositionValid;
                output.isValid               = searchAndDestroy.isPositionValid || explore.wanderPositionValid;
                fire.fire                    = searchAndDestroy.fire;
            }).ScheduleParallel();

            Entities.WithAll<AiTag>().WithNone<AiSearchAndDestroyOutput>().ForEach((ref AiGoalOutput output, ref AiWantsToFire fire, in AiExploreOutput explore) =>
            {
                output.flyTowardsPosition    = math.select(0f, explore.wanderPosition, explore.wanderPositionValid);
                output.useAggressiveSteering = false;
                output.isValid               = explore.wanderPositionValid;
                fire.fire                    = false;
            }).ScheduleParallel();

            Entities.WithAll<AiTag>().WithNone<AiExploreOutput>().ForEach((ref AiGoalOutput output, ref AiWantsToFire fire, in AiSearchAndDestroyOutput searchAndDestroy) =>
            {
                output.flyTowardsPosition    = math.select(0f, searchAndDestroy.flyTowardsPosition, searchAndDestroy.isPositionValid);
                output.useAggressiveSteering = searchAndDestroy.isPositionValid;
                output.isValid               = searchAndDestroy.isPositionValid;
                fire.fire                    = searchAndDestroy.fire;
            }).ScheduleParallel();

            Entities.WithAll<AiTag>().WithNone<AiSearchAndDestroyOutput, AiExploreOutput>().ForEach((ref AiGoalOutput output, ref AiWantsToFire fire) =>
            {
                output.isValid = false;
                fire.fire      = false;
            }).ScheduleParallel();
        }
    }
}

