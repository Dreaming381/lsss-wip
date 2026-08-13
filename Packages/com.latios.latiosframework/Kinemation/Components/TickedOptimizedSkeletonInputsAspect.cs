#if !LATIOS_TRANSFORMS_UNITY
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
    /// The inputs needed to create a TickedOptimizedSkeletonAspect except for the entity's transform.
    /// Combine this with a TickedTransformAspect via ToTickedOptimizedSkeletonAspect() to obtain the full
    /// TickedOptimizedSkeletonAspect.
    /// </summary>
    [IJobEach.ParameterHandle(typeof(TickedOptimizedSkeletonInputsAspectParameterHandle), IJobEach.ScheduleModeMask.All)]
    public unsafe struct TickedOptimizedSkeletonInputsAspect : IJobEach.IParameter
    {
        internal RefRO<OptimizedSkeletonHierarchyBlobReference>               m_skeletonHierarchyBlobRef;
        internal RefRW<TickedOptimizedSkeletonState>                          m_skeletonState;
        internal DynamicBuffer<TickedOptimizedBoneTransform>                  m_boneTransforms;
        internal DynamicBuffer<TickedOptimizedBoneInertialBlendState>         m_bonesInertialBlendStates;
        internal DynamicBuffer<DependentSkinnedMesh>                          m_optionalDependentSkinnedMeshes;
        internal short                                                        m_boneCount;
        [NativeDisableUnsafePtrRestriction] internal ComponentLookup<Socket>* m_socketLookupAccess;

        /// <summary>
        /// The number of bones in the skeleton, derived directly from the skeleton's hierarchy blob.
        /// </summary>
        public int boneCount => m_boneCount;

        /// <summary>
        /// Combines this TickedOptimizedSkeletonInputsAspect with the specified TickedTransformAspect to construct
        /// the full TickedOptimizedSkeletonAspect.
        /// </summary>
        public TickedOptimizedSkeletonAspect ToTickedOptimizedSkeletonAspect(TickedTransformAspect transformAspect)
        {
            return new TickedOptimizedSkeletonAspect(transformAspect,
                                                     ref *m_socketLookupAccess,
                                                     m_skeletonHierarchyBlobRef,
                                                     m_skeletonState,
                                                     ref m_boneTransforms,
                                                     ref m_bonesInertialBlendStates,
                                                     m_optionalDependentSkinnedMeshes);
        }
    }

    // WARNING: The following is for internal use in IJobEach only.
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public unsafe struct TickedOptimizedSkeletonInputsAspectParameterHandle : IParameterHandle<TickedOptimizedSkeletonInputsAspect>
    {
        struct Cache
        {
            public NativeArray<OptimizedSkeletonHierarchyBlobReference>  chunkHierarchies;
            public NativeArray<TickedOptimizedSkeletonState>             chunkStates;
            public BufferAccessor<TickedOptimizedBoneTransform>          chunkBoneTransforms;
            public BufferAccessor<TickedOptimizedBoneInertialBlendState> chunkBlendStates;
            public BufferAccessor<DependentSkinnedMesh>                  chunkOptionalSkinnedMeshes;
        }

        [ReadOnly] ComponentTypeHandle<OptimizedSkeletonHierarchyBlobReference> hierarchyHandle;
        ComponentTypeHandle<TickedOptimizedSkeletonState>                       stateHandle;
        BufferTypeHandle<TickedOptimizedBoneTransform>                          boneTransformHandle;
        BufferTypeHandle<TickedOptimizedBoneInertialBlendState>                 blendStateHandle;
        [ReadOnly] BufferTypeHandle<DependentSkinnedMesh>                       skinnedMeshesHandle;
        [ReadOnly] ComponentLookup<Socket>                                      socketLookup;
        ThreadCache<Cache>                                                      threadCache;

        bool isChunkInitialized;

        public FluentQuery AppendToQuery(FluentQuery query)
        {
            return query.With<OptimizedSkeletonHierarchyBlobReference>(true)
                   .With<TickedOptimizedSkeletonState, TickedOptimizedBoneTransform>(false)
                   .With<TickedOptimizedBoneInertialBlendState>(                     false);
        }

        public void CreateForApi(ref SystemState state)
        {
            hierarchyHandle     = state.GetComponentTypeHandle<OptimizedSkeletonHierarchyBlobReference>(true);
            stateHandle         = state.GetComponentTypeHandle<TickedOptimizedSkeletonState>(false);
            boneTransformHandle = state.GetBufferTypeHandle<TickedOptimizedBoneTransform>(false);
            blendStateHandle    = state.GetBufferTypeHandle<TickedOptimizedBoneInertialBlendState>(false);
            skinnedMeshesHandle = state.GetBufferTypeHandle<DependentSkinnedMesh>(true);
            socketLookup        = state.GetComponentLookup<Socket>(true);
        }

        public TickedOptimizedSkeletonInputsAspect GetParameter(in JobContext context)
        {
            CheckEntityStart();
            ref var cache        = ref threadCache.cache;
            int     indexInChunk = context.indexInChunk;
            var     result       = new TickedOptimizedSkeletonInputsAspect
            {
                m_skeletonHierarchyBlobRef       = new RefRO<OptimizedSkeletonHierarchyBlobReference>(cache.chunkHierarchies, indexInChunk),
                m_skeletonState                  = new RefRW<TickedOptimizedSkeletonState>(cache.chunkStates, indexInChunk),
                m_boneTransforms                 = cache.chunkBoneTransforms[indexInChunk],
                m_bonesInertialBlendStates       = cache.chunkBlendStates[indexInChunk],
                m_boneCount                      = (short)cache.chunkHierarchies[indexInChunk].blob.Value.parentIndices.Length,
                m_optionalDependentSkinnedMeshes = cache.chunkOptionalSkinnedMeshes.Length > 0 ? cache.chunkOptionalSkinnedMeshes[indexInChunk] : default,
            };
            result.m_socketLookupAccess = (ComponentLookup<Socket>*)UnsafeUtility.AddressOf(ref socketLookup);
            return result;
        }

        public bool OnChunkBegin(in JobContext context)
        {
            if (!threadCache.isCreated)
                threadCache = new ThreadCache<Cache>(default);
            ref var cache   = ref threadCache.cache;

            var chunk                        = context.chunk;
            cache.chunkHierarchies           = chunk.GetNativeArray(ref hierarchyHandle);
            cache.chunkStates                = chunk.GetNativeArray(ref stateHandle);
            cache.chunkBoneTransforms        = chunk.GetBufferAccessor(ref boneTransformHandle);
            cache.chunkBlendStates           = chunk.GetBufferAccessor(ref blendStateHandle);
            cache.chunkOptionalSkinnedMeshes = chunk.GetBufferAccessor(ref skinnedMeshesHandle);

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
            skinnedMeshesHandle.Update(ref state);
            socketLookup.Update(ref state);
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS"), Conditional("UNITY_DOTS_DEBUG")]
        void CheckEntityStart()
        {
            if (!isChunkInitialized)
                throw new System.InvalidOperationException("Attempted to run an Entity when the chunk wasn't initialized.");
        }
    }
}
#endif

