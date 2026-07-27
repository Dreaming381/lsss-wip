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
    public partial struct BuildExplosionsCollisionLayerSystem : ISystem, ILatiosApi, ISystemNewScene
    {
        EntityQuery m_query;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            this.OnCreateForLatios(ref state);

            m_query = state.Fluent().With<ExplosionTag>(true).PatchQueryForBuildingCollisionLayer().Build();
        }

        public void OnNewScene(ref SystemState state) => this.GetApi(ref state).sceneBlackboardEntity.AddOrSetCollectionComponentAndDisposeOld(new ExplosionCollisionLayer());

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var                    api = this.GetApi(ref state);
            CollisionLayerSettings settings;
            if (api.sceneBlackboardEntity.HasComponent<ArenaCollisionSettings>())
                settings = api.sceneBlackboardEntity.GetComponentData<ArenaCollisionSettings>().settings;
            else
                settings = BuildCollisionLayerConfig.defaultSettings;

            var handles      = api.Get<BuildCollisionLayerTypeHandles>();
            state.Dependency = Physics.BuildCollisionLayer(m_query, handles).WithSettings(settings).ScheduleParallel(out CollisionLayer layer,
                                                                                                                     Allocator.Persistent,
                                                                                                                     state.Dependency);
            var explosionLayer = new ExplosionCollisionLayer { layer = layer };
            api.sceneBlackboardEntity.SetCollectionComponentAndDisposeOld(explosionLayer);
        }
    }
}

