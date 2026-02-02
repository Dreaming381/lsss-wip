using Latios;
using Unity.Transforms;

namespace Lsss.SuperSystems
{
    /// <summary>
    /// Updates transforms of graphics-exclusive items (Cameras, billboards, ect)
    /// </summary>
    public partial class GraphicsTransformsSuperSystem : SuperSystem
    {
        protected override void CreateSystems()
        {
            GetOrCreateAndAddUnmanagedSystem<CameraFollowPlayerSystem>();
            GetOrCreateAndAddUnmanagedSystem<FaceCameraSystem>();
        }
    }

    public partial class GraphicsPresentationSuperSystem : SuperSystem
    {
        protected override void CreateSystems()
        {
            GetOrCreateAndAddUnmanagedSystem<SpawnPointAnimationSystem>();
            //GetOrCreateAndAddSystem<GravityWarpShaderUpdateSystem>();
            GetOrCreateAndAddUnmanagedSystem<LifetimeFadeSystem>();
            GetOrCreateAndAddUnmanagedSystem<SpeedShaderUpdateSystem>();
            GetOrCreateAndAddManagedSystem<SetCameraDrawDistanceSystem>();
        }
    }
}

