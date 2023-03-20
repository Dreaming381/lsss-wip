using Latios;
using Latios.Transforms;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace Lsss
{
    [BurstCompile]
    public partial struct SpawnPointAnimationSystem : ISystem
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
            new Job().ScheduleParallel();

            //state.CompleteDependency();
        }

        [BurstCompile]
        partial struct Job : IJobEntity
        {
            public void Execute(ref TransformAspect transform, in TimeToLive timeToLive, in SpawnPointAnimationData data)
            {
                float growFactor   = math.unlerp(data.growStartTime, data.growEndTime, timeToLive.timeToLive);
                growFactor         = math.select(growFactor, 1f, data.growStartTime == data.growEndTime);
                float shrinkFactor = math.unlerp(0f, data.shrinkStartTime, timeToLive.timeToLive);
                float factor       = math.saturate(math.min(growFactor, shrinkFactor));
                bool  isGrowing    = growFactor < shrinkFactor;

                float growRadians   = math.lerp(-data.growSpins, 0f, factor);
                float shrinkRadians = math.lerp(data.shrinkSpins, 0f, factor);
                float rads          = math.select(shrinkRadians, growRadians, isGrowing);

                transform.localRotation = quaternion.Euler(0f, 0f, rads);
                transform.localScale    = factor;
            }
        }
    }
}

