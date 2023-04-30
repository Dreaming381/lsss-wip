using Latios;
using Latios.Psyshock;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace Lsss
{
    [BurstCompile]
    public partial struct BuildSpawnPointCollisionLayerSystem : ISystem, ISystemNewScene
    {
        private EntityQuery m_query;

        BuildCollisionLayerTypeHandles m_handles;

        LatiosWorldUnmanaged latiosWorld;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            m_query = state.Fluent().WithAll<SpawnPointTag>(true).PatchQueryForBuildingCollisionLayer().Build();

            m_handles = new BuildCollisionLayerTypeHandles(ref state);

            latiosWorld = state.GetLatiosWorldUnmanaged();
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }

        public void OnNewScene(ref SystemState state) => latiosWorld.sceneBlackboardEntity.AddOrSetCollectionComponentAndDisposeOld(new SpawnPointCollisionLayer());

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            m_handles.Update(ref state);

            CollisionLayerSettings settings;
            if (latiosWorld.sceneBlackboardEntity.HasComponent<ArenaCollisionSettings>())
                settings = latiosWorld.sceneBlackboardEntity.GetComponentData<ArenaCollisionSettings>().settings;
            else
                settings = BuildCollisionLayerConfig.defaultSettings;

            state.Dependency = Physics.BuildCollisionLayer(m_query, m_handles).WithSettings(settings).ScheduleParallel(out CollisionLayer layer,
                                                                                                                       Allocator.Persistent,
                                                                                                                       state.Dependency);
            var spawnPointLayer = new SpawnPointCollisionLayer { layer = layer };
            latiosWorld.sceneBlackboardEntity.SetCollectionComponentAndDisposeOld(spawnPointLayer);
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

