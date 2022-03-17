using Latios;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace Lsss
{
    public partial class MoveBulletsSystem : SubSystem
    {
        protected override void OnUpdate()
        {
            float dt = Time.DeltaTime;

            Entities.WithAll<BulletTag>().ForEach((ref Translation translation, ref BulletPreviousPosition prevPosition, in Speed speed, in Rotation rotation) =>
            {
                prevPosition.previousPosition  = translation.Value;
                translation.Value             += math.forward(rotation.Value) * speed.speed * dt;
            }).ScheduleParallel();
        }
    }
}

