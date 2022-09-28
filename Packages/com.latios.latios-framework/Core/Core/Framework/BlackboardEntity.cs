using Unity.Entities;
using Unity.Jobs;

namespace Latios
{
    /// <summary>
    /// An entity and its associated EntityManager, which provides shorthands for manipulating the entity's components
    /// </summary>
    public struct BlackboardEntity
    {
        private Entity        entity;
        private EntityManager em;

        /// <summary>
        /// Create a blackboard entity
        /// </summary>
        /// <param name="entity">The existing entity to use</param>
        /// <param name="entityManager">The entity's associated EntityManager</param>
        public BlackboardEntity(Entity entity, EntityManager entityManager)
        {
            this.entity = entity;
            em          = entityManager;
        }

        /// <summary>
        /// Implicitly fetch the entity of the BlackboardEntity
        /// </summary>
        public static implicit operator Entity(BlackboardEntity entity)
        {
            return entity.entity;
        }

        public bool AddComponent<T>() where T : unmanaged, IComponentData
        {
            return em.AddComponent<T>(entity);
        }

        public bool AddComponentData<T>(T data) where T : unmanaged, IComponentData
        {
            return em.AddComponentData(entity, data);
        }

        public bool AddComponentDataIfMissing<T>(T data) where T : unmanaged, IComponentData
        {
            if (em.HasComponent<T>(entity))
                return false;
            em.AddComponentData(entity, data);
            return true;
        }

        public void SetComponentData<T>(T data) where T : unmanaged, IComponentData
        {
            em.SetComponentData(entity, data);
        }

        public T GetComponentData<T>() where T : unmanaged, IComponentData
        {
            return em.GetComponentData<T>(entity);
        }

        public bool HasComponent<T>() where T : unmanaged, IComponentData
        {
            return em.HasComponent<T>(entity);
        }

        public bool HasComponent(ComponentType componentType)
        {
            return em.HasComponent(entity, componentType);
        }

        public bool AddSharedComponentData<T>(T data) where T : unmanaged, ISharedComponentData
        {
            return em.AddSharedComponentManaged(entity, data);
        }

        public void SetSharedComponentData<T>(T data) where T : unmanaged, ISharedComponentData
        {
            em.SetSharedComponentManaged(entity, data);
        }

        public T GetSharedComponentData<T>() where T : unmanaged, ISharedComponentData
        {
            return em.GetSharedComponentManaged<T>(entity);
        }

        public DynamicBuffer<T> AddBuffer<T>() where T : unmanaged, IBufferElementData
        {
            return em.AddBuffer<T>(entity);
        }

        public DynamicBuffer<T> GetBuffer<T>(bool readOnly = false) where T : unmanaged, IBufferElementData
        {
            return em.GetBuffer<T>(entity, readOnly);
        }

        public void AddCollectionComponent<T>(T value, bool isInitialized = true) where T : struct, ICollectionComponent
        {
            em.AddCollectionComponent(entity, value, isInitialized);
        }

        public T GetCollectionComponent<T>(bool readOnly, out JobHandle handle) where T : struct, ICollectionComponent
        {
            return em.GetCollectionComponent<T>(entity, readOnly, out handle);
        }

        public T GetCollectionComponent<T>(bool readOnly = false) where T : struct, ICollectionComponent
        {
            return em.GetCollectionComponent<T>(entity, readOnly);
        }

        public bool HasCollectionComponent<T>() where T : struct, ICollectionComponent
        {
            return em.HasCollectionComponent<T>(entity);
        }

        public void RemoveCollectionComponentAndDispose<T>() where T : struct, ICollectionComponent
        {
            em.RemoveCollectionComponentAndDispose<T>(entity);
        }

        public void SetCollectionComponentAndDisposeOld<T>(T value) where T : struct, ICollectionComponent
        {
            em.SetCollectionComponentAndDisposeOld(entity, value);
        }

        public void UpdateJobDependency<T>(JobHandle handle, bool wasReadOnly) where T : struct, ICollectionComponent
        {
            em.UpdateCollectionComponentDependency<T>(entity, handle, wasReadOnly);
        }
    }
}

