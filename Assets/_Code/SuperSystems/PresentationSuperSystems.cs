using Latios;
using Latios.Transforms;
using Lsss.SuperSystems;
using Lsss.Tools;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace Lsss.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup), OrderFirst = true)]
    public partial class LsssPresentationRootSuperSystem : RootSuperSystem
    {
        protected override void CreateSystems()
        {
            // Audio
            GetOrCreateAndAddUnmanagedSystem<AudioVolumeSystem>();

            // Animation and Effects
            GetOrCreateAndAddUnmanagedSystem<SpawnPointAnimationSystem>();
            //GetOrCreateAndAddSystem<GravityWarpShaderUpdateSystem>();
            GetOrCreateAndAddUnmanagedSystem<LifetimeFadeSystem>();
            GetOrCreateAndAddUnmanagedSystem<SpeedShaderUpdateSystem>();

            // LOD
            GetOrCreateAndAddManagedSystem<SetCameraDrawDistanceSystem>();

            // UI
            GetOrCreateAndAddManagedSystem<ProfilingDisplayUpdateSystem>();
            GetOrCreateAndAddManagedSystem<TitleAndMenuUpdateSystem>();
            GetOrCreateAndAddManagedSystem<GameResultsSystem>();
            GetOrCreateAndAddManagedSystem<HudUpdateSystem>();
        }
    }
}

