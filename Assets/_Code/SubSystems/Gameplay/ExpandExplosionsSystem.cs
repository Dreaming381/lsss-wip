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
            float dt = state.Time.DeltaTime;
            state.Entities.WithAll<ExplosionTag>().ForEach((ref Scale scale, in ExplosionStats stats) =>
            {
                scale.Value += stats.expansionRate * dt;
                scale.Value  = math.min(scale.Value, stats.radius);
            }).ScheduleParallel();
        }
    }
}

