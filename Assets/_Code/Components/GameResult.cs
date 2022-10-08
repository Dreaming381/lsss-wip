using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace Lsss
{
    [AddComponentMenu("LSSS/UI/Game Result")]
    public class GameResult : MonoBehaviour
    {
        [HideInInspector] public bool retry;
        [HideInInspector] public bool mainMenu;

        private Entity entity = Entity.Null;

        private void Awake()
        {
            Debug.Log("Results screen live");
        }

        private void Update()
        {
            if (entity != Entity.Null)
                return;

            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            entity = em.CreateEntity();
            em.AddComponentObject(entity, this);
        }

        public void SetNextAction(bool isRetryAndNotMainMenu)
        {
            retry    = isRetryAndNotMainMenu;
            mainMenu = !isRetryAndNotMainMenu;
        }
    }
}

