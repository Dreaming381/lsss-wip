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
            var foundCamera = new NativeReference<float3>(state.WorldUnmanaged.UpdateAllocator.ToAllocator);

            new JobA { foundCamera = foundCamera }.Schedule();
            new JobB {foundCamera  = foundCamera}.ScheduleParallel();
        }

        [BurstCompile]
        [WithAll(typeof(CameraManager.ExistComponent))]
        partial struct JobA : IJobEntity
        {
            public NativeReference<float3> foundCamera;

            public void Execute(in WorldTransform translation)
            {
                foundCamera.Value = translation.position;
            }
        }

        [BurstCompile]
        [WithAll(typeof(FaceCameraTag))]
        partial struct JobB : IJobEntity
        {
            [ReadOnly] public NativeReference<float3> foundCamera;

            public void Execute(TransformAspect transform)
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

