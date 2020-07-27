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
        BeginInitializationEntityCommandBufferSystem m_ecbSystem;

        protected override void OnCreate()
        {
            m_ecbSystem = World.GetExistingSystem<BeginInitializationEntityCommandBufferSystem>();
        }

        protected override void OnUpdate()
        {
            Entities.ForEach((GameResult result) =>
            {
                if (result.mainMenu)
                {
                    m_ecbSystem.CreateCommandBuffer().AddComponent(sceneGlobalEntity, new RequestLoadScene { newScene = "Title and Menu" });
                    m_ecbSystem.AddJobHandleForProducer(Dependency);
                }
                else if (result.retry)
                {
                    m_ecbSystem.CreateCommandBuffer().AddComponent(sceneGlobalEntity, new RequestLoadScene {
                        newScene = worldGlobalEntity.GetComponentData<CurrentScene>().previous
                    });
                    m_ecbSystem.AddJobHandleForProducer(Dependency);
                }
            }).WithoutBurst().Run();
        }
    }
}

