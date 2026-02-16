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
    public partial struct SpawnPointAnimationSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            //new Job().ScheduleParallel();
            var job = new Job
            {
                transformLookup = new TransformAspectLookup(SystemAPI.GetComponentLookup<WorldTransform>(false),
                                                            SystemAPI.GetComponentLookup<RootReference>(true),
                                                            SystemAPI.GetBufferLookup<EntityInHierarchy>(true),
                                                            SystemAPI.GetBufferLookup<EntityInHierarchyCleanup>(true),
                                                            SystemAPI.GetEntityStorageInfoLookup())
            };
            job.Schedule();
            job.ScheduleByRef();
        }

        [WithAll(typeof(WorldTransform))]
        [BurstCompile]
        partial struct Job : IJobEntity
        {
            public TransformAspectLookup transformLookup;

            public void Execute(Entity entity, in TimeToLive timeToLive, in SpawnPointAnimationData data)
            {
                float growFactor   = math.unlerp(data.growStartTime, data.growEndTime, timeToLive.timeToLive);
                growFactor         = math.select(growFactor, 1f, data.growStartTime == data.growEndTime);
                float shrinkFactor = math.unlerp(0f, data.shrinkStartTime, timeToLive.timeToLive);
                float factor       = math.saturate(math.min(growFactor, shrinkFactor));
                bool  isGrowing    = growFactor < shrinkFactor;

                float growRadians   = math.lerp(-data.growSpins, 0f, factor);
                float shrinkRadians = math.lerp(data.shrinkSpins, 0f, factor);
                float rads          = math.select(shrinkRadians, growRadians, isGrowing);

                var transform           = transformLookup[entity];
                transform.localRotation = quaternion.Euler(0f, 0f, rads);
                transform.localScale    = math.max(factor, 0.001f);
            }
        }
    }

    [RequireMatchingQueriesForUpdate]
    [BurstCompile]
    public partial struct SpawnPointAnimationSystem2 : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var job = new Job
            {
                transformHandle = new TransformAspectParallelChunkHandle(SystemAPI.GetComponentLookup<WorldTransform>(false),
                                                                         SystemAPI.GetComponentTypeHandle<RootReference>(true),
                                                                         SystemAPI.GetBufferLookup<EntityInHierarchy>(true),
                                                                         SystemAPI.GetBufferLookup<EntityInHierarchyCleanup>(true),
                                                                         SystemAPI.GetEntityStorageInfoLookup(),
                                                                         ref state)
            };
            job.ScheduleByRef();
            state.Dependency = job.transformHandle.ScheduleChunkGrouping(state.Dependency);
            state.Dependency = job.GetTransformsScheduler().ScheduleParallel(state.Dependency);
        }

        [WithAll(typeof(WorldTransform))]
        [BurstCompile]
        partial struct Job : IJobEntity, IJobEntityChunkBeginEnd, IJobChunkParallelTransform
        {
            public TransformAspectParallelChunkHandle transformHandle;

            public ref TransformAspectParallelChunkHandle transformAspectHandleAccess => ref transformHandle.RefAccess();

            public void Execute([EntityIndexInChunk] int indexInChunk, in TimeToLive timeToLive, in SpawnPointAnimationData data)
            {
                float growFactor   = math.unlerp(data.growStartTime, data.growEndTime, timeToLive.timeToLive);
                growFactor         = math.select(growFactor, 1f, data.growStartTime == data.growEndTime);
                float shrinkFactor = math.unlerp(0f, data.shrinkStartTime, timeToLive.timeToLive);
                float factor       = math.saturate(math.min(growFactor, shrinkFactor));
                bool  isGrowing    = growFactor < shrinkFactor;

                float growRadians   = math.lerp(-data.growSpins, 0f, factor);
                float shrinkRadians = math.lerp(data.shrinkSpins, 0f, factor);
                float rads          = math.select(shrinkRadians, growRadians, isGrowing);

                var transform           = transformHandle[indexInChunk];
                transform.localRotation = quaternion.Euler(0f, 0f, rads);
                transform.localScale    = math.max(factor, 0.001f);
            }

            public bool OnChunkBegin(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                return transformHandle.OnChunkBegin(in chunk, unfilteredChunkIndex, useEnabledMask, chunkEnabledMask);
            }

            public void OnChunkEnd(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask, bool chunkWasExecuted)
            {
            }
        }
    }
}

