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
    public partial struct UpdateChunkComputeDeformMetadataSystem : ISystem
    {
        EntityQuery m_query;

        public void OnCreate(ref SystemState state)
        {
            m_query = state.Fluent().WithAll<SkeletonDependent>(true).WithAll<ChunkComputeDeformMemoryMetadata>(false, true).Build();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.GetWorldBlackboardEntity().SetComponentData(new MaxRequiredDeformVertices { verticesCount = 0 });

            var lastSystemVersion = state.LastSystemVersion;
            var blobHandle        = state.GetComponentTypeHandle<SkeletonDependent>(true);
            var metaHandle        = state.GetComponentTypeHandle<ChunkComputeDeformMemoryMetadata>(false);

            state.Dependency = new UpdateChunkVertexCountsJob
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
        struct UpdateChunkVertexCountsJob : IJobEntityBatch
        {
            [ReadOnly] public ComponentTypeHandle<SkeletonDependent>     blobHandle;
            public ComponentTypeHandle<ChunkComputeDeformMemoryMetadata> metaHandle;
            public uint                                                  lastSystemVersion;

            public void Execute(ArchetypeChunk batchInChunk, int batchIndex)
            {
                bool needsUpdate  = batchInChunk.DidChange(blobHandle, lastSystemVersion);
                needsUpdate      |= batchInChunk.DidOrderChange(lastSystemVersion);
                if (!needsUpdate)
                    return;

                var blobs       = batchInChunk.GetNativeArray(blobHandle);
                int minVertices = int.MaxValue;
                int maxVertices = int.MinValue;

                for (int i = 0; i < batchInChunk.Count; i++)
                {
                    int c       = blobs[i].skinningBlob.Value.verticesToSkin.Length;
                    minVertices = math.min(minVertices, c);
                    maxVertices = math.max(maxVertices, c);
                }

                CheckVertexCountMismatch(minVertices, maxVertices);

                var metadata = batchInChunk.GetChunkComponentData(metaHandle);
                if (metadata.verticesPerMesh != maxVertices || metadata.entitiesInChunk != batchInChunk.Count)
                {
                    metadata.verticesPerMesh = maxVertices;
                    metadata.entitiesInChunk = batchInChunk.Count;
                    batchInChunk.SetChunkComponentData(metaHandle, metadata);
                }
            }

            [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
            void CheckVertexCountMismatch(int min, int max)
            {
                if (min != max)
                    UnityEngine.Debug.LogWarning(
                        "A chunk contains multiple Mesh Skinning Blobs with different vertex counts. Because Mesh Skinning Blobs are tied to their RenderMesh of which there is only one per chunk, this is likely a bug. Did you forget to change the Mesh Skinning Blob Reference when changing a Render Mesh?");
            }
        }
    }
}

