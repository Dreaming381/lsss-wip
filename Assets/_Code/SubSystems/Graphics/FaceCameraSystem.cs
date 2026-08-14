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
    public partial struct FaceCameraSystem : ISystem, ILatiosApi
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            this.OnCreateForLatios(ref state);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var api         = this.GetApi(ref state);
            var foundCamera = new NativeReference<float3>(state.WorldUpdateAllocator);

            new JobA { foundCamera = foundCamera }.Schedule(api);
            new JobB
            {
                foundCamera = foundCamera,
            }.ScheduleParallel(api);
        }

        [BurstCompile]
        [With(typeof(CameraManager.ExistComponent))]
        partial struct JobA : IJobEach
        {
            public NativeReference<float3> foundCamera;

            public void Execute(in WorldTransform translation)
            {
                foundCamera.Value = translation.position;
            }
        }

        [BurstCompile]
        [With(typeof(FaceCameraTag), typeof(WorldTransform))]
        partial struct JobB : IJobEach
        {
            [ReadOnly] public NativeReference<float3> foundCamera;

            public void Execute(TransformDeferableAspect transform)
            {
                var    camPos    = foundCamera.Value;
                float3 direction = math.normalize(camPos - transform.worldPosition);
                if (math.abs(math.dot(direction, new float3(0f, 1f, 0f))) < 0.9999f)
                {
                    transform.LookAt(direction, new float3(0f, 1f, 0f));
                }
            }
        }
    }
}

