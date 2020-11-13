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
    public class BuildWormholesCollisionLayerSystem : SubSystem
    {
        EntityQuery m_query;

        protected override void OnCreate()
        {
            m_query = Fluent.WithAll<WormholeTag>(true).WithAll<LocalToWorld>(true).WithChangeFilter<LocalToWorld>().PatchQueryForBuildingCollisionLayer().Build();
        }

        public override bool ShouldUpdateSystem()
        {
            //Todo: Use different dirtying mechanism instead of change filter.
            //Change filter forces a sync point on transform system which is surprisingly expensive.
            return true;
            //if (!sceneGlobalEntity.HasCollectionComponent<WormholeCollisionLayer>())
            //    return true;
            //return m_query.CalculateChunkCount() > 0;
        }

        protected override void OnUpdate()
        {
            m_query.ResetFilter();
            Dependency = Physics.BuildCollisionLayer(m_query, this).ScheduleParallel(out CollisionLayer layer, Allocator.Persistent, Dependency);
            var wcl    = new WormholeCollisionLayer { layer = layer };
            if (sceneGlobalEntity.HasCollectionComponent<WormholeCollisionLayer>())
                sceneGlobalEntity.SetCollectionComponentAndDisposeOld(wcl);
            else
            {
                CompleteDependency();
                sceneGlobalEntity.AddCollectionComponent(wcl);
            }
            m_query.AddChangedVersionFilter(typeof(LocalToWorld));
        }
    }

    public class DebugDrawWormholeCollisionLayersSystem : SubSystem
    {
        protected override void OnUpdate()
        {
            var layer = sceneGlobalEntity.GetCollectionComponent<WormholeCollisionLayer>(true).layer;
            CompleteDependency();
            PhysicsDebug.DrawLayer(layer).Run();
            UnityEngine.Debug.Log("Wormholes in layer: " + layer.Count);
        }
    }
}

