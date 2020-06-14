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
    public class BuildExplosionsCollisionLayerSystem : SubSystem
    {
        private EntityQuery m_query;

        protected override void OnUpdate()
        {
            var bodies = new NativeArray<ColliderBody>(m_query.CalculateEntityCount(), Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            Entities.WithAll<ExplosionTag>().WithStoreEntityQueryInField(ref m_query).ForEach((Entity entity, int entityInQueryIndex, in Translation translation, in Scale scale) =>
            {
                var sphere                 = new SphereCollider(0f, scale.Value);
                bodies[entityInQueryIndex] = new ColliderBody
                {
                    entity    = entity,
                    transform = new RigidTransform(quaternion.identity, translation.Value),
                    collider  = sphere
                };
            }).ScheduleParallel();

            Dependency         = Physics.BuildCollisionLayer(bodies).ScheduleParallel(out CollisionLayer layer, Allocator.Persistent, Dependency);
            Dependency         = bodies.Dispose(Dependency);
            var explosionLayer = new ExplosionCollisionLayer { layer = layer };
            if (sceneGlobalEntity.HasCollectionComponent<ExplosionCollisionLayer>())
            {
                sceneGlobalEntity.SetCollectionComponentAndDisposeOld(explosionLayer);
            }
            else
            {
                //Some bizarre bug exists that requires dependencies to be completed before calling EntityManager.AddComponent
                //At least this only happens on the first frame of the scene.
                CompleteDependency();

                sceneGlobalEntity.AddCollectionComponent(explosionLayer);
            }
        }
    }
}

