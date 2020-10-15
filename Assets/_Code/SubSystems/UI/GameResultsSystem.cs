using Latios;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace Lsss
{
    public class GameResultsSystem : SubSystem
    {
        protected override void OnUpdate()
        {
            Entities.ForEach((GameResult result) =>
            {
                UnityEngine.Cursor.lockState = UnityEngine.CursorLockMode.None;
                UnityEngine.Cursor.visible   = true;
                if (result.mainMenu)
                {
                    latiosWorld.SyncPoint.CreateEntityCommandBuffer().AddComponent(sceneGlobalEntity, new RequestLoadScene { newScene = "Title and Menu" });
                }
                else if (result.retry)
                {
                    latiosWorld.SyncPoint.CreateEntityCommandBuffer().AddComponent(sceneGlobalEntity, new RequestLoadScene {
                        newScene = worldGlobalEntity.GetComponentData<CurrentScene>().previous
                    });
                }
            }).WithoutBurst().Run();
        }
    }
}

