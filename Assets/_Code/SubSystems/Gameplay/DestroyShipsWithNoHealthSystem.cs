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
            var api = this.OnCreateForLatios(ref state);
            api.GetDefaultQuery<Job>().AddChangedVersionFilter(ComponentType.ReadOnly<ShipHealth>());
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var api = this.GetApi(ref state);
            var icb = api.syncPoint.CreateInstantiateCommandBuffer<WorldTransformCommand>().AsParallelWriter();
            var dcb = api.syncPoint.CreateDestroyCommandBuffer().AsParallelWriter();

            new Job { dcb = dcb, icb = icb }.ScheduleParallel(api);
        }

        [BurstCompile]
        partial struct Job : IJobEach
        {
            public InstantiateCommandBufferCommand1<WorldTransformCommand>.ParallelWriter icb;
            public DestroyCommandBuffer.ParallelWriter                                    dcb;

            public void Execute(Entity entity,
                                in IJobEach.JobContext context,
                                in ShipHealth health,
                                in ShipExplosionPrefab explosionPrefab,
                                in WorldTransform worldTransform)
            {
                if (health.health <= 0f)
                {
                    dcb.Add(entity, context.chunkIndexInQuery);
                    if (explosionPrefab.explosionPrefab != Entity.Null)
                        icb.Add(explosionPrefab.explosionPrefab, new WorldTransformCommand(worldTransform.worldTransform), context.chunkIndexInQuery);
                }
            }
        }
    }
}

