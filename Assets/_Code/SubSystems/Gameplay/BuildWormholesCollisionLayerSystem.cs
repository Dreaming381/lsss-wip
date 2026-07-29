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
    public partial struct BuildWormholesCollisionLayerSystem : ISystem, ILatiosApi, ISystemNewScene, ISystemShouldUpdate
    {
        EntityQuery m_query;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            this.OnCreateForLatios(ref state);
            m_query = state.Fluent().With<WormholeTag>(true).PatchQueryForBuildingCollisionLayer().Build();
        }

        public void OnNewScene(ref SystemState state) => this.GetApi(ref state).sceneBlackboardEntity.AddOrSetCollectionComponentAndDisposeOld(new WormholeCollisionLayer());

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
            var api = this.GetApi(ref state);

            CollisionLayerSettings settings;
            if (api.sceneBlackboardEntity.HasComponent<ArenaCollisionSettings>())
                settings = api.sceneBlackboardEntity.GetComponentData<ArenaCollisionSettings>().settings;
            else
                settings = BuildCollisionLayerConfig.defaultSettings;

            var typeHandles  = api.Get<BuildCollisionLayerTypeHandles>();
            state.Dependency = Physics.BuildCollisionLayer(m_query, typeHandles).WithSettings(settings).ScheduleParallel(out CollisionLayer layer,
                                                                                                                         Allocator.Persistent,
                                                                                                                         state.Dependency);
            var wcl = new WormholeCollisionLayer { layer = layer };
            api.sceneBlackboardEntity.SetCollectionComponentAndDisposeOld(wcl);
        }
    }

    public partial class DebugDrawWormholeCollisionLayersSystem : SubSystem
    {
        protected override void OnUpdate()
        {
            var layer = sceneBlackboardEntity.GetCollectionComponent<WormholeCollisionLayer>(true).layer;
            CompleteDependency();
            PhysicsDebug.DrawLayer(layer).Run();
            UnityEngine.Debug.Log("Wormholes in layer: " + layer.count);
        }
    }
}

