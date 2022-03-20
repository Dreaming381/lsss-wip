using Latios;

namespace Lsss.SuperSystems
{
    public class AiUpdateSuperSystem : SuperSystem
    {
        protected override void CreateSystems()
        {
            GetOrCreateAndAddSystem<AiShipRadarScanSystem>();
            GetOrCreateAndAddSystem<AiSearchAndDestroyInitializePersonalitySystem>();
            GetOrCreateAndAddUnmanagedSystem<AiSearchAndDestroySystem>();
            GetOrCreateAndAddSystem<AiExploreInitializePersonalitySystem>();
            GetOrCreateAndAddUnmanagedSystem<AiExploreSystem>();
            GetOrCreateAndAddUnmanagedSystem<AiEvaluateGoalsSystem>();
            GetOrCreateAndAddUnmanagedSystem<AiCreateDesiredActionsSystem>();
        }
    }
}

