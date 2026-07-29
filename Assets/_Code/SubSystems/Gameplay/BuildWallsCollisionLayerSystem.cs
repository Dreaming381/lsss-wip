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
    public partial struct BuildWallsCollisionLayerSystem : ISystem, ILatiosApi, ISystemNewScene, ISystemShouldUpdate
    {
        EntityQuery m_query;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            this.OnCreateForLatios(ref state);
            m_query = state.Fluent().With<WallTag>(true).PatchQueryForBuildingCollisionLayer().Build();
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }

        public void OnNewScene(ref SystemState state) => this.GetApi(ref state).sceneBlackboardEntity.AddOrSetCollectionComponentAndDisposeOld(new WallCollisionLayer());

        public bool ShouldUpdateSystem(ref SystemState state)
        {
            //Todo: Use different dirtying mechanism instead of change filter.
            //Change filter forces a sync point on transform system which is surprisingly expensive.
            return true;
            //if (!sceneBlackboardEntity.HasCollectionComponent<WallCollisionLayer>())
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
            state.Dependency = Physics.BuildCollisionLayer(m_query, in typeHandles).WithSettings(settings).ScheduleParallel(out CollisionLayer layer,
                                                                                                                            Allocator.Persistent,
                                                                                                                            state.Dependency);
            var wcl = new WallCollisionLayer { layer = layer };
            api.sceneBlackboardEntity.SetCollectionComponentAndDisposeOld(wcl);
        }
    }
}

