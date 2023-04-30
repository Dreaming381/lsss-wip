using Latios;
using Latios.Psyshock;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

using static Unity.Entities.SystemAPI;

namespace Lsss
{
    [BurstCompile]
    public partial struct BulletVsWallSystem : ISystem
    {
        LatiosWorldUnmanaged latiosWorld;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            latiosWorld = state.GetLatiosWorldUnmanaged();
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var dcb = latiosWorld.syncPoint.CreateDestroyCommandBuffer().AsParallelWriter();

            var bulletLayer = latiosWorld.sceneBlackboardEntity.GetCollectionComponent<BulletCollisionLayer>(true).layer;
            var wallLayer   = latiosWorld.sceneBlackboardEntity.GetCollectionComponent<WallCollisionLayer>(true).layer;

            var processor = new DestroyBulletsThatHitWallsProcessor { dcb = dcb };

            state.Dependency = Physics.FindPairs(bulletLayer, wallLayer, processor).ScheduleParallel(state.Dependency);
        }

        struct DestroyBulletsThatHitWallsProcessor : IFindPairsProcessor
        {
            public DestroyCommandBuffer.ParallelWriter dcb;

            public void Execute(in FindPairsResult result)
            {
                if (Physics.DistanceBetween(result.bodyA.collider, result.bodyA.transform, result.bodyB.collider, result.bodyB.transform, 0f, out _))
                {
                    dcb.Add(result.entityA, result.jobIndex);
                }
            }
        }
    }
}

