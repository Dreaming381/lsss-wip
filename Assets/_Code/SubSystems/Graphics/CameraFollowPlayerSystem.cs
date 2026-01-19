using Latios;
using Latios.Transforms;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

using static Unity.Entities.SystemAPI;

// Todo: Change to use parenting.

namespace Lsss
{
    [BurstCompile]
    public partial struct CameraFollowPlayerSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var mountTransform = new NativeReference<TransformQvvs>(state.WorldUnmanaged.UpdateAllocator.ToAllocator, NativeArrayOptions.ClearMemory);

            new JobA
            {
                mountTransform  = mountTransform,
                transformLookup = GetComponentLookup<WorldTransform>(true)
            }.Schedule();
            new JobB
            {
                mountTransform  = mountTransform,
                transformLookup = new TransformAspectLookup(SystemAPI.GetComponentLookup<WorldTransform>(false),
                                                            SystemAPI.GetComponentLookup<RootReference>(true),
                                                            SystemAPI.GetBufferLookup<EntityInHierarchy>(true),
                                                            SystemAPI.GetBufferLookup<EntityInHierarchyCleanup>(true),
                                                            SystemAPI.GetEntityStorageInfoLookup())
            }.Schedule();
        }

        [BurstCompile]
        [WithAll(typeof(PlayerTag))]
        partial struct JobA : IJobEntity
        {
            public NativeReference<TransformQvvs>             mountTransform;
            [ReadOnly] public ComponentLookup<WorldTransform> transformLookup;

            public void Execute(in CameraMountPoint mount)
            {
                mountTransform.Value = transformLookup[mount.mountPoint].worldTransform;
            }
        }

        [BurstCompile]
        [WithAll(typeof(CameraManager.ExistComponent))]
        [WithAll(typeof(WorldTransform))]
        partial struct JobB : IJobEntity
        {
            public NativeReference<TransformQvvs> mountTransform;
            public TransformAspectLookup          transformLookup;

            public void Execute(Entity entity)
            {
                var worldTransform = mountTransform.Value;
                if (worldTransform.Equals(default))
                    return;

                var cameraTransform           = transformLookup[entity];
                cameraTransform.worldRotation = worldTransform.rotation;
                cameraTransform.worldPosition = worldTransform.position;
            }
        }
    }
}

