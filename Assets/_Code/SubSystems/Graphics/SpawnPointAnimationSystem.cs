using Latios;
using Latios.Transforms;
using Unity.Burst;
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
            new Job
            {
                transformLookup = new TransformAspectLookup(SystemAPI.GetComponentLookup<WorldTransform>(false),
                                                            SystemAPI.GetComponentLookup<RootReference>(true),
                                                            SystemAPI.GetBufferLookup<EntityInHierarchy>(true),
                                                            SystemAPI.GetBufferLookup<EntityInHierarchyCleanup>(true),
                                                            SystemAPI.GetEntityStorageInfoLookup())
            }.Schedule();
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
}

