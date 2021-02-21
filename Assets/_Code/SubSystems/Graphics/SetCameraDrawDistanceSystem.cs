using Latios;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace Lsss
{
    public class SetCameraDrawDistanceSystem : SubSystem
    {
        UnityEngine.Camera lastSeenCamera;
        int                lastSeenQualityLevel = -1;

        protected override void OnCreate()
        {
            if (!worldBlackboardEntity.HasComponentData<GraphicsQualityLevel>())
                worldBlackboardEntity.AddComponentData(new GraphicsQualityLevel());
        }

        protected override void OnUpdate()
        {
            var qualityLevel = worldBlackboardEntity.GetComponentData<GraphicsQualityLevel>();

            Entities.ForEach((CameraManager camera, in DrawDistances distances) =>
            {
                if (qualityLevel.level != lastSeenQualityLevel || camera.camera != lastSeenCamera)
                {
                    camera.camera.farClipPlane = distances.distances[math.min(distances.distances.Length - 1, qualityLevel.level)];
                    lastSeenCamera             = camera.camera;
                    lastSeenQualityLevel       = qualityLevel.level;
                }
            }).WithoutBurst().Run();
        }
    }
}

