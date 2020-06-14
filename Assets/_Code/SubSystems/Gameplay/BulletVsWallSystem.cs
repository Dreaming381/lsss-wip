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
    public class BulletVsWallSystem : SubSystem
    {
        BeginInitializationEntityCommandBufferSystem m_ecbSystem;

        protected override void OnCreate()
        {
            m_ecbSystem = World.GetExistingSystem<BeginInitializationEntityCommandBufferSystem>();
        }

        protected override void OnUpdate()
        {
            var ecbPackage = m_ecbSystem.CreateCommandBuffer();
            var ecb        = ecbPackage.ToConcurrent();

            var bulletLayer = sceneGlobalEntity.GetCollectionComponent<BulletCollisionLayer>(true).layer;
            var wallLayer   = sceneGlobalEntity.GetCollectionComponent<WallCollisionLayer>(true).layer;

            var processor = new DestroyBulletsThatHitWallsProcessor { ecb = ecb };

            Dependency = Physics.FindPairs(bulletLayer, wallLayer, processor).ScheduleParallel(Dependency);

            m_ecbSystem.AddJobHandleForProducer(Dependency);
        }

        struct DestroyBulletsThatHitWallsProcessor : IFindPairsProcessor
        {
            public EntityCommandBuffer.Concurrent ecb;

            public void Execute(FindPairsResult result)
            {
                if (Physics.DistanceBetween(result.bodyA.collider, result.bodyA.transform, result.bodyB.collider, result.bodyB.transform, 0f, out _))
                {
                    ecb.DestroyEntity(result.jobIndex, result.entityA);
                }
            }
        }
    }
}

