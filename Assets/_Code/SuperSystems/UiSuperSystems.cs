using Latios;
using Lsss.Tools;

namespace Lsss.SuperSystems
{
    public class UiMainMenuSuperSystem : SuperSystem
    {
        protected override void CreateSystems()
        {
            GetOrCreateAndAddManagedSystem<TitleAndMenuUpdateSystem>();
        }
    }

    public class UiGameplaySuperSystem : SuperSystem
    {
        protected override void CreateSystems()
        {
            GetOrCreateAndAddManagedSystem<HudUpdateSystem>();
        }
    }

    public class UiResultsSuperSystem : SuperSystem
    {
        protected override void CreateSystems()
        {
            GetOrCreateAndAddManagedSystem<GameResultsSystem>();
        }
    }
}

