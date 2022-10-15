using System.Diagnostics;
using Unity.Collections;
using Unity.Entities;
using UnityEngine.SceneManagement;

namespace Latios.Systems
{
    [DisableAutoCreation]
    [UpdateInGroup(typeof(LatiosInitializationSystemGroup), OrderFirst = true)]
    [UpdateAfter(typeof(SyncPointPlaybackSystemDispatch))]
    public partial class SceneManagerSystem : SubSystem
    {
        private EntityQuery m_rlsQuery;
        private EntityQuery m_unitySubsceneLoadQuery;
        private EntityQuery m_dontDestroyOnSceneChangeQuery;
        private bool        m_paused = false;

        protected override void OnCreate()
        {
            CurrentScene curr = new CurrentScene
            {
                currentScene      = new FixedString128Bytes(),
                previousScene     = new FixedString128Bytes(),
                isSceneFirstFrame = false
            };
            worldBlackboardEntity.AddComponentData(curr);

            latiosWorld.autoGenerateSceneBlackboardEntity = false;

            m_unitySubsceneLoadQuery        = Fluent.WithAll<Unity.Entities.RequestSceneLoaded>().Build();
            m_dontDestroyOnSceneChangeQuery = Fluent.WithAll<Unity.Entities.SceneTag>().WithAll<DontDestroyOnSceneChangeTag>().IncludeDisabled().IncludePrefabs().Build();
        }

        protected override void OnUpdate()
        {
            if (!m_rlsQuery.IsEmptyIgnoreFilter)
            {
                FixedString128Bytes targetScene = new FixedString128Bytes();
                bool                isInvalid   = false;

                Entities.WithStoreEntityQueryInField(ref m_rlsQuery).ForEach((ref RequestLoadScene rls) =>
                {
                    if (rls.newScene.Length == 0)
                        return;
                    if (targetScene.Length == 0)
                        targetScene = rls.newScene;
                    else if (rls.newScene != targetScene)
                        isInvalid = true;
                }).Run();

                if (targetScene.Length > 0)
                {
                    if (isInvalid)
                    {
                        UnityEngine.Debug.LogError("Multiple scenes were requested to load during the previous frame.");
                        EntityManager.RemoveComponent<RequestLoadScene>(m_rlsQuery);
                    }
                    else
                    {
                        var curr           = worldBlackboardEntity.GetComponentData<CurrentScene>();
                        curr.previousScene = curr.currentScene;
                        EntityManager.RemoveComponent<Unity.Entities.SceneTag>(m_dontDestroyOnSceneChangeQuery);
                        UnityEngine.Debug.Log("Loading scene: " + targetScene);
                        SceneManager.LoadScene(targetScene.ToString());
                        latiosWorld.Pause();
                        m_paused               = true;
                        curr.currentScene      = targetScene;
                        curr.isSceneFirstFrame = true;
                        worldBlackboardEntity.SetComponentData(curr);
                        EntityManager.RemoveComponent<RequestLoadScene>(m_rlsQuery);
                        return;
                    }
                }
            }

            //Handle case where initial scene loads or set firstFrame to false
            var currentScene = worldBlackboardEntity.GetComponentData<CurrentScene>();
            if (currentScene.currentScene.Length == 0)
            {
                currentScene.currentScene      = SceneManager.GetActiveScene().name;
                currentScene.isSceneFirstFrame = true;
                latiosWorld.CreateNewSceneBlackboardEntity();
            }
            else if (m_paused)
            {
                m_paused = false;
            }
            else
            {
                currentScene.isSceneFirstFrame = false;
            }
            worldBlackboardEntity.SetComponentData(currentScene);

            var subsceneRequests = m_unitySubsceneLoadQuery.ToComponentDataArray<RequestSceneLoaded>(Allocator.Temp);
            var subsceneEntities = m_unitySubsceneLoadQuery.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < subsceneEntities.Length; i++)
            {
                var subsceneEntity = subsceneEntities[i];
                var request        = subsceneRequests[i];
                if ((request.LoadFlags & SceneLoadFlags.DisableAutoLoad) == 0)
                {
                    request.LoadFlags |= SceneLoadFlags.BlockOnStreamIn;
                    EntityManager.SetComponentData(subsceneEntity, request);
                    if (EntityManager.HasComponent<Unity.Scenes.ResolvedSectionEntity>(subsceneEntity))
                    {
                        foreach (var s in EntityManager.GetBuffer<Unity.Scenes.ResolvedSectionEntity>(subsceneEntity))
                            EntityManager.AddComponentData(s.SectionEntity, request);
                    }
                }
            }
        }
    }
}

