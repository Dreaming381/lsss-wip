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
    public partial struct MoveOrbitalSpawnPointsSystem : ISystem, ILatiosApi
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
        [With(typeof(SpawnPointTag))]
        partial struct Job : IJobEach
        {
            public float dt;

            public void Execute([RootOnly] TransformAspect transform, in SpawnPointOrbitalPath path, in SpawnTimes pauseTime)
            {
                var    rotation             = quaternion.AxisAngle(path.orbitPlaneNormal, path.orbitSpeed * dt);
                float3 currentOutwardVector = transform.worldPosition - path.center;
                float3 newOutwardVector     = math.rotate(rotation, currentOutwardVector);
                newOutwardVector            = math.normalizesafe(newOutwardVector) * path.radius;
                transform.worldPosition     = math.select(transform.worldPosition, path.center + newOutwardVector, pauseTime.pauseTime <= 0f);
            }
        }
    }
}

