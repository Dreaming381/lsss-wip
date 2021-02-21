using Latios;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine.Jobs;

//Todo: Use order versions to detect changes
namespace Lsss
{
    public class CameraManagerCameraSyncHackSystem : SubSystem
    {
        TransformAccessArray m_transforms;
        EntityQuery          m_query;

        protected override void OnCreate()
        {
            m_transforms = new TransformAccessArray(1);
            m_query      = Fluent.WithAll<CameraManager>().WithAll<LocalToWorld>(true).Build();
        }

        protected override void OnUpdate()
        {
            int queryLength = m_query.CalculateEntityCount();
            if (queryLength > m_transforms.capacity)
            {
                m_transforms.Dispose();
                m_transforms = new TransformAccessArray(queryLength);
            }

            int transformsLength = m_transforms.length;
            for (int i = 0; i < transformsLength; i++)
            {
                m_transforms.RemoveAtSwapBack(i);
            }

            var jh     = Dependency;
            Dependency = default;
            Entities.ForEach((CameraManager manager) =>
            {
                m_transforms.Add(manager.camera.transform);
            }).WithoutBurst().Run();

            var ltws = m_query.ToComponentDataArrayAsync<LocalToWorld>(Allocator.TempJob, out var jh2);

            Dependency = new CopyTransformsJob
            {
                ltws = ltws
            }.Schedule(m_transforms, JobHandle.CombineDependencies(jh, jh2));

            Dependency = ltws.Dispose(Dependency);
        }

        protected override void OnDestroy()
        {
            m_transforms.Dispose();
        }

        [BurstCompile]
        struct CopyTransformsJob : IJobParallelForTransform
        {
            [ReadOnly] public NativeArray<LocalToWorld> ltws;

            public void Execute(int index, TransformAccess transform)
            {
                transform.position = ltws[index].Position;
                var rotation       = quaternion.LookRotationSafe(ltws[index].Forward, ltws[index].Up);
                transform.rotation = rotation;
            }
        }
    }
}

