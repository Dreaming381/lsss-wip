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
    public partial struct BuildWormholesCollisionLayerSystem : ISystem, ISystemNewScene, ISystemShouldUpdate
    {
        EntityQuery m_query;

        BuildCollisionLayerTypeHandles m_handles;

        LatiosWorldUnmanaged latiosWorld;

        public void OnCreate(ref SystemState state)
        {
            m_query = state.Fluent().WithAll<WormholeTag>(true).PatchQueryForBuildingCollisionLayer().Build();

            m_handles = new BuildCollisionLayerTypeHandles(ref state);

            latiosWorld = state.GetLatiosWorldUnmanaged();
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }

        public void OnNewScene(ref SystemState state) => latiosWorld.sceneBlackboardEntity.AddOrSetCollectionComponentAndDisposeOld(new WormholeCollisionLayer());

        public bool ShouldUpdateSystem(ref SystemState state)
        {
            //Todo: Use different dirtying mechanism instead of change filter.
            //Change filter forces a sync point on transform system which is surprisingly expensive.
            return true;
            //if (!sceneBlackboardEntity.HasCollectionComponent<WormholeCollisionLayer>())
            //    return true;
            //return m_query.CalculateChunkCount() > 0;
        }

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
            var wcl = new WormholeCollisionLayer { layer = layer };
            latiosWorld.sceneBlackboardEntity.SetCollectionComponentAndDisposeOld(wcl);
        }
    }

    public partial class DebugDrawWormholeCollisionLayersSystem : SubSystem
    {
        protected override void OnUpdate()
        {
            var layer = sceneBlackboardEntity.GetCollectionComponent<WormholeCollisionLayer>(true).layer;
            CompleteDependency();
            PhysicsDebug.DrawLayer(layer).Run();
            UnityEngine.Debug.Log("Wormholes in layer: " + layer.Count);
        }
    }
}

