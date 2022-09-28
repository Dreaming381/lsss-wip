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
    public partial struct CameraFollowPlayerSystem : ISystem
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
            var mountEntity = new NativeReference<EntityWith<LocalToWorld> >(state.WorldUnmanaged.UpdateAllocator.ToAllocator);
            var ltwCdfe     = state.GetComponentLookup<LocalToWorld>(true);

            new JobA { mountEntity = mountEntity }.Schedule();
            new JobB { mountEntity = mountEntity, ltwClu = ltwCdfe }.Schedule();
        }

        [BurstCompile]
        [WithAll(typeof(PlayerTag))]
        partial struct JobA : IJobEntity
        {
            public NativeReference<EntityWith<LocalToWorld> > mountEntity;

            public void Execute(in CameraMountPoint mount)
            {
                mountEntity.Value = mount.mountPoint;
            }
        }

        [BurstCompile]
        [WithAll(typeof(CameraManager))]
        partial struct JobB : IJobEntity
        {
            public NativeReference<EntityWith<LocalToWorld> > mountEntity;
            [ReadOnly] public ComponentLookup<LocalToWorld>   ltwClu;

            public void Execute(ref Translation translation, ref Rotation rotation)
            {
                if (mountEntity.Value == Entity.Null)
                    return;

                var ltw           = ltwClu[mountEntity.Value];
                translation.Value = ltw.Position;
                rotation.Value    = quaternion.LookRotationSafe(ltw.Forward, ltw.Up);
            }
        }
    }
}

