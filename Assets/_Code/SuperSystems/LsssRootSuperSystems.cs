using Latios;
using Lsss.Tools;
using Unity.Entities;

namespace Lsss.SuperSystems
{
    [UpdateInGroup(typeof(Latios.Systems.PreSyncPointGroup))]
    public class LsssPreSyncRootSuperSystem : RootSuperSystem
    {
        protected override void CreateSystems()
        {
            GetOrCreateAndAddManagedSystem<BeginFrameProfilingSystem>();
        }
    }

    [UpdateInGroup(typeof(Latios.Systems.LatiosWorldSyncGroup), OrderLast = true)]
    public class LsssInitializationRootSuperSystem : RootSuperSystem
    {
        protected override void CreateSystems()
        {
            GetOrCreateAndAddManagedSystem<GameplaySyncPointSuperSystem>();

            GetOrCreateAndAddManagedSystem<Latios.Transforms.Systems.TransformSuperSystem>();
            //GetOrCreateAndAddManagedSystem<CompanionGameObjectUpdateTransformSystem>();  //Todo: Namespace
        }
    }

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(Latios.Transforms.Systems.TransformSuperSystem))]
    public class LsssPreTransformRootSuperSystem : RootSuperSystem
    {
        protected override void CreateSystems()
        {
            GetOrCreateAndAddManagedSystem<PlayerInGameSuperSystem>();
            GetOrCreateAndAddManagedSystem<AdvanceGameplayMotionSuperSystem>();
        }
    }

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Latios.Transforms.Systems.TransformSuperSystem))]
    public class LsssPostTransformRootSuperSystem : RootSuperSystem
    {
        protected override void CreateSystems()
        {
            GetOrCreateAndAddManagedSystem<UpdateTransformSpatialQueriesSuperSystem>();
            GetOrCreateAndAddManagedSystem<AiUpdateSuperSystem>();
            GetOrCreateAndAddManagedSystem<ProcessGameplayEventsSuperSystem>();
            GetOrCreateAndAddManagedSystem<GraphicsTransformsSuperSystem>();
        }
    }

    [UpdateInGroup(typeof(PresentationSystemGroup), OrderFirst = true)]
    public class LsssPresentationRootSuperSystem : RootSuperSystem
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

