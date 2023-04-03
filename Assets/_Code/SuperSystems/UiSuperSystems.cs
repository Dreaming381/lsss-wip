using Latios;
using Lsss.Tools;

namespace Lsss.SuperSystems
{
    public partial class UiMainMenuSuperSystem : SuperSystem
    {
        protected override void CreateSystems()
        {
            GetOrCreateAndAddManagedSystem<TitleAndMenuUpdateSystem>();
        }
    }

    public partial class UiGameplaySuperSystem : SuperSystem
    {
        protected override void CreateSystems()
        {
            GetOrCreateAndAddManagedSystem<HudUpdateSystem>();
        }
    }

    public partial class UiResultsSuperSystem : SuperSystem
    {
        protected override void CreateSystems()
        {
            GetOrCreateAndAddManagedSystem<GameResultsSystem>();
        }
    }
}

