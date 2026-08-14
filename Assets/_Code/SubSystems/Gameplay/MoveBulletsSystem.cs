using Latios;
using Latios.Transforms;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace Lsss
{
    [BurstCompile]
    public partial struct MoveBulletsSystem : ISystem, ILatiosApi
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
            new Job
            {
                dt = api.deltaTime
            }.ScheduleParallel(api);
        }

        [BurstCompile]
        [With(typeof(BulletTag))]
        partial struct Job : IJobEach
        {
            public float dt;

            public void Execute([RootOnly] TransformAspect transform, in Speed speed)
            {
                transform.worldPosition += dt * speed.speed * transform.forwardDirection;
            }
        }
    }
}

