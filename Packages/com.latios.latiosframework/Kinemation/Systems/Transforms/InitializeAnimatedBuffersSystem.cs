using Latios.Systems;
using Latios.Transforms;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

using static Unity.Entities.SystemAPI;

namespace Latios.Kinemation.Systems
{
    // This system must update before TransformInitializeSuperSystem, because sockets rely on this before the
    // early transform system update. Also, MotionHistoryInitializeSuperSystem doesn't exist in Unity Transforms.
    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
    [UpdateInGroup(typeof(PostSyncPointGroup), OrderFirst = true)]
    [RequireMatchingQueriesForUpdate]
    [DisableAutoCreation]
    [BurstCompile]
    public partial struct InitializeAnimatedBuffersSystem : ISystem
    {
        EntityQuery m_initSkeletonsQuery;
        EntityQuery m_initBlendShapesQuery;
        EntityQuery m_initDynamicMeshesQuery;

        public void OnCreate(ref SystemState state)
        {
            m_initSkeletonsQuery = state.Fluent().With<OptimizedSkeletonState>(true).With<OptimizedBoneTransform>(false)
                                   .With<OptimizedSkeletonHierarchyBlobReference>(true).IncludeDisabledEntities().Build();
            m_initBlendShapesQuery   = state.Fluent().With<BlendShapeState>(true).With<BlendShapeWeight>(false).With<BoundMesh>(true).Build();
            m_initDynamicMeshesQuery = state.Fluent().With<DynamicMeshState>(true).With<DynamicMeshVertex>(false).With<BoundMesh>(true).Build();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var lastSystemVersion = state.GetLiveBakeSafeLastSystemVersion();
            var skeletonJh        = new InitSkeletonJob
            {
                lastSystemVersion = lastSystemVersion,
                blobHandle        = GetComponentTypeHandle<OptimizedSkeletonHierarchyBlobReference>(true),
                bonesReadHandle   = GetBufferTypeHandle<OptimizedBoneTransform>(true),
                bonesWriteHandle  = GetBufferTypeHandle<OptimizedBoneTransform>(false),
                stateHandle       = GetComponentTypeHandle<OptimizedSkeletonState>(false),
            }.ScheduleParallel(m_initSkeletonsQuery, state.Dependency);

            var blendShapeJh = new InitBlendShapesJob
            {
                lastSystemVersion  = lastSystemVersion,
                blobHandle         = GetComponentTypeHandle<BoundMesh>(true),
                weightsReadHandle  = GetBufferTypeHandle<BlendShapeWeight>(true),
                weightsWriteHandle = GetBufferTypeHandle<BlendShapeWeight>(false)
            }.ScheduleParallel(m_initBlendShapesQuery, state.Dependency);

            var meshJh = new InitMeshJob
            {
                lastSystemVersion   = lastSystemVersion,
                blobHandle          = GetComponentTypeHandle<BoundMesh>(true),
                verticesReadHandle  = GetBufferTypeHandle<DynamicMeshVertex>(true),
                verticesWriteHandle = GetBufferTypeHandle<DynamicMeshVertex>(false)
            }.ScheduleParallel(m_initDynamicMeshesQuery, state.Dependency);

            state.Dependency = JobHandle.CombineDependencies(skeletonJh, blendShapeJh, meshJh);
        }

        [BurstCompile]
        struct InitSkeletonJob : IJobChunk
        {
            public BufferTypeHandle<OptimizedBoneTransform>                                                     bonesWriteHandle;
            public ComponentTypeHandle<OptimizedSkeletonState>                                                  stateHandle;
            [ReadOnly, NativeDisableContainerSafetyRestriction] public BufferTypeHandle<OptimizedBoneTransform> bonesReadHandle;
            [ReadOnly] public ComponentTypeHandle<OptimizedSkeletonHierarchyBlobReference>                      blobHandle;

            public uint lastSystemVersion;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                if (!chunk.DidOrderChange(lastSystemVersion))
                    return;

                var  blobs      = chunk.GetNativeArray(ref blobHandle);
                var  bones      = chunk.GetBufferAccessor(ref bonesReadHandle);
                bool needsWrite = false;

                // Much more likely for there to be a new entity at the end of a chunk.
                for (int i = chunk.Count - 1; i >= 0; i--)
                {
                    if (bones[i].Length != blobs[i].blob.Value.parentIndices.Length * 6)
                    {
                        needsWrite = true;
                        break;
                    }
                }

                if (!needsWrite)
                    return;

                bones      = chunk.GetBufferAccessor(ref bonesWriteHandle);
                var states = chunk.GetNativeArray(ref stateHandle);
                for (int i = 0; i < chunk.Count; i++)
                {
                    var boneCount = blobs[i].blob.Value.parentIndices.Length;
                    var buffer    = bones[i];
                    if (buffer.Length == boneCount * 6)
                        continue;

                    if (buffer.Length == boneCount)
                    {
                        // Buffer only contains local transforms.
                        buffer.Resize(boneCount * 6, NativeArrayOptions.UninitializedMemory);
                        var     bufferAsArray   = buffer.Reinterpret<TransformQvvs>().AsNativeArray();
                        ref var parentIndices   = ref blobs[i].blob.Value.parentIndices;
                        var     rootTransforms  = bufferAsArray.GetSubArray(0, boneCount);
                        var     localTransforms = bufferAsArray.GetSubArray(boneCount, boneCount);
                        localTransforms.CopyFrom(rootTransforms);
                        rootTransforms[0] = TransformQvvs.identity;
                        for (int j = 1; j < boneCount; j++)
                        {
                            var parent           = math.max(0, parentIndices[j]);
                            var local            = localTransforms[j];
                            local.rotation.value = math.normalize(local.rotation.value);
                            rootTransforms[j]    = qvvs.mul(rootTransforms[parent], in local);
                        }
                        {
                            var local            = localTransforms[0];
                            local.rotation.value = math.normalize(local.rotation.value);
                            localTransforms[0]   = local;
                            rootTransforms[0]    = local;
                        }
                        bufferAsArray.GetSubArray(boneCount * 2, boneCount * 2).CopyFrom(bufferAsArray.GetSubArray(0, boneCount * 2));
                        bufferAsArray.GetSubArray(boneCount * 4, boneCount * 2).CopyFrom(bufferAsArray.GetSubArray(0, boneCount * 2));
                    }
                    else if (buffer.Length < boneCount * 6)  // Typically (buffer.Length == 0)
                    {
                        // Todo: Should we leave this uninitialized instead?
                        buffer.Resize(boneCount * 6, NativeArrayOptions.ClearMemory);
                        var s      = states[i];
                        s.state   |= OptimizedSkeletonState.Flags.NeedsHistorySync;
                        states[i]  = s;
                    }
                }
            }
        }

        [BurstCompile]
        struct InitBlendShapesJob : IJobChunk
        {
            public BufferTypeHandle<BlendShapeWeight>                                                     weightsWriteHandle;
            [ReadOnly, NativeDisableContainerSafetyRestriction] public BufferTypeHandle<BlendShapeWeight> weightsReadHandle;
            [ReadOnly] public ComponentTypeHandle<BoundMesh>                                              blobHandle;

            public uint lastSystemVersion;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                if (!chunk.DidOrderChange(lastSystemVersion))
                    return;

                var  blobs      = chunk.GetNativeArray(ref blobHandle);
                var  weights    = chunk.GetBufferAccessor(ref weightsReadHandle);
                bool needsWrite = false;

                for (int i = chunk.Count - 1; i >= 0; i--)
                {
                    if (weights[i].Length != blobs[i].meshBlob.Value.blendShapesData.shapes.Length * 3)
                    {
                        needsWrite = true;
                        break;
                    }
                }

                if (!needsWrite)
                    return;

                weights = chunk.GetBufferAccessor(ref weightsWriteHandle);

                for (int i = 0; i < chunk.Count; i++)
                {
                    int shapes = blobs[i].meshBlob.Value.blendShapesData.shapeNames.Length;
                    if (shapes == 0)
                    {
                        UnityEngine.Debug.LogWarning($"Mesh {blobs[i].meshBlob.Value.name} does not have blend shapes!");
                        weights[i].Clear();
                    }
                    else if (weights[i].Length == shapes * 3)
                        continue;
                    else if (weights[i].Length == shapes)
                    {
                        weights[i].Resize(shapes * 3, NativeArrayOptions.UninitializedMemory);
                        var array       = weights[i].AsNativeArray();
                        var subArraySrc = array.GetSubArray(0, shapes);
                        var subArrayDst = array.GetSubArray(shapes, shapes);
                        subArrayDst.CopyFrom(subArraySrc);
                        subArrayDst = array.GetSubArray(shapes * 2, shapes);
                        subArrayDst.CopyFrom(subArraySrc);
                    }
                    else
                        weights[i].Resize(blobs[i].meshBlob.Value.blendShapesData.shapes.Length * 3, NativeArrayOptions.ClearMemory);
                }
            }
        }

        [BurstCompile]
        struct InitMeshJob : IJobChunk
        {
            public BufferTypeHandle<DynamicMeshVertex>                                                     verticesWriteHandle;
            [ReadOnly, NativeDisableContainerSafetyRestriction] public BufferTypeHandle<DynamicMeshVertex> verticesReadHandle;
            [ReadOnly] public ComponentTypeHandle<BoundMesh>                                               blobHandle;

            public uint lastSystemVersion;

            public unsafe void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                if (!chunk.DidOrderChange(lastSystemVersion))
                    return;

                var  blobs      = chunk.GetNativeArray(ref blobHandle);
                var  vertices   = chunk.GetBufferAccessor(ref verticesReadHandle);
                bool needsWrite = false;

                for (int i = chunk.Count - 1; i >= 0; i--)
                {
                    if (vertices[i].Length != blobs[i].meshBlob.Value.undeformedVertices.Length * 3)
                    {
                        needsWrite = true;
                        break;
                    }
                }

                if (!needsWrite)
                    return;

                vertices = chunk.GetBufferAccessor(ref verticesWriteHandle);

                for (int i = 0; i < chunk.Count; i++)
                {
                    if (vertices[i].Length != blobs[i].meshBlob.Value.undeformedVertices.Length * 3)
                    {
                        vertices[i].Resize(blobs[i].meshBlob.Value.undeformedVertices.Length * 3, NativeArrayOptions.UninitializedMemory);
                        UnsafeUtility.MemCpyReplicate(vertices[i].GetUnsafePtr(),
                                                      blobs[i].meshBlob.Value.undeformedVertices.GetUnsafePtr(),
                                                      sizeof(UndeformedVertex) * blobs[i].meshBlob.Value.undeformedVertices.Length,
                                                      3);
                    }
                }
            }
        }
    }
}

