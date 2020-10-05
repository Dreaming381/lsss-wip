using Latios;
using Lsss.Tools;
using Unity.Entities;
using Unity.Transforms;

namespace Lsss.SuperSystems
{
    [UpdateInGroup(typeof(Latios.Systems.LatiosSyncPointGroup))]
    public class LsssInitializationRootSuperSystem : RootSuperSystem
    {
        protected override void CreateSystems()
        {
            GetOrCreateAndAddSystem<GameplaySyncPointSuperSystem>();
            GetOrCreateAndAddSystem<AiInitializeSuperSystem>();

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

            //In here rather than LsssPresentationRootSuperSystem so that I can see the health readout that triggered death.
            //GetOrCreateAndAddSystem<UiGameplaySuperSystem>();
        }
    }

    [UpdateInGroup(typeof(PresentationSystemGroup), OrderFirst = true)]
    public class LsssPresentationRootSuperSystem : RootSuperSystem
    {
        protected override void CreateSystems()
        {
            GetOrCreateAndAddSystem<UiMainMenuSuperSystem>();
            GetOrCreateAndAddSystem<UiResultsSuperSystem>();
            GetOrCreateAndAddSystem<ShaderPropertySuperSystem>();
            GetOrCreateAndAddSystem<ProfilingDisplayUpdateSystem>();
            GetOrCreateAndAddSystem<UiGameplaySuperSystem>();
        }
    }
}

