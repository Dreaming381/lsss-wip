using Latios;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace Lsss
{
    public class ExpandExplosionsSystem : SubSystem
    {
        protected override void OnUpdate()
        {
            float dt = Time.DeltaTime;
            Entities.WithAll<ExplosionTag>().ForEach((ref Scale scale, in ExplosionStats stats) =>
            {
                scale.Value += stats.expansionRate * dt;
                scale.Value  = math.min(scale.Value, stats.radius);
            }).ScheduleParallel();
        }
    }
}

