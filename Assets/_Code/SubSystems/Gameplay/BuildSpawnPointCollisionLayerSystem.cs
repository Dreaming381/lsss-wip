using Latios;
using Latios.PhysicsEngine;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace Lsss
{
    [AlwaysUpdateSystem]
    public class BuildSpawnPointCollisionLayerSystem : SubSystem
    {
        private EntityQuery m_query;

        protected override void OnCreate()
        {
            m_query = Fluent.WithAll<SpawnPointTag>(true).PatchQueryForBuildingCollisionLayer().Build();
        }

        protected override void OnUpdate()
        {
            Dependency          = Physics.BuildCollisionLayer(m_query, this).ScheduleParallel(out CollisionLayer layer, Allocator.Persistent, Dependency);
            var spawnPointLayer = new SpawnPointCollisionLayer { layer = layer };
            if (sceneGlobalEntity.HasCollectionComponent<SpawnPointCollisionLayer>())
            {
                sceneGlobalEntity.SetCollectionComponentAndDisposeOld(spawnPointLayer);
            }
            else
            {
                //Some bizarre bug exists that requires dependencies to be completed before calling EntityManager.AddComponent
                //At least this only happens on the first frame of the scene.
                CompleteDependency();

                sceneGlobalEntity.AddCollectionComponent(spawnPointLayer);
            }
        }
    }

    public class DebugDrawSpawnPointCollisionLayersSystem : SubSystem
    {
        protected override void OnUpdate()
        {
            var layer = sceneGlobalEntity.GetCollectionComponent<SpawnPointCollisionLayer>(true).layer;
            CompleteDependency();
            PhysicsDebug.DrawLayer(layer);
            UnityEngine.Debug.Log("SpawnPoints in layer: " + layer.Count);
        }
    }
}

