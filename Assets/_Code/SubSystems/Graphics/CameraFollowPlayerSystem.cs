using Latios;
using Latios.Transforms;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

// Todo: Change to use parenting.

namespace Lsss
{
    [BurstCompile]
    public partial struct CameraFollowPlayerSystem : ISystem, ILatiosApi
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            this.OnCreateForLatios(ref state);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var api            = this.GetApi(ref state);
            var mountTransform = new NativeReference<TransformQvvs>(state.WorldUnmanaged.UpdateAllocator.ToAllocator, NativeArrayOptions.ClearMemory);

            new JobA
            {
                mountTransform = mountTransform,
            }.Inject(api).Schedule();
            new JobB
            {
                mountTransform = mountTransform,
            }.Inject(api).Schedule();
        }

        [BurstCompile]
        [WithAll(typeof(PlayerTag))]
        partial struct JobA : IJobEntity, IInjectable
        {
            public NativeReference<TransformQvvs>              mountTransform;
            [ReadOnly, Inject] ComponentLookup<WorldTransform> transformLookup;

            public void Execute(in CameraMountPoint mount)
            {
                mountTransform.Value = transformLookup[mount.mountPoint].worldTransform;
            }
        }

        [BurstCompile]
        [WithAll(typeof(CameraManager.ExistComponent))]
        [WithAll(typeof(WorldTransform))]
        partial struct JobB : IJobEntity, IInjectable
        {
            public NativeReference<TransformQvvs> mountTransform;
            [Inject] TransformAspectLookup        transformLookup;

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

