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
        TransformAspect.Lookup m_transformLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            m_transformLookup = new TransformAspect.Lookup(ref state);
        }
        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var mountEntity = new NativeReference<EntityWith<WorldTransform> >(state.WorldUnmanaged.UpdateAllocator.ToAllocator);

            new JobA { mountEntity = mountEntity }.Schedule();
            m_transformLookup.Update(ref state);
            var jobB = new JobB { mountEntity = mountEntity, transformLookup = m_transformLookup };
            jobB.Schedule();
        }

        [BurstCompile]
        [WithAll(typeof(PlayerTag))]
        partial struct JobA : IJobEntity
        {
            public NativeReference<EntityWith<WorldTransform> > mountEntity;

            public void Execute(in CameraMountPoint mount)
            {
                mountEntity.Value = mount.mountPoint;
            }
        }

        [BurstCompile]
        [WithAll(typeof(CameraManager.ExistComponent))]
        [WithAll(typeof(WorldTransform))]
        partial struct JobB : IJobEntity
        {
            public NativeReference<EntityWith<WorldTransform> > mountEntity;
            public TransformAspect.Lookup                       transformLookup;

            public void Execute(Entity entity)
            {
                if (mountEntity.Value == Entity.Null)
                    return;

                var worldTransform            = transformLookup[mountEntity.Value];
                var cameraTransform           = transformLookup[entity];
                cameraTransform.worldRotation = worldTransform.worldRotation;
                cameraTransform.worldPosition = worldTransform.worldPosition;
            }
        }
    }
}

