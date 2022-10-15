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
    public partial class BuildWormholesCollisionLayerSystem : SubSystem
    {
        EntityQuery m_query;

        protected override void OnCreate()
        {
            m_query = Fluent.WithAll<WormholeTag>(true).WithAll<LocalToWorld>(true).WithChangeFilter<LocalToWorld>().PatchQueryForBuildingCollisionLayer().Build();
        }

        public override void OnNewScene() => sceneBlackboardEntity.AddOrSetCollectionComponentAndDisposeOld(new WormholeCollisionLayer());

        public override bool ShouldUpdateSystem()
        {
            //Todo: Use different dirtying mechanism instead of change filter.
            //Change filter forces a sync point on transform system which is surprisingly expensive.
            return true;
            //if (!sceneBlackboardEntity.HasCollectionComponent<WormholeCollisionLayer>())
            //    return true;
            //return m_query.CalculateChunkCount() > 0;
        }

        protected override void OnUpdate()
        {
            var settings = sceneBlackboardEntity.GetComponentData<ArenaCollisionSettings>().settings;
            m_query.ResetFilter();
            Dependency = Physics.BuildCollisionLayer(m_query, this).WithSettings(settings).ScheduleParallel(out CollisionLayer layer, Allocator.Persistent, Dependency);
            var wcl    = new WormholeCollisionLayer { layer = layer };
            sceneBlackboardEntity.SetCollectionComponentAndDisposeOld(wcl);
            m_query.AddChangedVersionFilter(typeof(LocalToWorld));
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

