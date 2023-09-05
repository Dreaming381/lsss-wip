using Latios;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

using static Unity.Entities.SystemAPI;

namespace Lsss
{
    public partial class SetCameraDrawDistanceSystem : SubSystem
    {
        UnityEngine.Camera lastSeenCamera;
        int                lastSeenQualityLevel = -1;

        protected override void OnCreate()
        {
            if (!worldBlackboardEntity.HasComponent<GraphicsQualityLevel>())
                worldBlackboardEntity.AddComponentData(new GraphicsQualityLevel());
        }

        protected override void OnUpdate()
        {
            var qualityLevel = worldBlackboardEntity.GetComponentData<GraphicsQualityLevel>();

            foreach((var distances, var entity) in Query<RefRO<DrawDistances> >().WithEntityAccess().WithAll<CameraManager.ExistComponent>())
            {
                var camera = latiosWorldUnmanaged.GetManagedStructComponent<CameraManager>(entity).camera;
                if (camera == null)
                    return;
                if (qualityLevel.level != lastSeenQualityLevel || camera != lastSeenCamera)
                {
                    camera.farClipPlane  = distances.ValueRO.distances[math.min(distances.ValueRO.distances.Length - 1, qualityLevel.level)];
                    lastSeenCamera       = camera;
                    lastSeenQualityLevel = qualityLevel.level;
                }
            }
        }
    }
}

