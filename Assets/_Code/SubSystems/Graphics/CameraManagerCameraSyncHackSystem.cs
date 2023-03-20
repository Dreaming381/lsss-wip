using Latios;
using Latios.Transforms;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.Jobs;

//Todo: Use order versions to detect changes
namespace Lsss
{
    public partial class CameraManagerCameraSyncHackSystem : SubSystem
    {
        TransformAccessArray m_transforms;
        EntityQuery          m_query;

        protected override void OnCreate()
        {
            m_transforms = new TransformAccessArray(1);
            m_query      = Fluent.WithAll<CameraManager>().WithAll<WorldTransform>(true).Build();
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
                if (manager.camera == null)
                    manager.camera = UnityEngine.Camera.main;

                m_transforms.Add(manager.camera.transform);
            }).WithoutBurst().Run();

            var ltws = m_query.ToComponentDataListAsync<WorldTransform>(Allocator.TempJob, out var jh2);

            Dependency = new CopyTransformsJob
            {
                worldTransforms = ltws.AsDeferredJobArray()
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
            [ReadOnly] public NativeArray<WorldTransform> worldTransforms;

            public void Execute(int index, TransformAccess transform)
            {
                transform.rotation = worldTransforms[index].rotation;
                transform.position = worldTransforms[index].position;
            }
        }
    }
}

