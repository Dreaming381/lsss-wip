using Latios;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace Lsss
{
    [BurstCompile]
    public partial struct UpdateTimeToLiveSystem : ISystem, ILatiosApi
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

            new Job { dcb = dcb, dt = api.deltaTime }.ScheduleParallel();
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

