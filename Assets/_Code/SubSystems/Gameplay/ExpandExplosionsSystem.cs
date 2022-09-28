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

            public void Execute(ref Scale scale, in ExplosionStats stats)
            {
                scale.Value += stats.expansionRate * dt;
                scale.Value  = math.min(scale.Value, stats.radius);
            }
        }
    }
}

