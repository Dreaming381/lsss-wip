using Latios;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

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
            float dt = state.Time.DeltaTime;

            state.Entities.WithAll<BulletTag>().ForEach((ref Translation translation, ref BulletPreviousPosition prevPosition, in Speed speed, in Rotation rotation) =>
            {
                prevPosition.previousPosition  = translation.Value;
                translation.Value             += math.forward(rotation.Value) * speed.speed * dt;
            }).ScheduleParallel();
        }
    }
}

