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
                if ((maskToProcess.lower.Value | maskToProcess.upper.Value) == 0)
                    return;

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

        [BurstCompile]
        struct WriteMeshesJob : IJobFor
        {
            [ReadOnly] public NativeArray<CollectedChunk>            collectedChunks;
            [ReadOnly] public ComponentTypeHandle<MaterialMeshInfo>  mmiHandle;
            [ReadOnly] public ComponentTypeHandle<TrackedUniqueMesh> trackedHandle;
            [ReadOnly] public UniqueMeshPool                         meshPool;

            public ComponentTypeHandle<UniqueMeshConfig> configHandle;
            public BufferTypeHandle<UniqueMeshPosition>  positionHandle;
            public BufferTypeHandle<UniqueMeshNormal>    normalHandle;
            public BufferTypeHandle<UniqueMeshTangent>   tangentHandle;
            public BufferTypeHandle<UniqueMeshColor>     colorHandle;
            public BufferTypeHandle<UniqueMeshUv0xy>     uv0xyHandle;
            public BufferTypeHandle<UniqueMeshUv3xyz>    uv3xyzHandle;
            public BufferTypeHandle<UniqueMeshIndex>     indexHandle;
            public BufferTypeHandle<UniqueMeshSubmesh>   submeshHandle;

            [NativeDisableParallelForRestriction] public UnityEngine.Mesh.MeshDataArray                 meshDataArray;
            [NativeDisableParallelForRestriction] public NativeArray<UnityObjectRef<UnityEngine.Mesh> > meshesToUpload;

            [NativeDisableContainerSafetyRestriction] NativeList<float3>                    tempNormals;
            [NativeDisableContainerSafetyRestriction] NativeList<float4>                    tempTangents;
            [NativeDisableContainerSafetyRestriction] NativeList<VertexAttributeDescriptor> tempDescriptors;

            public unsafe void Execute(int chunkIndex)
            {
                if (!tempDescriptors.IsCreated)
                    tempDescriptors = new NativeList<VertexAttributeDescriptor>(8, Allocator.Temp);

                var chunk = collectedChunks[chunkIndex];

                // Assign all the meshes now for a little better cache coherency.
                var mask    = chunk.chunk.GetEnabledMask(ref configHandle);
                var tracked = chunk.chunk.GetComponentDataPtrRO(ref trackedHandle);
                if (tracked != null)
                {
                    int dstIndex = chunk.prefixSum;
                    var e        = new ChunkEntityEnumerator(true, new v128(chunk.lower.Value, chunk.upper.Value), chunk.chunk.Count);
                    while (e.NextEntityIndex(out var entityIndex))
                    {
                        mask[entityIndex]        = false;
                        meshesToUpload[dstIndex] = tracked[entityIndex].mesh;
                        dstIndex++;
                    }
                }
                else
                {
                    var mmis     = chunk.chunk.GetComponentDataPtrRO(ref mmiHandle);
                    int dstIndex = chunk.prefixSum;
                    var e        = new ChunkEntityEnumerator(true, new v128(chunk.lower.Value, chunk.upper.Value), chunk.chunk.Count);
                    while (e.NextEntityIndex(out var entityIndex))
                    {
                        mask[entityIndex]        = false;
                        meshesToUpload[dstIndex] = meshPool.idToMeshMap[mmis[entityIndex].MeshID];
                        dstIndex++;
                    }
                }

                var configurations  = chunk.chunk.GetComponentDataPtrRO(ref configHandle);
                var positionBuffers = chunk.chunk.GetBufferAccessor(ref positionHandle);
                var normalBuffers   = chunk.chunk.GetBufferAccessor(ref normalHandle);
                var tangentBuffers  = chunk.chunk.GetBufferAccessor(ref tangentHandle);
                var colorBuffers    = chunk.chunk.GetBufferAccessor(ref colorHandle);
                var uv0xyBuffers    = chunk.chunk.GetBufferAccessor(ref uv0xyHandle);
                var uv3xyzBuffers   = chunk.chunk.GetBufferAccessor(ref uv3xyzHandle);
                var indexBuffers    = chunk.chunk.GetBufferAccessor(ref indexHandle);
                var submeshBuffers  = chunk.chunk.GetBufferAccessor(ref submeshHandle);

                int meshIndex  = chunk.prefixSum;
                var enumerator = new ChunkEntityEnumerator(true, new v128(chunk.lower.Value, chunk.upper.Value), chunk.chunk.Count);
                while (enumerator.NextEntityIndex(out var entityIndex))
                {
                    var config = configurations[entityIndex];

                    // Capture the arrays, possibly performing normal and tangent recalculation.
                    var                 indices   = indexBuffers.Length > 0 ? indexBuffers[entityIndex].AsNativeArray().Reinterpret<int>() : default;
                    var                 submeshes = submeshBuffers.Length > 0 ? submeshBuffers[entityIndex].AsNativeArray() : default;
                    var                 positions = positionBuffers.Length > 0 ? positionBuffers[entityIndex].AsNativeArray().Reinterpret<float3>() : default;
                    NativeArray<float3> normals   = default;
                    if (config.calculateNormals)
                    {
                        if (normalBuffers.Length > 0)
                        {
                            var nb = normalBuffers[entityIndex];
                            nb.ResizeUninitialized(positions.Length);
                            normals = nb.AsNativeArray().Reinterpret<float3>();
                        }
                        else
                        {
                            if (!tempNormals.IsCreated)
                                tempNormals = new NativeList<float3>(positions.Length, Allocator.Temp);
                            tempNormals.ResizeUninitialized(positions.Length);
                            normals = tempNormals.AsArray();
                        }
                        RecalculateNormals(positions, normals, indices, submeshes);
                    }
                    else if (normalBuffers.Length > 0)
                    {
                        normals = normalBuffers[entityIndex].AsNativeArray().Reinterpret<float3>();
                    }
                    var                 uv0xys   = uv0xyBuffers.Length > 0 ? uv0xyBuffers[entityIndex].AsNativeArray().Reinterpret<float2>() : default;
                    NativeArray<float4> tangents = default;
                    if (config.calculateTangents)
                    {
                        if (tangentBuffers.Length > 0)
                        {
                            var nb = tangentBuffers[entityIndex];
                            nb.ResizeUninitialized(positions.Length);
                            tangents = nb.AsNativeArray().Reinterpret<float4>();
                        }
                        else
                        {
                            if (!tempTangents.IsCreated)
                                tempTangents = new NativeList<float4>(positions.Length, Allocator.Temp);
                            tempTangents.ResizeUninitialized(positions.Length);
                            tangents = tempTangents.AsArray();
                        }
                        RecalculateTangents(positions, normals, tangents, uv0xys, indices, submeshes);
                    }
                    else if (tangentBuffers.Length > 0)
                    {
                        tangents = tangentBuffers[entityIndex].AsNativeArray().Reinterpret<float4>();
                    }
                    var colors  = colorBuffers.Length > 0 ? colorBuffers[entityIndex].AsNativeArray().Reinterpret<float4>() : default;
                    var uv3xyzs = uv3xyzBuffers.Length > 0 ? uv3xyzBuffers[entityIndex].AsNativeArray().Reinterpret<float3>() : default;

                    // Set up vertex attributes
                    tempDescriptors.Clear();
                    int vertexCount = 0;
                    if (positions.Length > 0)
                    {
                        vertexCount = positions.Length;
                        tempDescriptors.Add(new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3, 0));
                    }
                    if (normals.Length > 0)
                    {
                        vertexCount = normals.Length;
                        tempDescriptors.Add(new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3, 0));
                    }
                    if (tangents.Length > 0)
                    {
                        vertexCount = tangents.Length;
                        tempDescriptors.Add(new VertexAttributeDescriptor(VertexAttribute.Tangent, VertexAttributeFormat.Float32, 4, 0));
                    }
                    if (colors.Length > 0)
                    {
                        vertexCount = colors.Length;
                        tempDescriptors.Add(new VertexAttributeDescriptor(VertexAttribute.Color));
                    }
                    if (uv0xys.Length > 0)
                    {
                        vertexCount = uv0xys.Length;
                        tempDescriptors.Add(new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2, 0));
                    }
                    if (uv3xyzBuffers.Length > 0)
                    {
                        vertexCount = uv3xyzs.Length;
                        tempDescriptors.Add(new VertexAttributeDescriptor(VertexAttribute.TexCoord3, VertexAttributeFormat.Float32, 3, 0));
                    }

                    if (vertexCount == 0)
                    {
                        // We have an attribute-free mesh. We need to extract the vertex count from the indices.
                        if (indices.Length > 0)
                        {
                            foreach (var vindex in indices)
                            {
                                vertexCount = math.max(vertexCount, vindex + 1);
                            }
                        }
                        else
                        {
                            // Our mesh is fully defined by the submesh.
                            foreach (var submesh in submeshes)
                            {
                                vertexCount = math.max(vertexCount, submesh.indexStart + submesh.indexCount);
                            }
                        }
                    }

                    var meshData = meshDataArray[meshIndex];
                    meshData.SetVertexBufferParams(vertexCount, tempDescriptors.AsArray());
                    var stream0 = meshData.GetVertexData<float>(0);

                    int writeIndex = 0;
                    for (int i = 0; i < vertexCount; i++)
                    {
                        if (positions.Length > 0)
                            Write(ref writeIndex, ref stream0, positions[i]);
                        if (normals.Length > 0)
                            Write(ref writeIndex, ref stream0, normals[i]);
                        if (tangents.Length > 0)
                            Write(ref writeIndex, ref stream0, tangents[i]);
                        if (colors.Length > 0)
                            Write(ref writeIndex, ref stream0, colors[i]);
                        if (uv0xys.Length > 0)
                            Write(ref writeIndex, ref stream0, uv0xys[i]);
                        if (uv3xyzs.Length > 0)
                            Write(ref writeIndex, ref stream0, uv3xyzs[i]);
                    }

                    // Process indices
                    int indexCount = indices.Length > 0 ? indices.Length : vertexCount;
                    if (vertexCount < ushort.MaxValue)
                    {
                        meshData.SetIndexBufferParams(indexCount, IndexFormat.UInt16);
                        var indexStream = meshData.GetIndexData<ushort>();
                        if (indices.Length > 0)
                        {
                            for (int i = 0; i < indices.Length; i++)
                            {
                                indexStream[i] = (ushort)indices[i];
                            }
                        }
                        else
                        {
                            for (ushort i = 0; i < indexCount; i++)
                            {
                                indexStream[i] = i;
                            }
                        }
                    }
                    else
                    {
                        meshData.SetIndexBufferParams(indexCount, IndexFormat.UInt32);
                        var indexStream = meshData.GetIndexData<int>();
                        if (indices.Length > 0)
                        {
                            indexStream.CopyFrom(indices);
                        }
                        else
                        {
                            for (int i = 0; i < indexCount; i++)
                            {
                                indexStream[i] = i;
                            }
                        }
                    }

                    // Process submeshes
                    meshData.subMeshCount = math.max(1, submeshes.Length);
                    var flags             = MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontResetBoneBounds | MeshUpdateFlags.DontNotifyMeshUsers |
                                            MeshUpdateFlags.DontRecalculateBounds;
                    if (submeshes.Length == 0)
                    {
                        meshData.SetSubMesh(0, new SubMeshDescriptor(0, indexCount, UnityEngine.MeshTopology.Triangles), flags);
                    }
                    else
                    {
                        for (int submeshIndex = 0; submeshIndex < submeshes.Length; submeshIndex++)
                        {
                            var submesh  = submeshes[submeshIndex];
                            var topology = submesh.topology switch
                            {
                                UniqueMeshSubmesh.Topology.Triangles => UnityEngine.MeshTopology.Triangles,
                                UniqueMeshSubmesh.Topology.Lines => UnityEngine.MeshTopology.Lines,
                                UniqueMeshSubmesh.Topology.LineStrip => UnityEngine.MeshTopology.LineStrip,
                                UniqueMeshSubmesh.Topology.Points => UnityEngine.MeshTopology.Points,
                                _ => UnityEngine.MeshTopology.Triangles
                            };
                            meshData.SetSubMesh(submeshIndex, new SubMeshDescriptor(submesh.indexStart, submesh.indexCount, topology), flags);
                        }
                    }

                    // Process buffer clearing option
                    if (config.reclaimDynamicBufferMemoryAfterUpload)
                    {
                        if (positionBuffers.Length > 0)
                        {
                            var b = positionBuffers[entityIndex];
                            b.Clear();
                            b.TrimExcess();
                        }
                        if (normalBuffers.Length > 0)
                        {
                            var b = normalBuffers[entityIndex];
                            b.Clear();
                            b.TrimExcess();
                        }
                        if (tangentBuffers.Length > 0)
                        {
                            var b = tangentBuffers[entityIndex];
                            b.Clear();
                            b.TrimExcess();
                        }
                        if (colorBuffers.Length > 0)
                        {
                            var b = colorBuffers[entityIndex];
                            b.Clear();
                            b.TrimExcess();
                        }
                        if (uv0xyBuffers.Length > 0)
                        {
                            var b = uv0xyBuffers[entityIndex];
                            b.Clear();
                            b.TrimExcess();
                        }
                        if (uv3xyzBuffers.Length > 0)
                        {
                            var b = uv3xyzBuffers[entityIndex];
                            b.Clear();
                            b.TrimExcess();
                        }
                        if (indexBuffers.Length > 0)
                        {
                            var b = indexBuffers[entityIndex];
                            b.Clear();
                            b.TrimExcess();
                        }
                        if (submeshBuffers.Length > 0)
                        {
                            var b = submeshBuffers[entityIndex];
                            b.Clear();
                            b.TrimExcess();
                        }
                    }
                }
            }

            void RecalculateNormals(NativeArray<float3> positions, NativeArray<float3> normals, NativeArray<int> indices, NativeArray<UniqueMeshSubmesh> submeshes)
            {
                // Todo:
            }

            void RecalculateTangents(NativeArray<float3>            positions,
                                     NativeArray<float3>            normals,
                                     NativeArray<float4>            tangents,
                                     NativeArray<float2>            uvs,
                                     NativeArray<int>               indices,
                                     NativeArray<UniqueMeshSubmesh> submeshes)
            {
                // Todo:
            }

            void Write(ref int writeIndex, ref NativeArray<float> stream, float2 value)
            {
                stream[writeIndex++] = value.x;
                stream[writeIndex++] = value.y;
            }

            void Write(ref int writeIndex, ref NativeArray<float> stream, float3 value)
            {
                stream[writeIndex++] = value.x;
                stream[writeIndex++] = value.y;
                stream[writeIndex++] = value.z;
            }

            void Write(ref int writeIndex, ref NativeArray<float> stream, float4 value)
            {
                stream[writeIndex++] = value.x;
                stream[writeIndex++] = value.y;
                stream[writeIndex++] = value.z;
                stream[writeIndex++] = value.w;
            }
        }
    }
}

