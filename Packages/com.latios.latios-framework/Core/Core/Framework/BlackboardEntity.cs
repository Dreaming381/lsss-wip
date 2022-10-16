using Unity.Entities;
using Unity.Jobs;

namespace Latios
{
    /// <summary>
    /// An entity and its associated EntityManager, which provides shorthands for manipulating the entity's components
    /// </summary>
    public unsafe struct BlackboardEntity
    {
        private Entity               entity;
        private LatiosWorldUnmanaged latiosWorld;
        private EntityManager em => latiosWorld.m_impl->m_worldUnmanaged.EntityManager;

        /// <summary>
        /// Create a blackboard entity
        /// </summary>
        /// <param name="entity">The existing entity to use</param>
        /// <param name="entityManager">The entity's associated EntityManager</param>
        public BlackboardEntity(Entity entity, LatiosWorldUnmanaged latiosWorld)
        {
            this.entity      = entity;
            this.latiosWorld = latiosWorld;
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
            return em.AddSharedComponent(entity, data);
        }

        public void SetSharedComponentData<T>(T data) where T : unmanaged, ISharedComponentData
        {
            em.SetSharedComponent(entity, data);
        }

        public T GetSharedComponentData<T>() where T : unmanaged, ISharedComponentData
        {
            return em.GetSharedComponent<T>(entity);
        }

        public DynamicBuffer<T> AddBuffer<T>() where T : unmanaged, IBufferElementData
        {
            return em.AddBuffer<T>(entity);
        }

        public DynamicBuffer<T> GetBuffer<T>(bool readOnly = false) where T : unmanaged, IBufferElementData
        {
            return em.GetBuffer<T>(entity, readOnly);
        }

        /// <summary>
        /// Adds a managed struct component to the entity. This implicitly adds the managed struct component's AssociatedComponentType as well.
        /// If the entity already contains the managed struct component, the managed struct component will be overwritten with the new value.
        /// </summary>
        /// <typeparam name="T">The struct type implementing IManagedComponent</typeparam>
        /// <param name="component">The data for the managed struct component</param>
        /// <returns>False if the component was already present, true otherwise</returns>
        public bool AddManagedStructComponent<T>(T component) where T : struct, IManagedStructComponent
        {
            return latiosWorld.AddManagedStructComponent(entity, component);
        }

        /// <summary>
        /// Removes a managed struct component from the entity. This implicitly removes the managed struct component's AssociatedComponentType as well.
        /// </summary>
        /// <typeparam name="T">The struct type implementing IManagedComponent</typeparam>
        /// <returns>Returns true if the entity had the managed struct component, false otherwise</returns>
        public bool RemoveManagedStructComponent<T>() where T : struct, IManagedStructComponent
        {
            return latiosWorld.RemoveManagedStructComponent<T>(entity);
        }

        /// <summary>
        /// Gets the managed struct component instance from the entity
        /// </summary>
        /// <typeparam name="T">The struct type implementing IManagedComponent</typeparam>
        public T GetManagedStructComponent<T>() where T : struct, IManagedStructComponent
        {
            return latiosWorld.GetManagedStructComponent<T>(entity);
        }

        /// <summary>
        /// Sets the managed struct component instance for the entity.
        /// Throws if the entity does not have the managed struct component
        /// </summary>
        /// <typeparam name="T">The struct type implementing IManagedComponent</typeparam>
        /// <param name="component">The new managed struct component value</param>
        public void SetManagedStructComponent<T>(T component) where T : struct, IManagedStructComponent
        {
            latiosWorld.SetManagedStructComponent(entity, component);
        }

        /// <summary>
        /// Returns true if the entity has the managed struct component. False otherwise.
        /// </summary>
        /// <typeparam name="T">The struct type implementing IManagedComponent</typeparam>
        public bool HasManagedStructComponent<T>() where T : struct, IManagedStructComponent
        {
            return latiosWorld.HasManagedStructComponent<T>(entity);
        }

        public void AddOrSetCollectionComponentAndDisposeOld<T>(T value) where T : unmanaged, ICollectionComponent
        {
            latiosWorld.AddOrSetCollectionComponentAndDisposeOld(entity, value);
        }

        public T GetCollectionComponent<T>(bool readOnly = false) where T : unmanaged, ICollectionComponent
        {
            return latiosWorld.GetCollectionComponent<T>(entity, readOnly);
        }

        public bool HasCollectionComponent<T>() where T : unmanaged, ICollectionComponent
        {
            return latiosWorld.HasCollectionComponent<T>(entity);
        }

        public void RemoveCollectionComponentAndDispose<T>() where T : unmanaged, ICollectionComponent
        {
            latiosWorld.RemoveCollectionComponentAndDispose<T>(entity);
        }

        public void SetCollectionComponentAndDisposeOld<T>(T value) where T : unmanaged, ICollectionComponent
        {
            latiosWorld.SetCollectionComponentAndDisposeOld(entity, value);
        }

        public void UpdateJobDependency<T>(JobHandle handle, bool wasReadOnly) where T : unmanaged, ICollectionComponent
        {
            latiosWorld.UpdateCollectionComponentDependency<T>(entity, handle, wasReadOnly);
        }
    }
}

