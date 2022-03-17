using Latios;
using Lsss.Tools;
using Unity.Entities;
using Unity.Transforms;

namespace Lsss.SuperSystems
{
    [UpdateInGroup(typeof(Latios.Systems.PreSyncPointGroup))]
    public class LsssPreSyncRootSuperSystem : RootSuperSystem
    {
        protected override void CreateSystems()
        {
            GetOrCreateAndAddSystem<BeginFrameProfilingSystem>();
            //GetOrCreateAndAddSystem<Latios.Myri.Systems.AudioSystem>();
        }
    }

    [UpdateInGroup(typeof(Latios.Systems.LatiosWorldSyncGroup))]
    public class LsssInitializationRootSuperSystem : RootSuperSystem
    {
        protected override void CreateSystems()
        {
            GetOrCreateAndAddSystem<GameplaySyncPointSuperSystem>();

            GetOrCreateAndAddSystem<TransformSystemGroup>();
            GetOrCreateAndAddSystem<CompanionGameObjectUpdateTransformSystem>();  //Todo: Namespace
        }
    }

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(TransformSystemGroup))]
    public class LsssPreTransformRootSuperSystem : RootSuperSystem
    {
        protected override void CreateSystems()
        {
            GetOrCreateAndAddSystem<PlayerInGameSuperSystem>();
            GetOrCreateAndAddSystem<AdvanceGameplayMotionSuperSystem>();
        }
    }

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(TransformSystemGroup))]
    public class LsssPostTransformRootSuperSystem : RootSuperSystem
    {
        protected override void CreateSystems()
        {
            GetOrCreateAndAddSystem<UpdateTransformSpatialQueriesSuperSystem>();
            GetOrCreateAndAddSystem<AiUpdateSuperSystem>();
            GetOrCreateAndAddSystem<ProcessGameplayEventsSuperSystem>();
            GetOrCreateAndAddSystem<GraphicsTransformsSuperSystem>();
        }
    }

    [UpdateInGroup(typeof(PresentationSystemGroup), OrderFirst = true)]
    public class LsssPresentationRootSuperSystem : RootSuperSystem
    {
        protected override void CreateSystems()
        {
            GetOrCreateAndAddSystem<UiMainMenuSuperSystem>();
            GetOrCreateAndAddSystem<UiResultsSuperSystem>();
            GetOrCreateAndAddSystem<AudioSuperSystem>();
            GetOrCreateAndAddSystem<ShaderPropertySuperSystem>();
            GetOrCreateAndAddSystem<ProfilingDisplayUpdateSystem>();
            GetOrCreateAndAddSystem<UiGameplaySuperSystem>();
        }
    }
}

