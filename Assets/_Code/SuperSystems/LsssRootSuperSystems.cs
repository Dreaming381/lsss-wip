using Latios;
using Lsss.Tools;
using Unity.Entities;

namespace Lsss.SuperSystems
{
    [UpdateInGroup(typeof(Latios.Systems.PreSyncPointGroup))]
    public partial class LsssPreSyncRootSuperSystem : RootSuperSystem
    {
        protected override void CreateSystems()
        {
            GetOrCreateAndAddManagedSystem<BeginFrameProfilingSystem>();
        }
    }

    [UpdateInGroup(typeof(Latios.Systems.LatiosWorldSyncGroup), OrderLast = true)]
    public partial class LsssInitializationRootSuperSystem : RootSuperSystem
    {
        protected override void CreateSystems()
        {
            GetOrCreateAndAddManagedSystem<GameplaySyncPointSuperSystem>();
        }
    }

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class LsssSimulationRootSuperSystem : RootSuperSystem
    {
        protected override void CreateSystems()
        {
            GetOrCreateAndAddManagedSystem<PlayerInGameSuperSystem>();
            GetOrCreateAndAddManagedSystem<AdvanceGameplayMotionSuperSystem>();

            GetOrCreateAndAddManagedSystem<UpdateTransformSpatialQueriesSuperSystem>();
            GetOrCreateAndAddManagedSystem<AiUpdateSuperSystem>();
            GetOrCreateAndAddManagedSystem<ProcessGameplayEventsSuperSystem>();
            GetOrCreateAndAddManagedSystem<GraphicsTransformsSuperSystem>();
        }
    }

    [UpdateInGroup(typeof(PresentationSystemGroup), OrderFirst = true)]
    public partial class LsssPresentationRootSuperSystem : RootSuperSystem
    {
        protected override void CreateSystems()
        {
            GetOrCreateAndAddManagedSystem<UiMainMenuSuperSystem>();
            GetOrCreateAndAddManagedSystem<UiResultsSuperSystem>();
            GetOrCreateAndAddManagedSystem<AudioSuperSystem>();
            GetOrCreateAndAddManagedSystem<ShaderPropertySuperSystem>();
            GetOrCreateAndAddManagedSystem<ProfilingDisplayUpdateSystem>();
            GetOrCreateAndAddManagedSystem<UiGameplaySuperSystem>();
        }
    }
}

