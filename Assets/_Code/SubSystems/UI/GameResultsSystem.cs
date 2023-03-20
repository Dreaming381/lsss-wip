using Latios;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace Lsss
{
    public partial class GameResultsSystem : SubSystem
    {
        protected override void OnUpdate()
        {
            Entities.ForEach((GameResult result) =>
            {
                UnityEngine.Cursor.lockState = UnityEngine.CursorLockMode.None;
                UnityEngine.Cursor.visible   = true;
                if (result.mainMenu)
                {
                    latiosWorld.syncPoint.CreateEntityCommandBuffer().AddComponent(sceneBlackboardEntity, new RequestLoadScene { newScene = "Title and Menu" });
                }
                else if (result.retry)
                {
                    latiosWorld.syncPoint.CreateEntityCommandBuffer().AddComponent(sceneBlackboardEntity, new RequestLoadScene {
                        newScene = worldBlackboardEntity.GetComponentData<CurrentScene>().previous
                    });
                }
            }).WithoutBurst().Run();
        }
    }
}

