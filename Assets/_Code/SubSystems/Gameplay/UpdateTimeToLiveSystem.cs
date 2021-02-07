using Latios;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace Lsss
{
    public class UpdateTimeToLiveSystem : SubSystem
    {
        protected override void OnUpdate()
        {
            var   dcb = latiosWorld.syncPoint.CreateDestroyCommandBuffer().AsParallelWriter();
            float dt  = Time.DeltaTime;

            Entities.ForEach((Entity entity, int entityInQueryIndex, ref TimeToLive timeToLive) =>
            {
                timeToLive.timeToLive -= dt;
                if (timeToLive.timeToLive < 0f)
                    dcb.Add(entity, entityInQueryIndex);
            }).ScheduleParallel();
        }
    }
}

