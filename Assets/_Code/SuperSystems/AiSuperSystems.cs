using Latios;

namespace Lsss.SuperSystems
{
    public class AiUpdateSuperSystem : SuperSystem
    {
        protected override void CreateSystems()
        {
            GetOrCreateAndAddManagedSystem<AiShipRadarScanSystem>();
            GetOrCreateAndAddManagedSystem<AiSearchAndDestroyInitializePersonalitySystem>();
            GetOrCreateAndAddUnmanagedSystem<AiSearchAndDestroySystem>();
            GetOrCreateAndAddManagedSystem<AiExploreInitializePersonalitySystem>();
            GetOrCreateAndAddUnmanagedSystem<AiExploreSystem>();
            GetOrCreateAndAddUnmanagedSystem<AiEvaluateGoalsSystem>();
            GetOrCreateAndAddUnmanagedSystem<AiCreateDesiredActionsSystem>();
        }
    }
}

