using Unity.Entities;
using UnityEngine.SceneManagement;

namespace Latios.Systems
{
    internal struct LatiosSceneChangeDummyTag : IComponentData { }

    [AlwaysUpdateSystem]
    public class DestroyEntitiesOnSceneChangeSystem : SubSystem
    {
        private EntityQuery m_destroyQuery = default;

        protected override void OnCreate()
        {
            m_destroyQuery              = Fluent.WithAll<LatiosSceneChangeDummyTag>().Without<WorldGlobalTag>().Without<DontDestroyOnSceneChangeTag>().Build();
            SceneManager.sceneUnloaded += RealUpdateOnSceneChange;
        }

        protected override void OnDestroy()
        {
            SceneManager.sceneUnloaded -= RealUpdateOnSceneChange;
        }

        protected override void OnUpdate()
        {
        }

        private void RealUpdateOnSceneChange(Scene unloaded)
        {
            if (unloaded.isSubScene)
                return;

            EntityManager.AddComponent<LatiosSceneChangeDummyTag>(EntityManager.UniversalQuery);
            EntityManager.DestroyEntity(m_destroyQuery);
            EntityManager.RemoveComponent<LatiosSceneChangeDummyTag>(EntityManager.UniversalQuery);
            latiosWorld.CreateNewSceneGlobalEntity();
        }
    }
}

