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
    public partial class BuildBulletsCollisionLayerSystem : SubSystem
    {
        private EntityQuery m_query;

        public override void OnNewScene() => sceneBlackboardEntity.AddCollectionComponent(new BulletCollisionLayer(), false);

        protected override void OnUpdate()
        {
            CollisionLayerSettings settings;
            if (sceneBlackboardEntity.HasComponent<ArenaCollisionSettings>())
                settings = sceneBlackboardEntity.GetComponentData<ArenaCollisionSettings>().settings;
            else
                settings = BuildCollisionLayerConfig.defaultSettings;

            var bodies =
                CollectionHelper.CreateNativeArray<ColliderBody>(m_query.CalculateEntityCount(), World.UpdateAllocator.ToAllocator, NativeArrayOptions.UninitializedMemory);

            Entities.WithAll<BulletTag>().ForEach((Entity entity,
                                                   int entityInQueryIndex,
                                                   in Translation translation,
                                                   in Rotation rotation,
                                                   in Collider collider,
                                                   in BulletPreviousPosition previousPosition) =>
            {
                CapsuleCollider capsule     = collider;
                float           tailLength  = math.distance(translation.Value, previousPosition.previousPosition);
                capsule.pointA              = capsule.pointB;
                capsule.pointA.z           -= math.max(tailLength, 1.1920928955078125e-7f);  //Todo: Use math version of this once released.

                bodies[entityInQueryIndex] = new ColliderBody
                {
                    collider  = capsule,
                    entity    = entity,
                    transform = new RigidTransform(rotation.Value, translation.Value)
                };
            }).WithStoreEntityQueryInField(ref m_query).ScheduleParallel();

            Dependency = Physics.BuildCollisionLayer(bodies).WithSettings(settings).ScheduleParallel(out CollisionLayer layer, Allocator.Persistent, Dependency);
            var bcl    = new BulletCollisionLayer { layer = layer };
            sceneBlackboardEntity.SetCollectionComponentAndDisposeOld(bcl);
        }
    }

    public partial class DebugDrawBulletCollisionLayersSystem : SubSystem
    {
        protected override void OnUpdate()
        {
            var layer = sceneBlackboardEntity.GetCollectionComponent<BulletCollisionLayer>(true).layer;
            CompleteDependency();
            PhysicsDebug.DrawLayer(layer).Run();
            //UnityEngine.Debug.Log("Bullets in layer: " + layer.Count);
        }
    }
}

