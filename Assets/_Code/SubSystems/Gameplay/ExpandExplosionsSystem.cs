using Latios;
using Latios.Transforms;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace Lsss
{
    [BurstCompile]
    public partial struct ExpandExplosionsSystem : ISystem, ILatiosApi
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
                dt = api.deltaTime,
            }.ScheduleParallel(api);
        }

        [BurstCompile]
        [With(typeof(ExplosionTag))]
        partial struct Job : IJobEach
        {
            public float dt;

            public void Execute([RootOnly] TransformAspect transform, in ExplosionStats stats)
            {
                var scale            = transform.localScale + stats.expansionRate * dt;
                scale                = math.min(scale, stats.radius);
                transform.localScale = scale;
            }
        }
    }
}

