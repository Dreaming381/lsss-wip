using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

// This system doesn't actually allocate the compute buffer.
// Doing so now would introduce a sync point.
// This system just calculates the required size and distributes instance shader properties.
namespace Latios.Kinemation.Systems
{
    [DisableAutoCreation]
    [BurstCompile]
    public partial struct AllocateLinearBlendMatricesSystem : ISystem
    {
        EntityQuery m_metaQuery;

        public void OnCreate(ref SystemState state)
        {
            m_metaQuery = state.Fluent().WithAll<ChunkHeader>(true).WithAll<ChunkLinearBlendSkinningMemoryMetadata>().Build();

            state.GetWorldBlackboardEntity().AddComponent<MaxRequiredLinearBlendMatrices>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            int linearBlendIndex = state.GetWorldBlackboardEntity().GetBuffer<MaterialPropertyComponentType>(true).Reinterpret<ComponentType>().AsNativeArray()
                                   .IndexOf(ComponentType.ReadOnly<LinearBlendSkinningShaderIndex>());
            ulong linearBlendMaterialMaskLower = (ulong)linearBlendIndex >= 64UL ? 0UL : (1UL << linearBlendIndex);
            ulong linearBlendMaterialMaskUpper = (ulong)linearBlendIndex >= 64UL ? (1UL << (linearBlendIndex - 64)) : 0UL;

            var metaHandle          = state.GetComponentTypeHandle<ChunkLinearBlendSkinningMemoryMetadata>();
            var perCameraMaskHandle = state.GetComponentTypeHandle<ChunkPerCameraCullingMask>(true);
            var perFrameMaskHandle  = state.GetComponentTypeHandle<ChunkPerFrameCullingMask>(true);

            var chunkList = state.WorldUnmanaged.UpdateAllocator.AllocateNativeList<ArchetypeChunk>(m_metaQuery.CalculateEntityCountWithoutFiltering());

            state.Dependency = new ChunkPrefixSumJob
            {
                perCameraMaskHandle                = perCameraMaskHandle,
                perFrameMaskHandle                 = perFrameMaskHandle,
                chunkHeaderHandle                  = state.GetComponentTypeHandle<ChunkHeader>(true),
                metaHandle                         = metaHandle,
                materialMaskHandle                 = state.GetComponentTypeHandle<ChunkMaterialPropertyDirtyMask>(),
                maxRequiredLinearBlendMatricesCdfe = state.GetComponentDataFromEntity<MaxRequiredLinearBlendMatrices>(),
                worldBlackboardEntity              = state.GetWorldBlackboardEntity(),
                changedChunkList                   = chunkList,
                linearBlendMaterialMaskLower       = linearBlendMaterialMaskLower,
                linearBlendMaterialMaskUpper       = linearBlendMaterialMaskUpper
            }.Schedule(m_metaQuery, state.Dependency);

            state.Dependency = new AssignComputeDeformMeshOffsetsJob
            {
                perCameraMaskHandle = perCameraMaskHandle,
                perFrameMaskHandle  = perFrameMaskHandle,
                metaHandle          = metaHandle,
                changedChunks       = chunkList.AsDeferredJobArray(),
                indicesHandle       = state.GetComponentTypeHandle<LinearBlendSkinningShaderIndex>()
            }.Schedule(chunkList, 1, state.Dependency);
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state) {
        }

        // Schedule single
        [BurstCompile]
        struct ChunkPrefixSumJob : IJobEntityBatch
        {
            [ReadOnly] public ComponentTypeHandle<ChunkPerCameraCullingMask>   perCameraMaskHandle;
            [ReadOnly] public ComponentTypeHandle<ChunkPerFrameCullingMask>    perFrameMaskHandle;
            [ReadOnly] public ComponentTypeHandle<ChunkHeader>                 chunkHeaderHandle;
            public ComponentTypeHandle<ChunkLinearBlendSkinningMemoryMetadata> metaHandle;
            public ComponentTypeHandle<ChunkMaterialPropertyDirtyMask>         materialMaskHandle;
            public ComponentDataFromEntity<MaxRequiredLinearBlendMatrices>     maxRequiredLinearBlendMatricesCdfe;
            public Entity                                                      worldBlackboardEntity;
            public NativeList<ArchetypeChunk>                                  changedChunkList;
            public ulong                                                       linearBlendMaterialMaskLower;
            public ulong                                                       linearBlendMaterialMaskUpper;

            public void Execute(ArchetypeChunk batchInChunk, int batchIndex)
            {
                var prefixSum = maxRequiredLinearBlendMatricesCdfe[worldBlackboardEntity].matricesCount;

                var cameraMaskArray   = batchInChunk.GetNativeArray(perCameraMaskHandle);
                var frameMaskArray    = batchInChunk.GetNativeArray(perFrameMaskHandle);
                var headerArray       = batchInChunk.GetNativeArray(chunkHeaderHandle);
                var metaArray         = batchInChunk.GetNativeArray(metaHandle);
                var materialMaskArray = batchInChunk.GetNativeArray(materialMaskHandle);

                for (int i = 0; i < batchInChunk.Count; i++)
                {
                    var cameraMask = cameraMaskArray[i];
                    var frameMask  = frameMaskArray[i];
                    var lower      = cameraMask.lower.Value & (~frameMask.lower.Value);
                    var upper      = cameraMask.upper.Value & (~frameMask.upper.Value);
                    if ((upper | lower) == 0)
                        continue;

                    changedChunkList.Add(headerArray[i].ArchetypeChunk);
                    var materialMask          = materialMaskArray[i];
                    materialMask.lower.Value |= linearBlendMaterialMaskLower;
                    materialMask.upper.Value |= linearBlendMaterialMaskUpper;
                    materialMaskArray[i]      = materialMask;

                    var meta = metaArray[i];

                    meta.bonesStartPrefixSum  = prefixSum;
                    var newEntities           = math.countbits(lower) + math.countbits(upper);
                    prefixSum                += newEntities * meta.bonesPerMesh;
                    metaArray[i]              = meta;
                }
                maxRequiredLinearBlendMatricesCdfe[worldBlackboardEntity] = new MaxRequiredLinearBlendMatrices { matricesCount = prefixSum };
            }
        }

        [BurstCompile]
        struct AssignComputeDeformMeshOffsetsJob : IJobParallelForDefer
        {
            [ReadOnly] public ComponentTypeHandle<ChunkPerCameraCullingMask>              perCameraMaskHandle;
            [ReadOnly] public ComponentTypeHandle<ChunkPerFrameCullingMask>               perFrameMaskHandle;
            [ReadOnly] public ComponentTypeHandle<ChunkLinearBlendSkinningMemoryMetadata> metaHandle;
            [ReadOnly] public NativeArray<ArchetypeChunk>                                 changedChunks;

            public ComponentTypeHandle<LinearBlendSkinningShaderIndex> indicesHandle;

            public void Execute(int index)
            {
                var batchInChunk = changedChunks[index];

                var metadata   = batchInChunk.GetChunkComponentData(metaHandle);
                var cameraMask = batchInChunk.GetChunkComponentData(perCameraMaskHandle);
                var frameMask  = batchInChunk.GetChunkComponentData(perFrameMaskHandle);
                var lower      = new BitField64(cameraMask.lower.Value & (~frameMask.lower.Value));
                var upper      = new BitField64(cameraMask.upper.Value & (~frameMask.upper.Value));

                var indices   = batchInChunk.GetNativeArray(indicesHandle).Reinterpret<uint>();
                int prefixSum = metadata.bonesStartPrefixSum;

                for (int i = lower.CountTrailingZeros(); i < 64; lower.SetBits(i, false), i = lower.CountTrailingZeros())
                {
                    indices[i]  = (uint)prefixSum;
                    prefixSum  += metadata.bonesPerMesh;
                }

                for (int i = upper.CountTrailingZeros(); i < 64; upper.SetBits(i, false), i = upper.CountTrailingZeros())
                {
                    indices[i + 64]  = (uint)prefixSum;
                    prefixSum       += metadata.bonesPerMesh;
                }
            }
        }
    }
}

