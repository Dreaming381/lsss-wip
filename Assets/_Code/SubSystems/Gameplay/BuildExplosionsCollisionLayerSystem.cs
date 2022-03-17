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
    [AlwaysUpdateSystem]
    public partial class BuildExplosionsCollisionLayerSystem : SubSystem
    {
        private EntityQuery m_query;

        public override void OnNewScene() => sceneBlackboardEntity.AddCollectionComponent(new ExplosionCollisionLayer(), false);

        protected override void OnUpdate()
        {
            var bodies = new NativeArray<ColliderBody>(m_query.CalculateEntityCount(), Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            Entities.WithAll<ExplosionTag>().WithStoreEntityQueryInField(ref m_query).ForEach((Entity entity, int entityInQueryIndex, in Translation translation, in Scale scale) =>
            {
                var sphere                 = new SphereCollider(0f, scale.Value / 2f);  //convert diameter to radius
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
            sceneBlackboardEntity.SetCollectionComponentAndDisposeOld(explosionLayer);
        }
    }
}

