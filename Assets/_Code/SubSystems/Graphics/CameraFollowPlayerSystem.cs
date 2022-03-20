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
        EntityQuery m_query;

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
            if (m_query.IsEmpty)
                return;

            var mounts = state.WorldUnmanaged.UpdateAllocator.AllocateNativeArray<CameraMountPoint>(1);

            state.Entities.WithAll<PlayerTag>().WithStoreEntityQueryInField(ref m_query).ForEach((in CameraMountPoint mount) => { mounts[0] = mount; }).Schedule();

            var ltwCdfe = state.GetComponentDataFromEntity<LocalToWorld>(true);
            if (mounts.Length > 0)
            {
                state.Entities.WithAll<CameraManager>().ForEach((ref Translation translation, ref Rotation rotation) =>
                {
                    var mountEntity = mounts[0].mountPoint;
                    if (mountEntity == Entity.Null)
                        return;

                    var ltw           = ltwCdfe[mountEntity];
                    translation.Value = ltw.Position;
                    rotation.Value    = quaternion.LookRotationSafe(ltw.Forward, ltw.Up);
                }).WithReadOnly(ltwCdfe).Schedule();
            }
        }
    }
}

