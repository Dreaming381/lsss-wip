using Latios;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace Lsss
{
    public partial class CameraFollowPlayerSystem : SubSystem
    {
        EntityQuery m_query;

        protected override void OnCreate()
        {
            m_query = Fluent.WithAll<CameraMountPoint>(true).WithAll<PlayerTag>().Build();
        }

        protected override void OnUpdate()
        {
            var mounts = m_query.ToComponentDataArray<CameraMountPoint>(Allocator.TempJob);
            if (mounts.Length > 0)
            {
                Entities.WithAll<CameraManager>().ForEach((ref Translation translation, ref Rotation rotation) =>
                {
                    var mountEntity = mounts[0].mountPoint;
                    if (mountEntity == Entity.Null)
                        return;

                    var ltw           = GetComponent<LocalToWorld>(mountEntity);
                    translation.Value = ltw.Position;
                    rotation.Value    = quaternion.LookRotationSafe(ltw.Forward, ltw.Up);
                }).Schedule();
                Dependency = mounts.Dispose(Dependency);
            }
            else //Dispose without modifying dependency so SystemBase can take the fastpath.
                mounts.Dispose();
        }
    }
}

