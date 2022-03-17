using Latios;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace Lsss
{
    public partial class FaceCameraSystem : SubSystem
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
            var foundCamera = new NativeReference<CamFoundData>(Allocator.TempJob);

            Entities.WithAll<CameraManager>().ForEach((in Translation translation) =>
            {
                foundCamera.Value = new CamFoundData
                {
                    camPos = translation.Value,
                    found  = true
                };
            }).Schedule();
            var foundCameraRO = foundCamera.AsReadOnly();
            Entities.WithAll<FaceCameraTag>().ForEach((ref Rotation rotation, in LocalToWorld ltw) =>
            {
                if (foundCameraRO.Value.found == false)
                    return;

                var    camPos    = foundCameraRO.Value.camPos;
                float3 direction = math.normalize(camPos - ltw.Position);
                if (math.abs(math.dot(direction, new float3(0f, 1f, 0f))) < 0.9999f)
                {
                    var parentRot  = math.mul(ltw.Rotation, math.inverse(rotation.Value));
                    rotation.Value = math.mul(math.inverse(parentRot), quaternion.LookRotationSafe(direction, new float3(0f, 1f, 0f)));
                }
            }).ScheduleParallel();
            Dependency = foundCamera.Dispose(Dependency);
        }
    }
}

