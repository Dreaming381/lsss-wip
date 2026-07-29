using Latios;
using Latios.Psyshock;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace Lsss
{
    [BurstCompile]
    public partial struct BulletVsWallSystem : ISystem, ILatiosApi
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            this.OnCreateForLatios(ref state);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var api = this.GetApi(ref state);
            var dcb = api.syncPoint.CreateDestroyCommandBuffer().AsParallelWriter();

            var bulletLayer = api.sceneBlackboardEntity.GetCollectionComponent<BulletCollisionLayer>(true).layer;
            var wallLayer   = api.sceneBlackboardEntity.GetCollectionComponent<WallCollisionLayer>(true).layer;

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

