using Latios;
using Latios.Transforms;
using Unity.Entities;
using UnityEngine;

namespace Lsss
{
    [AddComponentMenu("LSSS/UI/Game Result")]
    public class GameResult : MonoBehaviour, IInitializeGameObjectEntity
    {
        [HideInInspector] public bool retry;
        [HideInInspector] public bool mainMenu;

        private void Awake()
        {
            Debug.Log("Results screen live");
        }

        public void SetNextAction(bool isRetryAndNotMainMenu)
        {
            retry    = isRetryAndNotMainMenu;
            mainMenu = !isRetryAndNotMainMenu;
        }

        public void Initialize(LatiosWorld latiosWorld, Entity gameObjectEntity)
        {
            latiosWorld.latiosWorldUnmanaged.AddManagedStructComponent(gameObjectEntity, new GameResultReference { gameResult = this });
        }
    }

    public partial struct GameResultReference : IManagedStructComponent
    {
        public GameResult gameResult;
    }
}

