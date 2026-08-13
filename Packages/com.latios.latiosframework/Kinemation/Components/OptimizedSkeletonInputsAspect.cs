using System.Diagnostics;
using Latios.Transforms;
using Latios.Unsafe;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

using static Latios.IJobEach;

namespace Latios.Kinemation
{
    /// <summary>
    /// The inputs needed to create an OptimizedSkeletonAspect except for the entity's transform.
    /// Combine this with a TransformAspect (QVVS Transforms) or Unity.Transforms.LocalToWorld (Unity Transforms)
    /// via ToSkeleton() to obtain the full OptimizedSkeletonAspect.
    /// </summary>
    [ParameterHandle(typeof(OptimizedSkeletonInputsAspectParameterHandle), ScheduleModeMask.All)]
    public unsafe struct OptimizedSkeletonInputsAspect : IParameter
    {
        internal RefRO<OptimizedSkeletonHierarchyBlobReference> m_skeletonHierarchyBlobRef;
        internal RefRW<OptimizedSkeletonState>                  m_skeletonState;
        internal DynamicBuffer<OptimizedBoneTransform>          m_boneTransforms;
        internal DynamicBuffer<OptimizedBoneInertialBlendState> m_bonesInertialBlendStates;
        internal DynamicBuffer<DependentSkinnedMesh>            m_optionalDependentSkinnedMeshes;
        internal short                                          m_boneCount;
#if !LATIOS_TRANSFORMS_UNITY
        internal ComponentLookup<Socket>* m_socketLookupAccess;
#endif

        /// <summary>
        /// The number of bones in the skeleton, derived directly from the skeleton's hierarchy blob.
        /// </summary>
        public int boneCount => m_boneCount;

#if LATIOS_TRANSFORMS_UNITY
        /// <summary>
        /// Combines this OptimizedSkeletonInputsAspect with the specified LocalToWorld to construct the full OptimizedSkeletonAspect.
        /// </summary>
        public OptimizedSkeletonAspect ToSkeleton(in Unity.Transforms.LocalToWorld localToWorld)
        {
            return new OptimizedSkeletonAspect(in localToWorld,
                                               m_skeletonHierarchyBlobRef,
                                               m_skeletonState,
                                               ref m_boneTransforms,
                                               ref m_bonesInertialBlendStates,
                                               m_optionalDependentSkinnedMeshes);
        }
#else
        /// <summary>
        /// Combines this OptimizedSkeletonInputsAspect with the specified TransformAspect to construct the full OptimizedSkeletonAspect.
        /// </summary>
        public OptimizedSkeletonAspect ToSkeleton(TransformAspect transformAspect)
        {
            return new OptimizedSkeletonAspect(transformAspect,
                                               ref *m_socketLookupAccess,
                                               m_skeletonHierarchyBlobRef,
                                               m_skeletonState,
                                               ref m_boneTransforms,
                                               ref m_bonesInertialBlendStates,
                                               m_optionalDependentSkinnedMeshes);
        }
#endif
    }

    // WARNING: The following is for internal use in IJobEach only.
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public unsafe struct OptimizedSkeletonInputsAspectParameterHandle : IParameterHandle<OptimizedSkeletonInputsAspect>
    {
        struct Cache
        {
            public NativeArray<OptimizedSkeletonHierarchyBlobReference> chunkHierarchies;
            public NativeArray<OptimizedSkeletonState>                  chunkStates;
            public BufferAccessor<OptimizedBoneTransform>               chunkBoneTransforms;
            public BufferAccessor<OptimizedBoneInertialBlendState>      chunkBlendStates;
#if !LATIOS_TRANSFORMS_UNITY
            public BufferAccessor<DependentSkinnedMesh> chunkOptionalSkinnedMeshes;
#endif
        }

        [ReadOnly] ComponentTypeHandle<OptimizedSkeletonHierarchyBlobReference> hierarchyHandle;
        ComponentTypeHandle<OptimizedSkeletonState>                             stateHandle;
        BufferTypeHandle<OptimizedBoneTransform>                                boneTransformHandle;
        BufferTypeHandle<OptimizedBoneInertialBlendState>                       blendStateHandle;
#if !LATIOS_TRANSFORMS_UNITY
        [ReadOnly] BufferTypeHandle<DependentSkinnedMesh> skinnedMeshesHandle;
        [ReadOnly] ComponentLookup<Socket>                socketLookup;
#endif
        ThreadCache<Cache> threadCache;

        bool isChunkInitialized;

        public FluentQuery AppendToQuery(FluentQuery query)
        {
            return query.With<OptimizedSkeletonHierarchyBlobReference>(true)
                   .With<OptimizedSkeletonState, OptimizedBoneTransform>(false)
                   .With<OptimizedBoneInertialBlendState>(               false);
        }

        public void CreateForApi(ref SystemState state)
        {
            hierarchyHandle     = state.GetComponentTypeHandle<OptimizedSkeletonHierarchyBlobReference>(true);
            stateHandle         = state.GetComponentTypeHandle<OptimizedSkeletonState>(false);
            boneTransformHandle = state.GetBufferTypeHandle<OptimizedBoneTransform>(false);
            blendStateHandle    = state.GetBufferTypeHandle<OptimizedBoneInertialBlendState>(false);
#if !LATIOS_TRANSFORMS_UNITY
            skinnedMeshesHandle = state.GetBufferTypeHandle<DependentSkinnedMesh>(true);
            socketLookup        = state.GetComponentLookup<Socket>(true);
#endif
        }

        public OptimizedSkeletonInputsAspect GetParameter(in JobContext context)
        {
            CheckEntityStart();
            ref var cache        = ref threadCache.cache;
            int     indexInChunk = context.indexInChunk;
            return new OptimizedSkeletonInputsAspect
            {
                m_skeletonHierarchyBlobRef = new RefRO<OptimizedSkeletonHierarchyBlobReference>(cache.chunkHierarchies, indexInChunk),
                m_skeletonState            = new RefRW<OptimizedSkeletonState>(cache.chunkStates, indexInChunk),
                m_boneTransforms           = cache.chunkBoneTransforms[indexInChunk],
                m_bonesInertialBlendStates = cache.chunkBlendStates[indexInChunk],
                m_boneCount                = (short)cache.chunkHierarchies[indexInChunk].blob.Value.parentIndices.Length,
#if !LATIOS_TRANSFORMS_UNITY
                m_optionalDependentSkinnedMeshes = cache.chunkOptionalSkinnedMeshes.Length > 0 ? cache.chunkOptionalSkinnedMeshes[indexInChunk] : default,
                m_socketLookupAccess             = (ComponentLookup<Socket>*)UnsafeUtility.AddressOf(ref socketLookup)
#else
                m_optionalDependentSkinnedMeshes = default,
#endif
            };
        }

        public bool OnChunkBegin(in JobContext context)
        {
            if (!threadCache.isCreated)
                threadCache = new ThreadCache<Cache>(default);
            ref var cache   = ref threadCache.cache;

            var chunk                 = context.chunk;
            cache.chunkHierarchies    = chunk.GetNativeArray(ref hierarchyHandle);
            cache.chunkStates         = chunk.GetNativeArray(ref stateHandle);
            cache.chunkBoneTransforms = chunk.GetBufferAccessor(ref boneTransformHandle);
            cache.chunkBlendStates    = chunk.GetBufferAccessor(ref blendStateHandle);
#if !LATIOS_TRANSFORMS_UNITY
            cache.chunkOptionalSkinnedMeshes = chunk.GetBufferAccessor(ref skinnedMeshesHandle);
#endif

            isChunkInitialized = true;
            return true;
        }

        public void OnChunkEnd(in JobContext context, bool chunkWasExecuted)
        {
            isChunkInitialized = false;
        }

        public void UpdateForApi(ref SystemState state)
        {
            hierarchyHandle.Update(ref state);
            stateHandle.Update(ref state);
            boneTransformHandle.Update(ref state);
            blendStateHandle.Update(ref state);
#if !LATIOS_TRANSFORMS_UNITY
            skinnedMeshesHandle.Update(ref state);
            socketLookup.Update(ref state);
#endif
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS"), Conditional("UNITY_DOTS_DEBUG")]
        void CheckEntityStart()
        {
            if (!isChunkInitialized)
                throw new System.InvalidOperationException("Attempted to run an Entity when the chunk wasn't initialized.");
        }
    }
}

