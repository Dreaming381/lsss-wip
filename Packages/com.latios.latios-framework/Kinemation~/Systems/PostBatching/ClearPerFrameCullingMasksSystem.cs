using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;

namespace Latios.Kinemation.Systems
{
    [DisableAutoCreation]
    [BurstCompile]
    public partial struct ClearPerFrameCullingMasksSystem : ISystem
    {
        EntityQuery m_metaQuery;

        public void OnCreate(ref SystemState state)
        {
            m_metaQuery = state.Fluent().WithAll<ChunkPerFrameCullingMask>(false).WithAll<ChunkHeader>(true).Build();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.Dependency = new ClearJob
            {
                handle            = state.GetComponentTypeHandle<ChunkPerFrameCullingMask>(false),
                lastSystemVersion = state.LastSystemVersion
            }.ScheduleParallel(m_metaQuery, state.Dependency);
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state) {
        }

        [BurstCompile]
        struct ClearJob : IJobEntityBatch
        {
            public ComponentTypeHandle<ChunkPerFrameCullingMask> handle;
            public uint                                          lastSystemVersion;

            public unsafe void Execute(ArchetypeChunk batchInChunk, int batchIndex)
            {
                if (batchInChunk.DidChange(handle, lastSystemVersion))
                {
                    var ptr = batchInChunk.GetComponentDataPtrRW(ref handle);
                    UnsafeUtility.MemClear(ptr, sizeof(ChunkPerFrameCullingMask) * batchInChunk.Count);
                }
            }
        }
    }
}

