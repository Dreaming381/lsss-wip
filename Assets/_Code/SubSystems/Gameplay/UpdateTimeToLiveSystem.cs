using Latios;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

using static Unity.Entities.SystemAPI;

namespace Lsss
{
    [BurstCompile]
    public partial struct UpdateTimeToLiveSystem : ISystem
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
            var   dcb = latiosWorld.syncPoint.CreateDestroyCommandBuffer().AsParallelWriter();
            float dt  = Time.DeltaTime;

            new Job { dcb = dcb, dt = dt }.ScheduleParallel();
        }

        [BurstCompile]
        partial struct Job : IJobEntity
        {
            public DestroyCommandBuffer.ParallelWriter dcb;
            public float                               dt;

            public void Execute(Entity entity, [ChunkIndexInQuery] int chunkIndexInQuery, ref TimeToLive timeToLive)
            {
                timeToLive.timeToLive -= dt;
                if (timeToLive.timeToLive < 0f)
                    dcb.Add(entity, chunkIndexInQuery);
            }
        }
    }
}

