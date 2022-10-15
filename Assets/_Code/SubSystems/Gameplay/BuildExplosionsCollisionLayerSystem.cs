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
    public partial class BuildExplosionsCollisionLayerSystem : SubSystem
    {
        private EntityQuery m_query;

        public override void OnNewScene() => sceneBlackboardEntity.AddOrSetCollectionComponentAndDisposeOld(new ExplosionCollisionLayer());

        protected override void OnUpdate()
        {
            CollisionLayerSettings settings;
            if (sceneBlackboardEntity.HasComponent<ArenaCollisionSettings>())
                settings = sceneBlackboardEntity.GetComponentData<ArenaCollisionSettings>().settings;
            else
                settings = BuildCollisionLayerConfig.defaultSettings;

            var bodies =
                CollectionHelper.CreateNativeArray<ColliderBody>(m_query.CalculateEntityCount(), World.UpdateAllocator.ToAllocator, NativeArrayOptions.UninitializedMemory);
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

            Dependency         = Physics.BuildCollisionLayer(bodies).WithSettings(settings).ScheduleParallel(out CollisionLayer layer, Allocator.Persistent, Dependency);
            var explosionLayer = new ExplosionCollisionLayer { layer = layer };
            sceneBlackboardEntity.SetCollectionComponentAndDisposeOld(explosionLayer);
        }
    }
}

