using Latios;

namespace Lsss.SuperSystems
{
    public class AiUpdateSuperSystem : SuperSystem
    {
        protected override void CreateSystems()
        {
            GetOrCreateAndAddUnmanagedSystem<AiShipRadarScanSystem>();
            GetOrCreateAndAddUnmanagedSystem<AiSearchAndDestroyInitializePersonalitySystem>();
            GetOrCreateAndAddUnmanagedSystem<AiSearchAndDestroySystem>();
            GetOrCreateAndAddUnmanagedSystem<AiExploreInitializePersonalitySystem>();
            GetOrCreateAndAddUnmanagedSystem<AiExploreSystem>();
            GetOrCreateAndAddUnmanagedSystem<AiEvaluateGoalsSystem>();
            GetOrCreateAndAddUnmanagedSystem<AiCreateDesiredActionsSystem>();
        }
    }
}

