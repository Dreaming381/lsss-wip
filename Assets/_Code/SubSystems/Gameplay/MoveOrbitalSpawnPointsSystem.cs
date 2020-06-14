using Latios;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace Lsss
{
    public class MoveOrbitalSpawnPointsSystem : SubSystem
    {
        protected override void OnUpdate()
        {
            float dt = Time.DeltaTime;
            Entities.WithAll<SpawnPointTag>().ForEach((ref Translation translation, in SpawnPointOrbitalPath path, in SpawnTimes pauseTime) =>
            {
                var    rotation             = quaternion.AxisAngle(path.orbitPlaneNormal, path.orbitSpeed * dt);
                float3 currentOutwardVector = translation.Value - path.center;
                float3 newOutwardVector     = math.rotate(rotation, currentOutwardVector);
                newOutwardVector            = math.normalize(newOutwardVector) * path.radius;
                translation.Value           = math.select(translation.Value, path.center + newOutwardVector, pauseTime.pauseTime <= 0f);
            }).ScheduleParallel();
        }
    }
}

