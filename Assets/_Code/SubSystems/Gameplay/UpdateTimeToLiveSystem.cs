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
        BeginInitializationEntityCommandBufferSystem m_ecbSystem;

        protected override void OnCreate()
        {
            m_ecbSystem = World.GetExistingSystem<BeginInitializationEntityCommandBufferSystem>();
        }

        protected override void OnUpdate()
        {
            var   ecbPackage = m_ecbSystem.CreateCommandBuffer();
            var   ecb        = ecbPackage.ToConcurrent();
            float dt         = Time.DeltaTime;

            Entities.ForEach((Entity entity, int entityInQueryIndex, ref TimeToLive timeToLive) =>
            {
                timeToLive.timeToLive -= dt;
                if (timeToLive.timeToLive < 0f)
                    ecb.DestroyEntity(entityInQueryIndex, entity);
            }).ScheduleParallel();

            m_ecbSystem.AddJobHandleForProducer(Dependency);
        }
    }
}

