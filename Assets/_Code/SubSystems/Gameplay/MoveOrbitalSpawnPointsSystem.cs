using Latios;
using Latios.Transforms;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

using static Unity.Entities.SystemAPI;

namespace Lsss
{
    [BurstCompile]
    public partial struct MoveOrbitalSpawnPointsSystem : ISystem
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
            var job = new Job();
            job.dt  = Time.DeltaTime;
            job.ScheduleParallel();
        }

        [BurstCompile]
        [WithAll(typeof(SpawnPointTag))]
        partial struct Job : IJobEntity
        {
            public float dt;
            public void Execute(TransformAspect transform, in SpawnPointOrbitalPath path, in SpawnTimes pauseTime)
            {
                // !!!!!!!!!!!!!!!!!!!!! SERIOUSLY UNITY? !!!!!!!!!!!!!!!!!!!
                //var    dt                   = Time.DeltaTime;
                var    rotation             = quaternion.AxisAngle(path.orbitPlaneNormal, path.orbitSpeed * dt);
                float3 currentOutwardVector = transform.worldPosition - path.center;
                float3 newOutwardVector     = math.rotate(rotation, currentOutwardVector);
                newOutwardVector            = math.normalizesafe(newOutwardVector) * path.radius;
                transform.worldPosition     = math.select(transform.worldPosition, path.center + newOutwardVector, pauseTime.pauseTime <= 0f);
            }
        }
    }
}

