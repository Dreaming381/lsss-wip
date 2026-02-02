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
    public partial struct MoveBulletsSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float dt = Time.DeltaTime;
            new Job
            {
                transformHandle = new TransformAspectRootHandle(SystemAPI.GetComponentLookup<WorldTransform>(false),
                                                                SystemAPI.GetBufferTypeHandle<EntityInHierarchy>(true),
                                                                SystemAPI.GetBufferTypeHandle<EntityInHierarchyCleanup>(true),
                                                                SystemAPI.GetEntityStorageInfoLookup()),
                dt = dt
            }.ScheduleParallel();
        }

        [BurstCompile]
        [WithAll(typeof(BulletTag), typeof(WorldTransform))]
        partial struct Job : IJobEntity, IJobEntityChunkBeginEnd
        {
            public TransformAspectRootHandle transformHandle;
            public float                     dt;

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

