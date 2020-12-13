using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Lsss
{
    [AddComponentMenu("LSSS/UI/Game Result")]
    public class GameResult : MonoBehaviour
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
    }
}

