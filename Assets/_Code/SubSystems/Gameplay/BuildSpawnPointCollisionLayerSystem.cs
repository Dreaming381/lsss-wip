using Latios;
using Latios.Psyshock;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace Lsss
{
    public partial class BuildSpawnPointCollisionLayerSystem : SubSystem
    {
        private EntityQuery m_query;

        protected override void OnCreate()
        {
            m_query = Fluent.WithAll<SpawnPointTag>(true).PatchQueryForBuildingCollisionLayer().Build();
        }

        public override void OnNewScene() => sceneBlackboardEntity.AddOrSetCollectionComponentAndDisposeOld(new SpawnPointCollisionLayer());

        protected override void OnUpdate()
        {
            CollisionLayerSettings settings;
            if (sceneBlackboardEntity.HasComponent<ArenaCollisionSettings>())
                settings = sceneBlackboardEntity.GetComponentData<ArenaCollisionSettings>().settings;
            else
                settings        = BuildCollisionLayerConfig.defaultSettings;
            Dependency          = Physics.BuildCollisionLayer(m_query, this).WithSettings(settings).ScheduleParallel(out CollisionLayer layer, Allocator.Persistent, Dependency);
            var spawnPointLayer = new SpawnPointCollisionLayer { layer = layer };
            sceneBlackboardEntity.SetCollectionComponentAndDisposeOld(spawnPointLayer);
        }
    }

    public partial class DebugDrawSpawnPointCollisionLayersSystem : SubSystem
    {
        protected override void OnUpdate()
        {
            var layer = sceneBlackboardEntity.GetCollectionComponent<SpawnPointCollisionLayer>(true).layer;
            CompleteDependency();
            PhysicsDebug.DrawLayer(layer).Run();
            UnityEngine.Debug.Log("SpawnPoints in layer: " + layer.Count);
        }
    }
}

