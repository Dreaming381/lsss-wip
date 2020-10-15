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
        protected override void OnUpdate()
        {
            var dcb = latiosWorld.SyncPoint.CreateDestroyCommandBuffer().AsParallelWriter();

            var bulletLayer = sceneGlobalEntity.GetCollectionComponent<BulletCollisionLayer>(true).layer;
            var wallLayer   = sceneGlobalEntity.GetCollectionComponent<WallCollisionLayer>(true).layer;

            var processor = new DestroyBulletsThatHitWallsProcessor { dcb = dcb };

            Dependency = Physics.FindPairs(bulletLayer, wallLayer, processor).ScheduleParallel(Dependency);
        }

        struct DestroyBulletsThatHitWallsProcessor : IFindPairsProcessor
        {
            public DestroyCommandBuffer.ParallelWriter dcb;

            public void Execute(FindPairsResult result)
            {
                if (Physics.DistanceBetween(result.bodyA.collider, result.bodyA.transform, result.bodyB.collider, result.bodyB.transform, 0f, out _))
                {
                    dcb.Add(result.entityA, result.jobIndex);
                }
            }
        }
    }
}

