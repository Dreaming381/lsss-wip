using Latios;
using Latios.Transforms;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

using static Unity.Entities.SystemAPI;

namespace Lsss
{
    [BurstCompile]
    public partial struct DestroyShipsWithNoHealthSystem : ISystem
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
            var icb = latiosWorld.syncPoint.CreateInstantiateCommandBuffer<WorldTransform>().AsParallelWriter();
            var dcb = latiosWorld.syncPoint.CreateDestroyCommandBuffer().AsParallelWriter();

            new Job { dcb = dcb, icb = icb }.ScheduleParallel();
        }

        [BurstCompile]
        [WithChangeFilter(typeof(ShipHealth))]
        partial struct Job : IJobEntity
        {
            public InstantiateCommandBuffer<WorldTransform>.ParallelWriter icb;
            public DestroyCommandBuffer.ParallelWriter                     dcb;

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
                        icb.Add(explosionPrefab.explosionPrefab, worldTransform, chunkIndexInQuery);
                }
            }
        }
    }
}

