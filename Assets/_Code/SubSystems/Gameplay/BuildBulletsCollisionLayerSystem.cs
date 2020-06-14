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
    public class BuildBulletsCollisionLayerSystem : SubSystem
    {
        private EntityQuery m_query;

        protected override void OnUpdate()
        {
            var bodies = new NativeArray<ColliderBody>(m_query.CalculateEntityCount(), Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

            Entities.WithAll<BulletTag>().WithStoreEntityQueryInField(ref m_query).ForEach((Entity entity,
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
            }).ScheduleParallel();

            Dependency = Physics.BuildCollisionLayer(bodies).ScheduleParallel(out CollisionLayer layer, Allocator.Persistent, Dependency);
            Dependency = bodies.Dispose(Dependency);

            var bcl = new BulletCollisionLayer { layer = layer };
            if (sceneGlobalEntity.HasCollectionComponent<BulletCollisionLayer>())
            {
                sceneGlobalEntity.SetCollectionComponentAndDisposeOld(bcl);
            }
            else
            {
                //Some bizarre bug exists that requires dependencies to be completed before calling EntityManager.AddComponent
                //At least this only happens on the first frame of the scene.
                CompleteDependency();

                sceneGlobalEntity.AddCollectionComponent(bcl);
            }
        }
    }

    public class DebugDrawBulletCollisionLayersSystem : SubSystem
    {
        protected override void OnUpdate()
        {
            var layer = sceneGlobalEntity.GetCollectionComponent<BulletCollisionLayer>(true).layer;
            CompleteDependency();
            PhysicsDebug.DrawLayer(layer);
            UnityEngine.Debug.Log("Bullets in layer: " + layer.Count);
        }
    }
}

