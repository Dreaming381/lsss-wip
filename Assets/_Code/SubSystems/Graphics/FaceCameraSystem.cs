using Latios;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace Lsss
{
    public class FaceCameraSystem : SubSystem
    {
        struct CamFoundData
        {
            public float3 camPos;
            public bool   found;
        }

        protected override void OnUpdate()
        {
            var camFound = new CamFoundData
            {
                camPos = default,
                found  = false
            };
            var foundCameras = new NativeArray<CamFoundData>(1, Allocator.TempJob);

            Entities.WithAll<UnityEngine.Camera>().ForEach((in Translation translation) =>
            {
                foundCameras[0] = new CamFoundData
                {
                    camPos = translation.Value,
                    found  = true
                };
            }).Schedule();

            Entities.WithAll<FaceCameraTag>().ForEach((ref Rotation rotation, in LocalToWorld ltw) =>
            {
                if (foundCameras[0].found == false)
                    return;

                var    camPos    = foundCameras[0].camPos;
                float3 direction = math.normalize(camPos - ltw.Position);
                if (math.abs(math.dot(direction, new float3(0f, 1f, 0f))) < 0.9999f)
                {
                    var parentRot  = math.mul(ltw.Rotation, math.inverse(rotation.Value));
                    rotation.Value = math.mul(math.inverse(parentRot), quaternion.LookRotationSafe(direction, new float3(0f, 1f, 0f)));
                }
            }).ScheduleParallel();
            Dependency = foundCameras.Dispose(Dependency);
        }
    }
}

