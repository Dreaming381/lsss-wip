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
    public partial struct ExpandExplosionsSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
        }
        [BurstCompile] public void OnDestroy(ref SystemState state)
        {
        }
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float dt     = SystemAPI.Time.DeltaTime;
            new Job { dt = dt }.ScheduleParallel();
        }

        [BurstCompile]
        [WithAll(typeof(ExplosionTag))]
        partial struct Job : IJobEntity
        {
            public float dt;

            public void Execute(TransformAspect transform, in ExplosionStats stats)
            {
                var scale            = transform.localScale + stats.expansionRate * dt;
                scale                = math.min(scale, stats.radius);
                transform.localScale = scale;
            }
        }
    }
}

