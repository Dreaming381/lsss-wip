using Latios;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

//Todo: Switch to IJobChunk that only updates chunk if any timeToLive < timeToLiveFadeStart.
//Doing so saves GPU bandwidth on Hybrid Renderer V2.
namespace Lsss
{
    public class LifetimeFadeSystem : SubSystem
    {
        protected override void OnUpdate()
        {
            Entities.ForEach((ref FadeProperty fade, in TimeToLive timeToLive, in TimeToLiveFadeStart timeToLiveFadeStart) =>
            {
                fade.fade  = math.saturate(timeToLive.timeToLive / timeToLiveFadeStart.fadeTimeWindow);
                fade.fade *= fade.fade;
            }).ScheduleParallel();
        }
    }
}

