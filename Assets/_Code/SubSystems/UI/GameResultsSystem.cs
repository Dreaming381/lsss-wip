using Latios;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

using static Unity.Entities.SystemAPI;

namespace Lsss
{
    public partial class GameResultsSystem : SubSystem
    {
        protected override void OnUpdate()
        {
            var resultEntities = QueryBuilder().WithAll<GameResultReference.ExistComponent>().Build().ToEntityArray(Allocator.Temp);
            foreach(var entity in resultEntities)
            {
                var result                   = latiosWorldUnmanaged.GetManagedStructComponent<GameResultReference>(entity).gameResult;
                UnityEngine.Cursor.lockState = UnityEngine.CursorLockMode.None;
                UnityEngine.Cursor.visible   = true;
                if (result.mainMenu)
                {
                    latiosWorld.syncPoint.CreateEntityCommandBuffer().AddComponent(sceneBlackboardEntity, new RequestLoadScene { newScene = "Title and Menu" });
                }
                else if (result.retry)
                {
                    latiosWorld.syncPoint.CreateEntityCommandBuffer().AddComponent(sceneBlackboardEntity, new RequestLoadScene
                    {
                        newScene = worldBlackboardEntity.GetComponentData<CurrentScene>().previous
                    });
                }
            }
        }
    }
}

