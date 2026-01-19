using Unity.Entities;

namespace Latios.Transforms
{
    public static partial class TransformTools
    {
        #region Extensions
        /// <summary>
        /// Resolves the EntityInHierarchyHandle for the specified RootReference, allowing for fast hierarchy traversal.
        /// </summary>
        /// <param name="entityManager">The EntityManager which manages the entity this RootReference came from</param>
        /// <returns>An EntityInHierarchyHandle referring to the spot in the hierarchy that the entity this RootReference
        /// belongs to is located</returns>
        public static EntityInHierarchyHandle ToHandle(this RootReference rootRef, EntityManager entityManager)
        {
            return rootRef.ToHandle(ref EntityManagerAccess.From(ref entityManager));
        }

        /// <summary>
        /// Resolves the EntityInHierarchyHandle for the specified RootReference, allowing for fast hierarchy traversal.
        /// </summary>
        /// <param name="componentBroker">A ComponentBroker with read access to EntityInHierarchy and EntityInHierarchyCleanup</param>
        /// <returns>An EntityInHierarchyHandle referring to the spot in the hierarchy that the entity this RootReference
        /// belongs to is located</returns>
        public static EntityInHierarchyHandle ToHandle(this RootReference rootRef, ref ComponentBroker componentBroker)
        {
            return rootRef.ToHandle(ref ComponentBrokerAccess.From(ref componentBroker));
        }

        /// <summary>
        /// Resolves the EntityInHierarchyHandle for the specified RootReference, allowing for fast hierarchy traversal.
        /// </summary>
        /// <param name="entityInHierarchyLookupRO">A readonly BufferLookup to the EntityInHierarchy dynamic buffer</param>
        /// <param name="entityInHierarchyCleanupLookupRO">A readonly BufferLookup to the EntityInHierarchyCleanup dynamic buffer</param>
        /// <returns>An EntityInHierarchyHandle referring to the spot in the hierarchy that the entity this RootReference
        /// belongs to is located</returns>
        public static EntityInHierarchyHandle ToHandle(this RootReference rootRef, ref BufferLookup<EntityInHierarchy> entityInHierarchyLookupRO,
                                                       ref BufferLookup<EntityInHierarchyCleanup> entityInHierarchyCleanupLookupRO)
        {
            ComponentLookup<RootReference> dummy           = default;
            var                            hierarchyAccess = new LookupHierarchy(dummy, entityInHierarchyLookupRO, entityInHierarchyCleanupLookupRO);
            var                            result          = rootRef.ToHandle(ref hierarchyAccess);
            hierarchyAccess.WriteBack(ref dummy, ref entityInHierarchyLookupRO, ref entityInHierarchyCleanupLookupRO);
            return result;
        }

        internal static EntityInHierarchyHandle ToHandle<T>(this RootReference rootRef, ref T hierarchyAccess) where T : unmanaged, IHierarchy
        {
            if (!hierarchyAccess.TryGetEntityInHierarchy(rootRef.rootEntity, out var buffer))
            {
                hierarchyAccess.TryGetEntityInHierarchyCleanup(rootRef.rootEntity, out var temp);
                buffer = temp.Reinterpret<EntityInHierarchy>();
            }
            return new EntityInHierarchyHandle
            {
                m_hierarchy = buffer.AsNativeArray(),
                m_index     = rootRef.indexInHierarchy
            };
        }

        /// <summary>
        /// Gets the root EntityInHierarchyHandle for the given buffer.
        /// </summary>
        public static EntityInHierarchyHandle GetRootHandle(this DynamicBuffer<EntityInHierarchy> entityInHierarchyBuffer)
        {
            return new EntityInHierarchyHandle
            {
                m_hierarchy = entityInHierarchyBuffer.AsNativeArray(),
                m_index     = 0
            };
        }

        /// <summary>
        /// Gets the root EntityInHierarchyHandle for the given buffer.
        /// </summary>
        public static EntityInHierarchyHandle GetRootHandle(this DynamicBuffer<EntityInHierarchyCleanup> entityInHierarchyBuffer)
        {
            return new EntityInHierarchyHandle
            {
                m_hierarchy = entityInHierarchyBuffer.AsNativeArray().Reinterpret<EntityInHierarchy>(),
                m_index     = 0
            };
        }
        #endregion

        #region Internal
        internal static EntityInHierarchyHandle GetHierarchyHandle(Entity entity, EntityManager entityManager)
        {
            if (entityManager.HasComponent<RootReference>(entity))
            {
                var rootRef = entityManager.GetComponentData<RootReference>(entity);
                return rootRef.ToHandle(entityManager);
            }
            if (entityManager.HasBuffer<EntityInHierarchy>(entity))
                return entityManager.GetBuffer<EntityInHierarchy>(entity).GetRootHandle();
            if (entityManager.HasBuffer<EntityInHierarchyCleanup>(entity))
                return entityManager.GetBuffer<EntityInHierarchyCleanup>(entity).GetRootHandle();
            return default;
        }

        internal static EntityInHierarchyHandle GetHierarchyHandle(Entity entity, ref ComponentBroker broker)
        {
            var rootRefRO = broker.GetRO<RootReference>(entity);
            if (rootRefRO.IsValid)
            {
                return rootRefRO.ValueRO.ToHandle(ref broker);
            }
            var buffer = broker.GetBuffer<EntityInHierarchy>(entity);
            if (buffer.IsCreated)
                return buffer.GetRootHandle();
            var cleanup = broker.GetBuffer<EntityInHierarchyCleanup>(entity);
            if (cleanup.IsCreated)
                return cleanup.GetRootHandle();
            return default;
        }

        internal static EntityInHierarchyHandle GetHierarchyHandle(Entity entity,
                                                                   ref ComponentLookup<RootReference>         rootReferenceLookupRO,
                                                                   ref BufferLookup<EntityInHierarchy>        entityInHierarchyLookupRO,
                                                                   ref BufferLookup<EntityInHierarchyCleanup> entityInHierarchyCleanupLookupRO)
        {
            if (rootReferenceLookupRO.TryGetComponent(entity, out var rootRef))
                return rootRef.ToHandle(ref entityInHierarchyLookupRO, ref entityInHierarchyCleanupLookupRO);
            if (entityInHierarchyLookupRO.TryGetBuffer(entity, out var buffer))
                return buffer.GetRootHandle();
            if (entityInHierarchyCleanupLookupRO.TryGetBuffer(entity, out var cleanup))
                return cleanup.GetRootHandle();
            return default;
        }
        #endregion
    }
}

