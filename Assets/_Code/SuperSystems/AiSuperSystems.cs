using Latios;

namespace Lsss.SuperSystems
{
    public class AiInitializeSuperSystem : SuperSystem
    {
        protected override void CreateSystems()
        {
            GetOrCreateAndAddSystem<AiInitializeNewShipsSystem>();
        }
    }

    public class AiUpdateSuperSystem : SuperSystem
    {
        protected override void CreateSystems()
        {
            GetOrCreateAndAddSystem<AiShipRadarScanSystem>();
            GetOrCreateAndAddSystem<AiEvaluateGoalsSystem>();
            GetOrCreateAndAddSystem<AiCreateDesiredActionsSystem>();
        }
    }
}

