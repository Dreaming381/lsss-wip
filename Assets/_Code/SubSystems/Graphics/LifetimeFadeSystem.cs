using Latios;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

// Todo: Switch to IJobChunk that only updates chunk if any timeToLive < timeToLiveFadeStart.
// Doing so saves GPU bandwidth on Hybrid Renderer V2.
namespace Lsss
{
    [BurstCompile]
    public partial struct LifetimeFadeSystem : ISystem
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
        }

        [BurstCompile]
        partial struct Job : IJobEntity
        {
            public void Execute(ref FadeProperty fade, in TimeToLive timeToLive, in TimeToLiveFadeStart timeToLiveFadeStart)
            {
                fade.fade  = math.saturate(timeToLive.timeToLive / timeToLiveFadeStart.fadeTimeWindow);
                fade.fade *= fade.fade;
            }
        }
    }
}

