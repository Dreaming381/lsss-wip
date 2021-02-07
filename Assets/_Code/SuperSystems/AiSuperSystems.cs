using Latios;

namespace Lsss.SuperSystems
{
    public class AiUpdateSuperSystem : SuperSystem
    {
        protected override void CreateSystems()
        {
            GetOrCreateAndAddSystem<AiShipRadarScanSystem>();
            GetOrCreateAndAddSystem<AiSearchAndDestroyInitializePersonalitySystem>();
            GetOrCreateAndAddSystem<AiSearchAndDestroySystem>();
            GetOrCreateAndAddSystem<AiExploreInitializePersonalitySystem>();
            GetOrCreateAndAddSystem<AiExploreSystem>();
            GetOrCreateAndAddSystem<AiEvaluateGoalsSystem>();
            GetOrCreateAndAddSystem<AiCreateDesiredActionsSystem>();
        }
    }
}

