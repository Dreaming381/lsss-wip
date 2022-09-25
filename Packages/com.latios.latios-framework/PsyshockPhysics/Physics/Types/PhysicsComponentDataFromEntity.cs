using System;
using System.Diagnostics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;

namespace Latios.Psyshock
{
    //Specifying this as a NativeContainer prevents this value from being stored in a NativeContainer.
    /// <summary>
    /// A struct representing an Entity that can potentially index a PhysicsComponentDataFromEntity safely in parallel,
    /// or will throw an error when safety checks are enabled and the safety cannot be guaranteed.
    /// This type can be implicitly converted to the Entity type.
    /// </summary>
    [NativeContainer]
    public struct SafeEntity
    {
        internal Entity entity;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        public static implicit operator Entity(SafeEntity e) => new Entity
        {
            Index = math.abs(e.entity.Index), Version = e.entity.Version
        };
#else
        public static implicit operator Entity(SafeEntity e) => e.entity;
#endif
    }

    /// <summary>
    /// A struct which wraps ComponentDataFromEntity<typeparamref name="T"/> and allows for performing
    /// Read-Write access in parallel using SafeEntity types when it is guaranteed safe to do so.
    /// You can implicitly cast a ComponentDataFromEntity<typeparamref name="T"/> to this type.
    /// </summary>
    /// <typeparam name="T">A type implementing IComponentData</typeparam>
    public struct PhysicsComponentDataFromEntity<T> where T : struct, IComponentData
    {
        [NativeDisableParallelForRestriction]
        internal ComponentDataFromEntity<T> cdfe;

        /// <summary>
        /// Reads or writes the component on the entity represented by safeEntity.
        /// When safety checks are enabled, this throws when parallel safety cannot
        /// be guaranteed.
        /// </summary>
        /// <param name="safeEntity">A safeEntity representing an entity that may be safe to access</param>
        /// <returns></returns>
        public T this[SafeEntity safeEntity]
        {
            get
            {
                ValidateSafeEntityIsSafe(safeEntity);
                return cdfe[safeEntity.entity];
            }
            set
            {
                ValidateSafeEntityIsSafe(safeEntity);
                cdfe[safeEntity.entity] = value;
            }
        }

        /// <summary>
        /// Fetches the component on the entity represented by safeEntity if the entity has the component.
        /// When safety checks are enabled, this throws when parallel safety cannot
        /// be guaranteed.
        /// </summary>
        /// <param name="safeEntity">A safeEntity representing an entity that may be safe to access</param>
        /// <param name="componentData">The fetched component</param>
        /// <returns>True if the entity had the component</returns>
        public bool TryGetComponent(SafeEntity safeEntity, out T componentData)
        {
            ValidateSafeEntityIsSafe(safeEntity);
            return cdfe.TryGetComponent(safeEntity, out componentData);
        }

        /// <summary>
        /// Checks if the entity represented by SafeEntity has the component specified.
        /// This check is always valid regardless of whether such a component would be
        /// safe to access.
        /// </summary>
        public bool HasComponent(SafeEntity safeEntity) => cdfe.HasComponent(safeEntity);

        /// <summary>
        /// This is identical to ComponentDataFromEntity<typeparamref name="T"/>.DidChange().
        /// Note that neither method is deterministic and both can be prone to race conditions.
        /// </summary>
        public bool DidChange(SafeEntity safeEntity, uint version) => cdfe.DidChange(safeEntity, version);

        public static implicit operator PhysicsComponentDataFromEntity<T>(ComponentDataFromEntity<T> componentDataFromEntity)
        {
            return new PhysicsComponentDataFromEntity<T> { cdfe = componentDataFromEntity };
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        static void ValidateSafeEntityIsSafe(SafeEntity safeEntity)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (safeEntity.entity.Index < 0)
            {
                throw new InvalidOperationException("PhysicsComponentDataFromEntity cannot be used inside a RunImmediate context. Use ComponentDataFromEntity instead.");
            }
#endif
        }
    }

    /// <summary>
    /// A struct which wraps BufferFromEntity<typeparamref name="T"/> and allows for performing
    /// Read-Write access in parallel using SafeEntity types when it is guaranteed safe to do so.
    /// You can implicitly cast a BufferFromEntity<typeparamref name="T"/> to this type.
    /// </summary>
    /// <typeparam name="T">A type implementing IComponentData</typeparam>
    public struct PhysicsBufferFromEntity<T> where T : struct, IBufferElementData
    {
        [NativeDisableParallelForRestriction]
        internal BufferFromEntity<T> bfe;

        /// <summary>
        /// Gets a reference to the buffer on the entity represented by safeEntity.
        /// When safety checks are enabled, this throws when parallel safety cannot
        /// be guaranteed.
        /// </summary>
        /// <param name="safeEntity">A safeEntity representing an entity that may be safe to access</param>
        /// <returns></returns>
        public DynamicBuffer<T> this[SafeEntity safeEntity]
        {
            get
            {
                ValidateSafeEntityIsSafe(safeEntity);
                return bfe[safeEntity.entity];
            }
        }

        /// <summary>
        /// Fetches the buffer on the entity represented by safeEntity if the entity has the buffer type.
        /// When safety checks are enabled, this throws when parallel safety cannot
        /// be guaranteed.
        /// </summary>
        /// <param name="safeEntity">A safeEntity representing an entity that may be safe to access</param>
        /// <param name="bufferData">The fetched buffer</param>
        /// <returns>True if the entity had the buffer type</returns>
        public bool TryGetComponent(SafeEntity safeEntity, out DynamicBuffer<T> bufferData)
        {
            ValidateSafeEntityIsSafe(safeEntity);
            return bfe.TryGetBuffer(safeEntity, out bufferData);
        }

        /// <summary>
        /// Checks if the entity represented by SafeEntity has the buffer type specified.
        /// This check is always valid regardless of whether such a buffer would be
        /// safe to access.
        /// </summary>
        public bool HasComponent(SafeEntity safeEntity) => bfe.HasComponent(safeEntity.entity);

        /// <summary>
        /// This is identical to BufferFromEntity<typeparamref name="T"/>.DidChange().
        /// Note that neither method is deterministic and both can be prone to race conditions.
        /// </summary>
        public bool DidChange(SafeEntity safeEntity, uint version) => bfe.DidChange(safeEntity, version);

        public static implicit operator PhysicsBufferFromEntity<T>(BufferFromEntity<T> bufferFromEntity)
        {
            return new PhysicsBufferFromEntity<T> { bfe = bufferFromEntity };
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        static void ValidateSafeEntityIsSafe(SafeEntity safeEntity)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (safeEntity.entity.Index < 0)
            {
                throw new InvalidOperationException("PhysicsBufferFromEntity cannot be used inside a RunImmediate context. Use BufferFromEntity instead.");
            }
#endif
        }
    }
}

