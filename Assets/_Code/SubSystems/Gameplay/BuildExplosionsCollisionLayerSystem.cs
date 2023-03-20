using Latios;
using Latios.Psyshock;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

using static Unity.Entities.SystemAPI;

namespace Lsss
{
    [BurstCompile]
    public partial struct BuildExplosionsCollisionLayerSystem : ISystem, ISystemNewScene
    {
        LatiosWorldUnmanaged latiosWorld;

        EntityQuery                    m_query;
        BuildCollisionLayerTypeHandles m_handles;

        public void OnCreate(ref SystemState state)
        {
            latiosWorld = state.GetLatiosWorldUnmanaged();

            m_query   = state.Fluent().WithAll<ExplosionTag>(true).PatchQueryForBuildingCollisionLayer().Build();
            m_handles = new BuildCollisionLayerTypeHandles(ref state);
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }

        public void OnNewScene(ref SystemState state) => latiosWorld.sceneBlackboardEntity.AddOrSetCollectionComponentAndDisposeOld(new ExplosionCollisionLayer());

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            CollisionLayerSettings settings;
            if (latiosWorld.sceneBlackboardEntity.HasComponent<ArenaCollisionSettings>())
                settings = latiosWorld.sceneBlackboardEntity.GetComponentData<ArenaCollisionSettings>().settings;
            else
                settings = BuildCollisionLayerConfig.defaultSettings;

            m_handles.Update(ref state);
            state.Dependency = Physics.BuildCollisionLayer(m_query, m_handles).WithSettings(settings).ScheduleParallel(out CollisionLayer layer,
                                                                                                                       Allocator.Persistent,
                                                                                                                       state.Dependency);
            var explosionLayer = new ExplosionCollisionLayer { layer = layer };
            latiosWorld.sceneBlackboardEntity.SetCollectionComponentAndDisposeOld(explosionLayer);
        }
    }
}

