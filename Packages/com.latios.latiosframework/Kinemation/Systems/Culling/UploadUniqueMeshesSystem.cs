using Latios.Kinemation.Systems;
using Latios.Transforms;
using Latios.Unsafe;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine.Rendering;

namespace Latios.Kinemation
{
    [RequireMatchingQueriesForUpdate]
    [DisableAutoCreation]
    [BurstCompile]
    public partial struct UploadUniqueMeshesSystem : ISystem
    {
        LatiosWorldUnmanaged latiosWorld;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            latiosWorld = state.GetLatiosWorldUnmanaged();
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
        }

        struct CollectedChunk
        {
            public ArchetypeChunk chunk;
            public BitField64     lower;
            public BitField64     upper;
            public int            prefixSum;
        }

        [BurstCompile]
        struct FindAndValidateMeshesJob : IJobChunk
        {
            [ReadOnly] public EntityTypeHandle                                 entityHandle;
            [ReadOnly] public ComponentTypeHandle<MaterialMeshInfo>            mmiHandle;
            [ReadOnly] public BufferTypeHandle<UniqueMeshPosition>             positionHandle;
            [ReadOnly] public BufferTypeHandle<UniqueMeshNormal>               normalHandle;
            [ReadOnly] public BufferTypeHandle<UniqueMeshTangent>              tangentHandle;
            [ReadOnly] public BufferTypeHandle<UniqueMeshColor>                colorHandle;
            [ReadOnly] public BufferTypeHandle<UniqueMeshUv0xy>                uv0xyHandle;
            [ReadOnly] public BufferTypeHandle<UniqueMeshUv3xyz>               uv3xyzHandle;
            [ReadOnly] public BufferTypeHandle<UniqueMeshIndex>                indexHandle;
            [ReadOnly] public BufferTypeHandle<UniqueMeshSubmesh>              submeshHandle;
            [ReadOnly] public ComponentTypeHandle<TrackedUniqueMesh>           trackedHandle;
            [ReadOnly] public ComponentTypeHandle<ChunkPerDispatchCullingMask> maskHandle;
            [ReadOnly] public UniqueMeshPool                                   meshPool;

            public ComponentTypeHandle<UniqueMeshConfig>                            configHandle;
            public UnsafeParallelBlockList                                          meshIDsToInvalidate;
            [NativeDisableParallelForRestriction] public NativeList<CollectedChunk> collectedChunks;  // Preallocated to query chunk count without filtering

            [NativeSetThreadIndex]
            int threadIndex;

            public unsafe void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                var configurations = (UniqueMeshConfig*)chunk.GetRequiredComponentDataPtrRO(ref configHandle);

                // 1) Only consider visible meshes or meshes with forced uploads
                ChunkPerDispatchCullingMask maskToProcess = default;
                if (chunk.HasChunkComponent(ref maskHandle))
                    maskToProcess = chunk.GetChunkComponentData(ref maskHandle);

                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                while (enumerator.NextEntityIndex(out var entityIndex))
                {
                    if (configurations[entityIndex].forceUpload)
                    {
                        if (entityIndex < 64)
                            maskToProcess.lower.SetBits(entityIndex, true);
                        else
                            maskToProcess.upper.SetBits(entityIndex - 64, true);
                    }
                }
                if ((maskToProcess.lower.Value | maskToProcess.upper.Value) == 0)
                    return;

                // 2) Identify which meshes still need validation.
                var        mmis          = (MaterialMeshInfo*)chunk.GetRequiredComponentDataPtrRO(ref mmiHandle);
                BitField64 validateLower = default, validateUpper = default;
                enumerator               = new ChunkEntityEnumerator(true, new v128(maskToProcess.lower.Value, maskToProcess.upper.Value), chunk.Count);
                while (enumerator.NextEntityIndex(out var entityIndex))
                {
                    if (!meshPool.meshesPrevalidatedThisFrame.Contains(mmis[entityIndex].MeshID))
                    {
                        if (entityIndex < 64)
                            validateLower.SetBits(entityIndex, true);
                        else
                            validateUpper.SetBits(entityIndex - 64, true);
                    }
                }

                // New configurations to validate
                var configuredBits = chunk.GetEnabledMask(ref configHandle);

                // 3) Validate meshes still needing validation if necessary
                if ((validateLower.Value | validateUpper.Value) != 0)
                {
                    var validator = new UniqueMeshValidator
                    {
                        configurations  = configurations,
                        entities        = chunk.GetEntityDataPtrRO(entityHandle),
                        positionBuffers = chunk.GetBufferAccessor(ref positionHandle),
                        normalBuffers   = chunk.GetBufferAccessor(ref normalHandle),
                        tangentBuffers  = chunk.GetBufferAccessor(ref tangentHandle),
                        colorBuffers    = chunk.GetBufferAccessor(ref colorHandle),
                        uv0xyBuffers    = chunk.GetBufferAccessor(ref uv0xyHandle),
                        uv3xyzBuffers   = chunk.GetBufferAccessor(ref uv3xyzHandle),
                        indexBuffers    = chunk.GetBufferAccessor(ref indexHandle),
                        submeshBuffers  = chunk.GetBufferAccessor(ref submeshHandle),
                    };
                    validator.Init();

                    enumerator = new ChunkEntityEnumerator(true, new v128(validateLower.Value, validateUpper.Value), chunk.Count);
                    while (enumerator.NextEntityIndex(out var entityIndex))
                    {
                        if (!validator.IsEntityIndexValidMesh(entityIndex))
                        {
                            // Mark the entity as configured now so that we don't try to process it again until the user fixes the problem.
                            configuredBits[entityIndex] = false;
                            // Mark the entity as invalid so that we can cull it in future updates, but only if the status changed.
                            if (meshPool.invalidMeshesToCull.Contains(mmis[entityIndex].MeshID))
                            {
                                meshIDsToInvalidate.Write(mmis[entityIndex].MeshID, threadIndex);
                            }
                            // Mark the entity as non-processable
                            maskToProcess.ClearBitAtIndex(entityIndex);
                        }
                    }
                }

                // 4) Export chunk
                collectedChunks[unfilteredChunkIndex] = new CollectedChunk
                {
                    chunk = chunk,
                    lower = maskToProcess.lower,
                    upper = maskToProcess.upper,
                };
            }
        }

        [BurstCompile]
        struct OrganizeMeshesJob : IJob
        {
            public NativeList<CollectedChunk> collectedChunks;
            public UniqueMeshPool             meshPool;
            public UnsafeParallelBlockList    meshIDsToInvalidate;

            public void Execute()
            {
                var enumerator = meshIDsToInvalidate.GetEnumerator();
                while (enumerator.MoveNext())
                {
                    var id = enumerator.GetCurrent<BatchMeshID>();
                    meshPool.invalidMeshesToCull.Add(id);
                }

                int prefixSum  = 0;
                int writeIndex = 0;
                for (int i = 0; i < collectedChunks.Length; i++)
                {
                    var chunk = collectedChunks[i];
                    if (chunk.chunk == default)
                        continue;

                    chunk.prefixSum              = prefixSum;
                    prefixSum                   += chunk.lower.CountBits() + chunk.upper.CountBits();
                    collectedChunks[writeIndex]  = chunk;
                    writeIndex++;
                }
                collectedChunks.Length = writeIndex;
            }
        }
    }
}

