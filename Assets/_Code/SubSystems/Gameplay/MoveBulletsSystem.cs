using Latios;
using Latios.Transforms;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

using static Unity.Entities.SystemAPI;

namespace Lsss
{
    [BurstCompile]
    public partial struct MoveBulletsSystem : ISystem
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
            float dt     = Time.DeltaTime;
            new Job { dt = dt }.ScheduleParallel();
        }

        [BurstCompile]
        [WithAll(typeof(BulletTag))]
        partial struct Job : IJobEntity
        {
            public float dt;

            public void Execute(ref TransformAspect transform, in Speed speed)
            {
                transform.worldPosition += transform.forwardDirection * speed.speed * dt;
            }
        }
    }
}

