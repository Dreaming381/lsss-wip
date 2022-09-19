using System.Diagnostics;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace Latios.Kinemation.Systems
{
    [DisableAutoCreation]
    [BurstCompile]
    public partial struct UpdateChunkLinearBlendMetadataSystem : ISystem
    {
        EntityQuery m_query;

        public void OnCreate(ref SystemState state)
        {
            m_query = state.Fluent().WithAll<SkeletonDependent>(true).WithAll<ChunkLinearBlendSkinningMemoryMetadata>(false, true).Build();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.GetWorldBlackboardEntity().SetComponentData(new MaxRequiredLinearBlendMatrices { matricesCount = 0 });

            var lastSystemVersion = state.LastSystemVersion;
            var blobHandle        = state.GetComponentTypeHandle<SkeletonDependent>(true);
            var metaHandle        = state.GetComponentTypeHandle<ChunkLinearBlendSkinningMemoryMetadata>(false);

            state.Dependency = new UpdateChunkMatrixCountsJob
            {
                blobHandle        = blobHandle,
                metaHandle        = metaHandle,
                lastSystemVersion = lastSystemVersion
            }.ScheduleParallel(m_query, state.Dependency);
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state) {
        }

        [BurstCompile]
        struct UpdateChunkMatrixCountsJob : IJobEntityBatch
        {
            [ReadOnly] public ComponentTypeHandle<SkeletonDependent>           blobHandle;
            public ComponentTypeHandle<ChunkLinearBlendSkinningMemoryMetadata> metaHandle;
            public uint                                                        lastSystemVersion;

            public void Execute(ArchetypeChunk batchInChunk, int batchIndex)
            {
                bool needsUpdate  = batchInChunk.DidChange(blobHandle, lastSystemVersion);
                needsUpdate      |= batchInChunk.DidOrderChange(lastSystemVersion);
                if (!needsUpdate)
                    return;

                var blobs       = batchInChunk.GetNativeArray(blobHandle);
                int minMatrices = int.MaxValue;
                int maxMatrices = int.MinValue;

                for (int i = 0; i < batchInChunk.Count; i++)
                {
                    int c       = blobs[i].skinningBlob.Value.bindPoses.Length;
                    minMatrices = math.min(minMatrices, c);
                    maxMatrices = math.max(maxMatrices, c);
                }

                CheckMatrixCountMismatch(minMatrices, maxMatrices);

                var metadata = batchInChunk.GetChunkComponentData(metaHandle);
                if (metadata.bonesPerMesh != maxMatrices || metadata.entitiesInChunk != batchInChunk.Count)
                {
                    metadata.bonesPerMesh    = maxMatrices;
                    metadata.entitiesInChunk = batchInChunk.Count;
                    batchInChunk.SetChunkComponentData(metaHandle, metadata);
                }
            }

            [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
            void CheckMatrixCountMismatch(int min, int max)
            {
                if (min != max)
                    UnityEngine.Debug.LogWarning(
                        "A chunk contains multiple Mesh Skinning Blobs with different matrix counts. Because Mesh Skinning Blobs are tied to their RenderMesh of which there is only one per chunk, this is likely a bug. Did you forget to change the Mesh Skinning Blob Reference when changing a Render Mesh?");
            }
        }
    }
}

