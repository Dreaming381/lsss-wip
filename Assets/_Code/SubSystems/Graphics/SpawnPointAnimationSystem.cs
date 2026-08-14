using Latios;
using Latios.Transforms;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace Lsss
{
    [RequireMatchingQueriesForUpdate]
    [BurstCompile]
    public partial struct SpawnPointAnimationSystem : ISystem, ILatiosApi
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
            new Job().ScheduleParallel(api);
        }

        [BurstCompile]
        partial struct Job : IJobEach
        {
            public void Execute(TransformAspect transform, in TimeToLive timeToLive, in SpawnPointAnimationData data)
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
                transform.localScale    = math.max(factor, 0.001f);
            }
        }
    }
}

