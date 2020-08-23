using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Lsss
{
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

