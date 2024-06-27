using Latios;

namespace Lsss.SuperSystems
{
    public partial class AiUpdateSuperSystem : SuperSystem
    {
        protected override void CreateSystems()
        {
            GetOrCreateAndAddUnmanagedSystem<AiUpdateRadarScanRequestsSystem>();
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

