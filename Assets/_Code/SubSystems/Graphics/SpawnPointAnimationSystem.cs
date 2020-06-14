using Latios;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace Lsss
{
    public class SpawnPointAnimationSystem : SubSystem
    {
        protected override void OnUpdate()
        {
            Entities.ForEach((ref Rotation rot, ref Scale scale, in TimeToLive timeToLive, in SpawnPointAnimationData data) =>
            {
                float growFactor   = math.unlerp(data.growStartTime, data.growEndTime, timeToLive.timeToLive);
                float shrinkFactor = math.unlerp(0f, data.shrinkStartTime, timeToLive.timeToLive);
                float factor       = math.saturate(math.min(growFactor, shrinkFactor));
                bool  isGrowing    = growFactor < shrinkFactor;

                float growRadians   = math.lerp(-data.growSpins, 0f, factor);
                float shrinkRadians = math.lerp(data.shrinkSpins, 0f, factor);
                float rads          = math.select(shrinkRadians, growRadians, isGrowing);

                rot.Value   = quaternion.Euler(0f, 0f, rads);
                scale.Value = factor;
            }).ScheduleParallel();
        }
    }
}

