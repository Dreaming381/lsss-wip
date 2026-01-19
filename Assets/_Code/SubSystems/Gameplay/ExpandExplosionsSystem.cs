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
    public partial struct ExpandExplosionsSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;
            new Job
            {
                transformHandle = new TransformAspectRootHandle(SystemAPI.GetComponentLookup<WorldTransform>(false),
                                                                SystemAPI.GetBufferTypeHandle<EntityInHierarchy>(true),
                                                                SystemAPI.GetEntityStorageInfoLookup()),
                dt = dt
            }.ScheduleParallel();
        }

        [BurstCompile]
        [WithAll(typeof(ExplosionTag), typeof(WorldTransform))]
        partial struct Job : IJobEntity, IJobEntityChunkBeginEnd
        {
            public TransformAspectRootHandle transformHandle;
            public float                     dt;

            public void Execute([EntityIndexInChunk] int indexInChunk, in ExplosionStats stats)
            {
                var transform        = transformHandle[indexInChunk];
                var scale            = transform.localScale + stats.expansionRate * dt;
                scale                = math.min(scale, stats.radius);
                transform.localScale = scale;
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

