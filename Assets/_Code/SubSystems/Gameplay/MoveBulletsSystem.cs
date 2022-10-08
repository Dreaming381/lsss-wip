using Latios;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

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
            float dt     = SystemAPI.Time.DeltaTime;
            new Job { dt = dt }.ScheduleParallel();
        }

        [BurstCompile]
        [WithAll(typeof(BulletTag))]
        partial struct Job : IJobEntity
        {
            public float dt;

            public void Execute(ref Translation translation, ref BulletPreviousPosition prevPosition, in Speed speed, in Rotation rotation)
            {
                prevPosition.previousPosition  = translation.Value;
                translation.Value             += math.forward(rotation.Value) * speed.speed * dt;
            }
        }
    }
}

