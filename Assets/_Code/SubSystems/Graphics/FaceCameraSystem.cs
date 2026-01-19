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
        public void OnUpdate(ref SystemState state)
        {
            var foundCamera = new NativeReference<float3>(state.WorldUnmanaged.UpdateAllocator.ToAllocator);

            new JobA { foundCamera = foundCamera }.Schedule();
            new JobB
            {
                foundCamera     = foundCamera,
                transformLookup = new TransformAspectLookup(SystemAPI.GetComponentLookup<WorldTransform>(false),
                                                            SystemAPI.GetComponentLookup<RootReference>(true),
                                                            SystemAPI.GetBufferLookup<EntityInHierarchy>(true),
                                                            SystemAPI.GetBufferLookup<EntityInHierarchyCleanup>(true),
                                                            SystemAPI.GetEntityStorageInfoLookup())
            }.Schedule();
            // .ScheduleParallel(); // Can't make parallel with TransformAspectLookup yet.
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
        [WithAll(typeof(FaceCameraTag), typeof(WorldTransform))]
        partial struct JobB : IJobEntity
        {
            [ReadOnly] public NativeReference<float3> foundCamera;
            public TransformAspectLookup              transformLookup;

            public void Execute(Entity entity)
            {
                var    transform = transformLookup[entity];
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

