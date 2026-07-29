using Latios;
using Latios.Transforms;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace Lsss
{
    [BurstCompile]
    public partial struct DestroyShipsWithNoHealthSystem : ISystem, ILatiosApi
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
            var icb = api.syncPoint.CreateInstantiateCommandBuffer<WorldTransformCommand>().AsParallelWriter();
            var dcb = api.syncPoint.CreateDestroyCommandBuffer().AsParallelWriter();

            new Job { dcb = dcb, icb = icb }.ScheduleParallel();
        }

        [BurstCompile]
        [WithChangeFilter(typeof(ShipHealth))]
        partial struct Job : IJobEntity
        {
            public InstantiateCommandBufferCommand1<WorldTransformCommand>.ParallelWriter icb;
            public DestroyCommandBuffer.ParallelWriter                                    dcb;

            public void Execute(Entity entity,
                                [ChunkIndexInQuery] int chunkIndexInQuery,
                                in ShipHealth health,
                                in ShipExplosionPrefab explosionPrefab,
                                in WorldTransform worldTransform)
            {
                if (health.health <= 0f)
                {
                    dcb.Add(entity, chunkIndexInQuery);
                    if (explosionPrefab.explosionPrefab != Entity.Null)
                        icb.Add(explosionPrefab.explosionPrefab, new WorldTransformCommand(worldTransform.worldTransform), chunkIndexInQuery);
                }
            }
        }
    }
}

