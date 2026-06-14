#if !LATIOS_TRANSFORMS_UNITY
using System.Diagnostics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Entities.Exposed;
using Unity.Mathematics;

namespace Latios.Transforms
{
    /// <summary>
    /// A struct which should be a field of a single-threaded job. It can provide TickedTransformAspect instances for the context of such a job.
    /// </summary>
    public unsafe struct TickedTransformAspectLookup
    {
        /* Construct Snippet
           new TickedTransformAspectLookup(SystemAPI.GetComponentLookup<TickedWorldTransform>(false),
                                  SystemAPI.GetComponentLookup<RootReference>(true),
                                  SystemAPI.GetBufferLookup<EntityInHierarchy>(true),
                                  SystemAPI.GetBufferLookup<EntityInHierarchyCleanup>(true),
                                  SystemAPI.GetEntityStorageInfoLookup())
         */
        ComponentLookup<TickedWorldTransform>             transformLookup;
        [ReadOnly] ComponentLookup<RootReference>         rootRefLookup;
        [ReadOnly] BufferLookup<EntityInHierarchy>        eihLookup;
        [ReadOnly] BufferLookup<EntityInHierarchyCleanup> cleanupLookup;
        [ReadOnly] EntityStorageInfoLookup                esil;

        public TickedTransformAspectLookup(ComponentLookup<TickedWorldTransform>  tickedWorldTransformLookupRW,
                                           ComponentLookup<RootReference>         rootReferenceLookupRO,
                                           BufferLookup<EntityInHierarchy>        entityInHierarchyLookupRO,
                                           BufferLookup<EntityInHierarchyCleanup> entityInHierarchyCleanupRO,
                                           EntityStorageInfoLookup entityStorageInfoLookup)
        {
            transformLookup = tickedWorldTransformLookupRW;
            rootRefLookup   = rootReferenceLookupRO;
            eihLookup       = entityInHierarchyLookupRO;
            cleanupLookup   = entityInHierarchyCleanupRO;
            esil            = entityStorageInfoLookup;
        }

        /// <summary>
        /// Retrieves a TickedTransformAspect corresponding to the EntityInHierarchyHandle
        /// </summary>
        public TickedTransformAspect this[EntityInHierarchyHandle handle] => new TickedTransformAspect
        {
            m_worldTransform = transformLookup.GetRefRW(handle.entity),
            m_handle         = handle,
            m_esil           = esil,
            m_accessType     = TickedTransformAspect.AccessType.ComponentLookup,
            m_access         = UnsafeUtility.AddressOf(ref transformLookup)
        };

        /// <summary>
        /// Retrieves a TickedTransformAspect from the entity
        /// </summary>
        public TickedTransformAspect this[Entity entity]
        {
            get
            {
                var tickedWorldTransform = transformLookup.GetRefRW(entity);
                var handle               = TransformTools.GetHierarchyHandle(entity, ref rootRefLookup, ref eihLookup, ref cleanupLookup);
                if (handle.isNull)
                    return new TickedTransformAspect { m_worldTransform = tickedWorldTransform, m_handle = handle, };
                else
                {
                    return new TickedTransformAspect
                    {
                        m_worldTransform = tickedWorldTransform,
                        m_handle         = handle,
                        m_esil           = esil,
                        m_accessType     = TickedTransformAspect.AccessType.ComponentLookup,
                        m_access         = UnsafeUtility.AddressOf(ref transformLookup),
                    };
                }
            }
        }

        /// <summary>
        /// Access to the internal EntityStorageInfoLookup for convenience
        /// </summary>
        public EntityStorageInfoLookup entityStorageInfoLookup => esil;
        /// <summary>
        /// Tries to look up a TickedWorldTransform with read-only access
        /// </summary>
        public bool TryGetTickedWorldTransformRO(Entity entity, out RefRO<TickedWorldTransform> tickedWorldTransform) => transformLookup.TryGetRefRO(entity,
                                                                                                                                                     out tickedWorldTransform);
    }

    /// <summary>
    /// A struct which should be a field of a parallel IJobChunk, IJobEntityChunkBeginEnd, or equivalent.
    /// It can provide TickedTransformAspect for any root or solo entities with thread-safe guarantees.
    /// For each chunk, call SetupChunk(). Then use the indexer with the index of the entity within the chunk to get the TickedTransformAspect.
    /// If used in an IJobEntity, make sure to include TickedWorldTransform in your query!
    /// </summary>
    public unsafe struct TickedTransformAspectRootHandle
    {
        /* Construct Snippet
           new TickedTransformAspectRootHandle(SystemAPI.GetComponentLookup<TickedWorldTransform>(false),
                                      SystemAPI.GetBufferTypeHandle<EntityInHierarchy>(true),
                                      SystemAPI.GetBufferTypeHandle<EntityInHierarchyCleanup>(true),
                                      SystemAPI.GetEntityStorageInfoLookup())
         */

        struct ThreadCache
        {
            public ComponentTypeHandle<TickedWorldTransform> transformHandle;
            public NativeArray<TickedWorldTransform>         chunkTransforms;
            public BufferAccessor<EntityInHierarchy>         entityInHierarchyAccessor;
            public BufferAccessor<EntityInHierarchyCleanup>  entityInHierarchyCleanupAccessor;
            public int                                       chunkIndex;
        }

        TransformsComponentLookup<TickedWorldTransform>       transformLookup;
        [ReadOnly] BufferTypeHandle<EntityInHierarchy>        hierarchyHandle;
        [ReadOnly] BufferTypeHandle<EntityInHierarchyCleanup> cleanupHandle;
        [ReadOnly] EntityStorageInfoLookup                    esil;
        [NativeDisableUnsafePtrRestriction] ThreadCache*      cache;
        HasChecker<RootReference>                             rootRefChecker;

        public TickedTransformAspectRootHandle(ComponentLookup<TickedWorldTransform>      tickedWorldTransformLookupRW,
                                               BufferTypeHandle<EntityInHierarchy>        entityInHierarchyHandleRO,
                                               BufferTypeHandle<EntityInHierarchyCleanup> entityInHierarchyCleanupHandleRO,
                                               EntityStorageInfoLookup entityStorageInfoLookup)
        {
            transformLookup = tickedWorldTransformLookupRW;
            hierarchyHandle = entityInHierarchyHandleRO;
            cleanupHandle   = entityInHierarchyCleanupHandleRO;
            esil            = entityStorageInfoLookup;
            cache           = null;
            rootRefChecker  = default;
        }

        /// <summary>
        /// Sets up a chunk for proper access. You must call this once for each chunk you iterate.
        /// If you jump between chunks, you must call this every time you switch. For IJobEntity,
        /// use the IJobEntityChunkBeginEnd interface to invoke this.
        /// </summary>
        /// <param name="chunk"></param>
        public void SetupChunk(in ArchetypeChunk chunk)
        {
            CheckIsRoot(in chunk);
            if (cache == null)
            {
                cache                  = AllocatorManager.Allocate<ThreadCache>(Allocator.Temp);
                cache->transformHandle = transformLookup.lookup.ToHandle(false);
            }
            cache->chunkIndex                       = chunk.GetHashCode();
            cache->chunkTransforms                  = chunk.GetNativeArray(ref cache->transformHandle);
            cache->entityInHierarchyAccessor        = chunk.GetBufferAccessorRO(ref hierarchyHandle);
            cache->entityInHierarchyCleanupAccessor = chunk.GetBufferAccessorRO(ref cleanupHandle);
        }

        /// <summary>
        /// Retrieves the TickedTransformAspect for the corresponding entity index within the current chunk
        /// </summary>
        public TickedTransformAspect this[int indexInChunk]
        {
            get
            {
                CheckInit();
                var transform = new RefRW<TickedWorldTransform>(cache->chunkTransforms, indexInChunk);
                if (cache->entityInHierarchyAccessor.Length == 0)
                {
                    return new TickedTransformAspect
                    {
                        m_worldTransform = transform,
                        m_handle         = default
                    };
                }
                else
                {
                    var extra  = cache->entityInHierarchyCleanupAccessor.Length > 0 ? cache->entityInHierarchyCleanupAccessor[indexInChunk].GetUnsafeReadOnlyPtr() : null;
                    var handle = new EntityInHierarchyHandle
                    {
                        m_hierarchy      = cache->entityInHierarchyAccessor[indexInChunk].AsNativeArray(),
                        m_extraHierarchy = (EntityInHierarchy*)extra,
                        m_index          = 0
                    };
                    return new TickedTransformAspect
                    {
                        m_worldTransform = transform,
                        m_handle         = handle,
                        m_esil           = esil,
                        m_accessType     = TickedTransformAspect.AccessType.ComponentLookup,
                        m_access         = UnsafeUtility.AddressOf(ref transformLookup)
                    };
                }
            }
        }

        /// <summary>
        /// Access to the TransformsKey for the current chunk
        /// </summary>
        public TransformsKey transformsKey
        {
            get
            {
                CheckInit();
                return new TransformsKey
                {
                    chunkIndex  = cache->chunkIndex,
                    entityIndex = -1,
                    esil        = esil,
                };
            }
        }

        /// <summary>
        /// Access to the internal EntityStorageInfoLookup for convenience
        /// </summary>
        public EntityStorageInfoLookup entityStorageInfoLookup => esil;

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        void CheckInit()
        {
            if (cache == null)
                throw new System.InvalidOperationException(
                    "The TransformAccessRootHandle has not been set up. Use IJobEntityChunkBeginEnd or IJobChunk to pass in the current chunk to SetupChunk().");
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        void CheckIsRoot(in ArchetypeChunk chunk)
        {
            if (rootRefChecker[chunk])
                throw new System.InvalidOperationException("Cannot set up a TransformAccessRootHandle for a chunk containing non-root entities.");
        }
    }

    public static class TickedTransformAspectAccessExtensions
    {
        /// <summary>
        /// Gets the TickedTransformAspect of the handle powered by the system's EntityManager.
        /// </summary>
        public static unsafe TickedTransformAspect GetTickedTransfromAspect(this EntityManager em, EntityInHierarchyHandle handle)
        {
            var tickedWorldTransform = em.GetComponentDataRW<TickedWorldTransform>(handle.entity);
            return new TickedTransformAspect
            {
                m_worldTransform = tickedWorldTransform,
                m_handle         = handle,
                m_esil           = em.GetEntityStorageInfoLookup(),
                m_accessType     = TickedTransformAspect.AccessType.EntityManager,
                m_access         = em.GetEntityManagerPtr()
            };
        }

        /// <summary>
        /// Gets the TickedTransformAspect of the entity powered by the system's EntityManager.
        /// </summary>
        public static unsafe TickedTransformAspect GetTickedTransfromAspect(this EntityManager em, Entity entity)
        {
            var tickedWorldTransform = em.GetComponentDataRW<TickedWorldTransform>(entity);
            var handle               = TransformTools.GetHierarchyHandle(entity, em);
            if (handle.isNull)
                return new TickedTransformAspect { m_worldTransform = tickedWorldTransform, m_handle = handle, };
            else
            {
                return new TickedTransformAspect
                {
                    m_worldTransform = tickedWorldTransform,
                    m_handle         = handle,
                    m_esil           = em.GetEntityStorageInfoLookup(),
                    m_accessType     = TickedTransformAspect.AccessType.EntityManager,
                    m_access         = em.GetEntityManagerPtr()
                };
            }
        }

        /// <summary>
        /// Gets the TickedTransformAspect of the handle powered by a ComponentBroker. The ComponentBroker
        /// must have a fixed address for the lifecycle of the TickedTransformAspect, such as a field in a
        /// currently executing job. The ComponentBroker requires write access to TickedWorldTransform, and
        /// read access to RootReference, EntityInHierarchy, and EntityInHierarchyCleanup.
        /// </summary>
        public static unsafe TickedTransformAspect GetTickedTransformAspect(this ref ComponentBroker broker, EntityInHierarchyHandle handle)
        {
            var tickedWorldTransform = broker.GetRW<TickedWorldTransform>(handle.entity);
            return new TickedTransformAspect
            {
                m_worldTransform = tickedWorldTransform,
                m_handle         = handle,
                m_esil           = broker.entityStorageInfoLookup,
                m_accessType     = TickedTransformAspect.AccessType.ComponentBroker,
                m_access         = UnsafeUtility.AddressOf(ref broker)
            };
        }

        /// <summary>
        /// Gets the TickedTransformAspect of the entity powered by a ComponentBroker. The ComponentBroker
        /// must have a fixed address for the lifecycle of the TickedTransformAspect, such as a field in a
        /// currently executing job. The ComponentBroker requires write access to TickedWorldTransform, and
        /// read access to RootReference, EntityInHierarchy, and EntityInHierarchyCleanup.
        /// </summary>
        public static unsafe TickedTransformAspect GetTickedTransformAspect(this ref ComponentBroker broker, Entity entity)
        {
            var tickedWorldTransform = broker.GetRW<TickedWorldTransform>(entity);
            var handle               = TransformTools.GetHierarchyHandle(entity, ref broker);
            if (handle.isNull)
                return new TickedTransformAspect { m_worldTransform = tickedWorldTransform, m_handle = handle, };
            else
            {
                return new TickedTransformAspect
                {
                    m_worldTransform = tickedWorldTransform,
                    m_handle         = handle,
                    m_esil           = broker.entityStorageInfoLookup,
                    m_accessType     = TickedTransformAspect.AccessType.ComponentBroker,
                    m_access         = UnsafeUtility.AddressOf(ref broker)
                };
            }
        }

        /// <summary>
        /// Gets the TickedTransformAspect of the handle powered by a ComponentBroker. The ComponentBroker
        /// must have a fixed address for the lifecycle of the TickedTransformAspect, such as a field in a
        /// currently executing job. The ComponentBroker requires write access to TickedWorldTransform, and
        /// read access to RootReference, EntityInHierarchy, and EntityInHierarchyCleanup. The aspect
        /// is verified for parallel writing by the key.
        /// </summary>
        public static unsafe TickedTransformAspect GetTickedTransformAspect(this ref ComponentBroker broker, EntityInHierarchyHandle handle, TransformsKey key)
        {
            key.Validate(handle.root.entity);
            var tickedWorldTransform = broker.GetRWIgnoreParallelSafety<TickedWorldTransform>(handle.entity);
            return new TickedTransformAspect
            {
                m_worldTransform = tickedWorldTransform,
                m_handle         = handle,
                m_esil           = broker.entityStorageInfoLookup,
                m_accessType     = TickedTransformAspect.AccessType.ComponentBrokerKeyed,
                m_access         = UnsafeUtility.AddressOf(ref broker)
            };
        }

        /// <summary>
        /// Gets the TickedTransformAspect of the entity powered by a ComponentBroker. The ComponentBroker
        /// must have a fixed address for the lifecycle of the TickedTransformAspect, such as a field in a
        /// currently executing job. The ComponentBroker requires write access to TickedWorldTransform, and
        /// read access to RootReference, EntityInHierarchy, and EntityInHierarchyCleanup. The aspect
        /// is verified for parallel writing by the key.
        /// </summary>
        public static unsafe TickedTransformAspect GetTickedTransformAspect(this ref ComponentBroker broker, Entity entity, TransformsKey key)
        {
            var tickedWorldTransform = broker.GetRWIgnoreParallelSafety<TickedWorldTransform>(entity);
            var handle               = TransformTools.GetHierarchyHandle(entity, ref broker);
            if (handle.isNull)
            {
                key.Validate(entity);
                return new TickedTransformAspect { m_worldTransform = tickedWorldTransform, m_handle = handle, };
            }
            else
            {
                key.Validate(handle.root.entity);
                return new TickedTransformAspect
                {
                    m_worldTransform = tickedWorldTransform,
                    m_handle         = handle,
                    m_esil           = broker.entityStorageInfoLookup,
                    m_accessType     = TickedTransformAspect.AccessType.ComponentBrokerKeyed,
                    m_access         = UnsafeUtility.AddressOf(ref broker)
                };
            }
        }
    }
}
#endif

