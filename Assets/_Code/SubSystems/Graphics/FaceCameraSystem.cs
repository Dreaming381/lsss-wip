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
    public partial struct FaceCameraSystem : ISystem
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
            // We use NativeList here because NativeReference is broken in ISystem
            var foundCamera = state.WorldUnmanaged.UpdateAllocator.AllocateNativeList<float3>(1);

            state.Entities.WithAll<CameraManager>().ForEach((in Translation translation) =>
            {
                if (foundCamera.IsEmpty)
                    foundCamera.Add(translation.Value);
            }).Schedule();
            state.Entities.WithAll<FaceCameraTag>().ForEach((ref Rotation rotation, in LocalToWorld ltw) =>
            {
                if (foundCamera.IsEmpty)
                    return;

                var    camPos    = foundCamera[0];
                float3 direction = math.normalize(camPos - ltw.Position);
                if (math.abs(math.dot(direction, new float3(0f, 1f, 0f))) < 0.9999f)
                {
                    var parentRot  = math.mul(ltw.Rotation, math.inverse(rotation.Value));
                    rotation.Value = math.mul(math.inverse(parentRot), quaternion.LookRotationSafe(direction, new float3(0f, 1f, 0f)));
                }
            }).WithReadOnly(foundCamera).ScheduleParallel();
        }
    }
}

