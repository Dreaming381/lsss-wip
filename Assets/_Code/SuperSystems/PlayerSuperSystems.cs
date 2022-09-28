using Latios;

namespace Lsss.SuperSystems
{
    /// <summary>
    /// Handles Player's in-game input and converts them to actions for the simulation.
    /// </summary>
    public class PlayerInGameSuperSystem : SuperSystem
    {
        protected override void CreateSystems()
        {
            GetOrCreateAndAddManagedSystem<PlayerGameplayReadInputSystem>();
        }
    }
}

