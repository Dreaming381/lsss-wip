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
    public partial class BuildWallsCollisionLayerSystem : SubSystem
    {
        EntityQuery m_query;

        protected override void OnCreate()
        {
            m_query = Fluent.WithAll<WallTag>(true).WithAll<LocalToWorld>(true).WithChangeFilter<LocalToWorld>().PatchQueryForBuildingCollisionLayer().Build();
        }

        public override void OnNewScene() => sceneBlackboardEntity.AddCollectionComponent(new WallCollisionLayer(), false);

        public override bool ShouldUpdateSystem()
        {
            //Todo: Use different dirtying mechanism instead of change filter.
            //Change filter forces a sync point on transform system which is surprisingly expensive.
            return true;
            //if (!sceneBlackboardEntity.HasCollectionComponent<WallCollisionLayer>())
            //    return true;
            //return m_query.CalculateChunkCount() > 0;
        }

        protected override void OnUpdate()
        {
            CollisionLayerSettings settings;
            if (sceneBlackboardEntity.HasComponent<ArenaCollisionSettings>())
                settings = sceneBlackboardEntity.GetComponentData<ArenaCollisionSettings>().settings;
            else
                settings = BuildCollisionLayerConfig.defaultSettings;
            m_query.ResetFilter();
            Dependency = Physics.BuildCollisionLayer(m_query, this).WithSettings(settings).ScheduleParallel(out CollisionLayer layer, Allocator.Persistent, Dependency);
            var wcl    = new WallCollisionLayer { layer = layer };
            sceneBlackboardEntity.SetCollectionComponentAndDisposeOld(wcl);
            m_query.AddChangedVersionFilter(typeof(LocalToWorld));
        }
    }
}

