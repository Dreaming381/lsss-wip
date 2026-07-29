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
    public partial struct MoveBulletsSystem : ISystem, ILatiosApi
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
            new Job
            {
                dt = api.deltaTime
            }.Inject(api).ScheduleParallel();
        }

        [BurstCompile]
        [WithAll(typeof(BulletTag), typeof(WorldTransform))]
        partial struct Job : IJobEntity, IJobEntityChunkBeginEnd, IInjectable
        {
            [Inject] TransformAspectRootHandle transformHandle;
            public float                       dt;

            public void Execute([EntityIndexInChunk] int indexInChunk, in Speed speed)
            {
                var transform            = transformHandle[indexInChunk];
                transform.worldPosition += dt * speed.speed * transform.forwardDirection;
            }

            public bool OnChunkBegin(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                transformHandle.SetupChunk(in chunk);
                return true;
            }

            public void OnChunkEnd(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask, bool chunkWasExecuted)
            {
            }
        }
    }
}

