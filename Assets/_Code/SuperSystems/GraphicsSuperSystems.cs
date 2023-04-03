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
            GetOrCreateAndAddUnmanagedSystem<SpawnPointAnimationSystem>();

            //Debug until playerloop fixed so camera doesn't jitter
            //GetOrCreateAndAddSystem<TransformSystemGroup>();
            //GetOrCreateAndAddSystem<CompanionGameObjectUpdateTransformSystem>();  //Todo: Namespace
        }
    }

    public partial class ShaderPropertySuperSystem : SuperSystem
    {
        protected override void CreateSystems()
        {
            //GetOrCreateAndAddSystem<GravityWarpShaderUpdateSystem>();
            GetOrCreateAndAddUnmanagedSystem<LifetimeFadeSystem>();
            GetOrCreateAndAddUnmanagedSystem<SpeedShaderUpdateSystem>();
            GetOrCreateAndAddManagedSystem<SetCameraDrawDistanceSystem>();
            GetOrCreateAndAddManagedSystem<CameraManagerCameraSyncHackSystem>();
        }
    }
}

