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
    public partial struct MoveOrbitalSpawnPointsSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float dt = Time.DeltaTime;
            new Job
            {
                transformHandle = new TransformAspectRootHandle(SystemAPI.GetComponentLookup<WorldTransform>(false),
                                                                SystemAPI.GetBufferTypeHandle<EntityInHierarchy>(true),
                                                                SystemAPI.GetEntityStorageInfoLookup()),
                dt = dt
            }.ScheduleParallel();
        }

        [BurstCompile]
        [WithAll(typeof(SpawnPointTag), typeof(WorldTransform))]
        partial struct Job : IJobEntity, IJobEntityChunkBeginEnd
        {
            public TransformAspectRootHandle transformHandle;
            public float                     dt;

            public void Execute([EntityIndexInChunk] int indexInChunk, in SpawnPointOrbitalPath path, in SpawnTimes pauseTime)
            {
                var    transform            = transformHandle[indexInChunk];
                var    rotation             = quaternion.AxisAngle(path.orbitPlaneNormal, path.orbitSpeed * dt);
                float3 currentOutwardVector = transform.worldPosition - path.center;
                float3 newOutwardVector     = math.rotate(rotation, currentOutwardVector);
                newOutwardVector            = math.normalizesafe(newOutwardVector) * path.radius;
                transform.worldPosition     = math.select(transform.worldPosition, path.center + newOutwardVector, pauseTime.pauseTime <= 0f);
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

