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
    [AlwaysUpdateSystem]
    public partial class BuildSpawnPointCollisionLayerSystem : SubSystem
    {
        private EntityQuery m_query;

        protected override void OnCreate()
        {
            m_query = Fluent.WithAll<SpawnPointTag>(true).PatchQueryForBuildingCollisionLayer().Build();
        }

        public override void OnNewScene() => sceneBlackboardEntity.AddCollectionComponent(new SpawnPointCollisionLayer(), false);

        protected override void OnUpdate()
        {
            Dependency          = Physics.BuildCollisionLayer(m_query, this).ScheduleParallel(out CollisionLayer layer, Allocator.Persistent, Dependency);
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

