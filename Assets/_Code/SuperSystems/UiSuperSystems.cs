using Latios;

namespace Lsss.SuperSystems
{
    public class UiMainMenuSuperSystem : SuperSystem
    {
        protected override void CreateSystems()
        {
            GetOrCreateAndAddSystem<TitleAndMenuUpdateSystem>();
        }
    }

    public class UiGameplaySuperSystem : SuperSystem
    {
        protected override void CreateSystems()
        {
            GetOrCreateAndAddSystem<HudUpdateSystem>();
        }
    }

    public class UiResultsSuperSystem : SuperSystem
    {
        protected override void CreateSystems()
        {
            GetOrCreateAndAddSystem<GameResultsSystem>();
        }
    }
}

