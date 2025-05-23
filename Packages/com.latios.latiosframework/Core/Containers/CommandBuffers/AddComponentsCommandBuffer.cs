﻿using System.Diagnostics;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;

namespace Latios
{
    public enum AddComponentsDestroyedEntityResolution
    {
        DropData,
        ThrowException,
        AddToNewEntityAndDestroy
    }

    [BurstCompile]
    public struct AddComponentsCommandBuffer : INativeDisposable
    {
        #region Structure
        private EntityOperationCommandBuffer           m_entityOperationCommandBuffer;
        private ComponentTypeSet                       m_typesToAdd;
        private AddComponentsDestroyedEntityResolution m_resolution;
        private NativeReference<bool>                  m_playedBack;
        #endregion

        #region CreateDestroy
        /// <summary>
        /// Create an AddComponentsCommandBuffer which can be used to add default components to entities and play them back later.
        /// </summary>
        /// <param name="allocator">The type of allocator to use for allocating the buffer</param>
        /// <param name="resolution">What to do when an entity no longer exists</param>
        public AddComponentsCommandBuffer(AllocatorManager.AllocatorHandle allocator, AddComponentsDestroyedEntityResolution resolution)
        {
            m_entityOperationCommandBuffer = new EntityOperationCommandBuffer(allocator);
            m_typesToAdd                   = default;
            m_resolution                   = resolution;
            m_playedBack                   = new NativeReference<bool>(allocator);
        }

        /// <summary>
        /// Disposes the AddComponentsCommandBuffer after the jobs which use it have finished.
        /// </summary>
        /// <param name="inputDeps">The JobHandle for any jobs previously using this AddComponentsCommandBuffer</param>
        /// <returns></returns>
        public JobHandle Dispose(JobHandle inputDeps)
        {
            var jh0 = m_entityOperationCommandBuffer.Dispose(inputDeps);
            var jh1 = m_playedBack.Dispose(inputDeps);
            return JobHandle.CombineDependencies(jh0, jh1);
        }

        /// <summary>
        /// Disposes the AddComponentsCommandBuffer
        /// </summary>
        public void Dispose()
        {
            m_entityOperationCommandBuffer.Dispose();
            m_playedBack.Dispose();
        }
        #endregion

        #region PublicAPI
        /// <summary>
        /// Adds an Entity to the AddComponentsCommandBuffer which should be have the components added to
        /// </summary>
        /// <param name="entity">The entity to add components too</param>
        /// <param name="sortKey">The sort key for deterministic playback if interleaving single and parallel writes</param>
        public void Add(Entity entity, int sortKey = int.MaxValue)
        {
            CheckDidNotPlayback();
            m_entityOperationCommandBuffer.Add(entity, sortKey);
        }

        /// <summary>
        /// Plays back the AddComponentsCommandBuffer.
        /// </summary>
        /// <param name="entityManager">The EntityManager with which to play back the AddComponentsCommandBuffer</param>
        public unsafe void Playback(EntityManager entityManager)
        {
            CheckDidNotPlayback();
            Playbacker.Playback((AddComponentsCommandBuffer*)UnsafeUtility.AddressOf(ref this), (EntityManager*)UnsafeUtility.AddressOf(ref entityManager));
        }

        /// <summary>
        /// Get the number of entities stored in this InstantiateCommandBuffer. This method performs a summing operation on every invocation.
        /// </summary>
        /// <returns>The number of elements stored in this InstantiateCommandBuffer</returns>
        public int Count() => m_entityOperationCommandBuffer.Count();

        /// <summary>
        /// Set additional component types to be added to the target entities. These components will be default-initialized.
        /// </summary>
        /// <param name="tags">The types to add to each target entity</param>
        public void SetComponentTags(ComponentTypeSet tags)
        {
            m_typesToAdd = tags;
        }

        /// <summary>
        /// Gets the ParallelWriter for this AddComponentsCommandBuffer.
        /// </summary>
        /// <returns>The ParallelWriter which shares this AddComponentsCommandBuffer's backing storage.</returns>
        public ParallelWriter AsParallelWriter()
        {
            CheckDidNotPlayback();
            return new ParallelWriter(m_entityOperationCommandBuffer);
        }
        #endregion

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        void CheckDidNotPlayback()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (m_playedBack.Value == true)
                throw new System.InvalidOperationException(
                    "The AddComponentsCommandBuffer has already been played back. You cannot write more commands to it or play it back again.");
#endif
        }

        #region PlaybackJobs
        [BurstCompile]
        static class Playbacker
        {
            [BurstCompile]
            public static unsafe void Playback(AddComponentsCommandBuffer* accb, EntityManager* em)
            {
                var targets = accb->m_entityOperationCommandBuffer.GetEntities(Allocator.Temp);
                int dst     = 0;
                for (int src = 0; src < targets.Length; src++)
                {
                    var e = targets[src];
                    if (em->Exists(e))
                    {
                        targets[dst] = e;
                        dst++;
                    }
                    else
                    {
                        if (accb->m_resolution == AddComponentsDestroyedEntityResolution.ThrowException)
                            throw new System.InvalidOperationException($"Entity {e.ToFixedString()} in AddComponentsCommandBuffer has been destroyed.");
                        else if (accb->m_resolution == AddComponentsDestroyedEntityResolution.AddToNewEntityAndDestroy)
                        {
                            e = em->CreateEntity();
                            em->AddComponent(e, accb->m_typesToAdd);
                        }
                    }
                }
                targets.GetSubArray(0, dst);
                em->AddComponent(targets, in accb->m_typesToAdd);
                accb->m_playedBack.Value = true;
            }
        }
        #endregion

        #region ParallelWriter
        /// <summary>
        /// The parallelWriter implementation of AddComponentsCommandBuffer. Use AsParallelWriter to obtain one from an AddComponentsCommandBuffer
        /// </summary>
        public struct ParallelWriter
        {
            private EntityOperationCommandBuffer.ParallelWriter m_entityOperationCommandBuffer;

            internal ParallelWriter(EntityOperationCommandBuffer eocb)
            {
                m_entityOperationCommandBuffer = eocb.AsParallelWriter();
            }

            /// <summary>
            /// Adds an Entity to the AddComponentsCommandBuffer which should have the components added
            /// </summary>
            /// <param name="entity">The entity to have the components added to</param>
            /// <param name="sortKey">The sort key for deterministic playback</param>
            public void Add(Entity entity, int sortKey)
            {
                m_entityOperationCommandBuffer.Add(entity, sortKey);
            }
        }
        #endregion
    }

    /// <summary>
    /// A specialized variant of the EntityCommandBuffer exclusively for adding components to entities.
    /// This variant initializes the specified component type with specified values per instance.
    /// </summary>
    public struct AddComponentsCommandBuffer<T0> : INativeDisposable where T0 : unmanaged, IComponentData
    {
        #region Structure
        internal AddComponentsCommandBufferUntyped m_addComponentsCommandBufferUntyped { get; private set; }
        #endregion

        #region CreateDestroy
        /// <summary>
        /// Create an AddComponentsCommandBuffer which can be used to addComponents entities and play them back later.
        /// </summary>
        /// <param name="allocator">The type of allocator to use for allocating the buffer</param>
        public AddComponentsCommandBuffer(AllocatorManager.AllocatorHandle allocator, AddComponentsDestroyedEntityResolution destroyedEntityResolution)
        {
            FixedList128Bytes<ComponentType> types = new FixedList128Bytes<ComponentType>();
            types.Add(ComponentType.ReadWrite<T0>());
            m_addComponentsCommandBufferUntyped = new AddComponentsCommandBufferUntyped(allocator, types, destroyedEntityResolution);
        }

        /// <summary>
        /// Disposes the AddComponentsCommandBuffer after the jobs which use it have finished.
        /// </summary>
        /// <param name="inputDeps">The JobHandle for any jobs previously using this AddComponentsCommandBuffer</param>
        /// <returns></returns>
        public JobHandle Dispose(JobHandle inputDeps)
        {
            return m_addComponentsCommandBufferUntyped.Dispose(inputDeps);
        }

        /// <summary>
        /// Disposes the AddComponentsCommandBuffer
        /// </summary>
        public void Dispose()
        {
            m_addComponentsCommandBufferUntyped.Dispose();
        }
        #endregion

        #region PublicAPI
        /// <summary>
        /// Adds an Entity to the AddComponentsCommandBuffer which should have components added to
        /// </summary>
        /// <param name="entity">The entity to have components added to</param>
        /// <param name="c0">The first component value to initialize for the target entity</param>
        /// <param name="sortKey">The sort key for deterministic playback if interleaving single and parallel writes</param>
        public void Add(Entity entity, T0 c0, int sortKey = int.MaxValue)
        {
            m_addComponentsCommandBufferUntyped.Add(entity, c0, sortKey);
        }

        /// <summary>
        /// Plays back the AddComponentsCommandBuffer.
        /// </summary>
        /// <param name="entityManager">The EntityManager with which to play back the AddComponentsCommandBuffer</param>
        public void Playback(EntityManager entityManager)
        {
            m_addComponentsCommandBufferUntyped.Playback(entityManager);
        }

        /// <summary>
        /// Get the number of entities stored in this AddComponentsCommandBuffer. This method performs a summing operation on every invocation.
        /// </summary>
        /// <returns>The number of elements stored in this AddComponentsCommandBuffer</returns>
        public int Count() => m_addComponentsCommandBufferUntyped.Count();

        /// <summary>
        /// Set additional component types to be added to the target entities. These components will be default-initialized.
        /// </summary>
        /// <param name="tags">The types to add to each target entity</param>
        public void SetComponentTags(ComponentTypeSet tags)
        {
            m_addComponentsCommandBufferUntyped.SetTags(tags);
        }

        /// <summary>
        /// Adds an additional component type to the list of types to add to the target entities. This type will be default-initialized.
        /// </summary>
        /// <param name="tag">The type to add to each target entity</param>
        public void AddComponentTag(ComponentType tag)
        {
            m_addComponentsCommandBufferUntyped.AddTag(tag);
        }

        /// <summary>
        /// Adds an additional component type to the list of types to add to the target entities. This type will be default-initialized.
        /// </summary>
        /// <typeparam name="T">The type to add to each target entity</typeparam>
        public void AddComponentTag<T>() where T : struct, IComponentData
        {
            AddComponentTag(ComponentType.ReadOnly<T>());
        }

        /// <summary>
        /// Gets the ParallelWriter for this AddComponentsCommandBuffer.
        /// </summary>
        /// <returns>The ParallelWriter which shares this AddComponentsCommandBuffer's backing storage.</returns>
        public ParallelWriter AsParallelWriter()
        {
            return new ParallelWriter(m_addComponentsCommandBufferUntyped);
        }
        #endregion

        #region ParallelWriter
        /// <summary>
        /// The parallelWriter implementation of AddComponentsCommandBuffer. Use AsParallelWriter to obtain one from an AddComponentsCommandBuffer
        /// </summary>
        public struct ParallelWriter
        {
            private AddComponentsCommandBufferUntyped.ParallelWriter m_addComponentsCommandBufferUntyped;

            internal ParallelWriter(AddComponentsCommandBufferUntyped eocb)
            {
                m_addComponentsCommandBufferUntyped = eocb.AsParallelWriter();
            }

            /// <summary>
            /// Adds an Entity to the AddComponentsCommandBuffer which should have components added to
            /// </summary>
            /// <param name="entity">The entity to have components added to</param>
            /// <param name="c0">The first component value to initialize for the target entity</param>
            /// <param name="sortKey">The sort key for deterministic playback</param>
            public void Add(Entity entity, T0 c0, int sortKey)
            {
                m_addComponentsCommandBufferUntyped.Add(entity, c0, sortKey);
            }
        }
        #endregion
    }

    /// <summary>
    /// A specialized variant of the EntityCommandBuffer exclusively for adding components to entities.
    /// This variant initializes both the specified component types with specified values per instance.
    /// </summary>
    public struct AddComponentsCommandBuffer<T0, T1> : INativeDisposable where T0 : unmanaged, IComponentData where T1 : unmanaged, IComponentData
    {
        #region Structure
        internal AddComponentsCommandBufferUntyped m_addComponentsCommandBufferUntyped { get; private set; }
        #endregion

        #region CreateDestroy
        /// <summary>
        /// Create an AddComponentsCommandBuffer which can be used to addComponents entities and play them back later.
        /// </summary>
        /// <param name="allocator">The type of allocator to use for allocating the buffer</param>
        public AddComponentsCommandBuffer(AllocatorManager.AllocatorHandle allocator, AddComponentsDestroyedEntityResolution destroyedEntityResolution)
        {
            FixedList128Bytes<ComponentType> types = new FixedList128Bytes<ComponentType>();
            types.Add(ComponentType.ReadWrite<T0>());
            types.Add(ComponentType.ReadWrite<T1>());
            m_addComponentsCommandBufferUntyped = new AddComponentsCommandBufferUntyped(allocator, types, destroyedEntityResolution);
        }

        /// <summary>
        /// Disposes the AddComponentsCommandBuffer after the jobs which use it have finished.
        /// </summary>
        /// <param name="inputDeps">The JobHandle for any jobs previously using this AddComponentsCommandBuffer</param>
        /// <returns></returns>
        public JobHandle Dispose(JobHandle inputDeps)
        {
            return m_addComponentsCommandBufferUntyped.Dispose(inputDeps);
        }

        /// <summary>
        /// Disposes the AddComponentsCommandBuffer
        /// </summary>
        public void Dispose()
        {
            m_addComponentsCommandBufferUntyped.Dispose();
        }
        #endregion

        #region PublicAPI
        /// <summary>
        /// Adds an Entity to the AddComponentsCommandBuffer which should have components added to
        /// </summary>
        /// <param name="entity">The entity to have components added to</param>
        /// <param name="c0">The first component value to initialize for the target entity</param>
        /// <param name="c1">The second component value to initialize for the target entity</param>
        /// <param name="sortKey">The sort key for deterministic playback if interleaving single and parallel writes</param>
        public void Add(Entity entity, T0 c0, T1 c1, int sortKey = int.MaxValue)
        {
            m_addComponentsCommandBufferUntyped.Add(entity, c0, c1, sortKey);
        }

        /// <summary>
        /// Plays back the AddComponentsCommandBuffer.
        /// </summary>
        /// <param name="entityManager">The EntityManager with which to play back the AddComponentsCommandBuffer</param>
        public void Playback(EntityManager entityManager)
        {
            m_addComponentsCommandBufferUntyped.Playback(entityManager);
        }

        /// <summary>
        /// Get the number of entities stored in this AddComponentsCommandBuffer. This method performs a summing operation on every invocation.
        /// </summary>
        /// <returns>The number of elements stored in this AddComponentsCommandBuffer</returns>
        public int Count() => m_addComponentsCommandBufferUntyped.Count();

        /// <summary>
        /// Set additional component types to be added to the target entities. These components will be default-initialized.
        /// </summary>
        /// <param name="tags">The types to add to each target entity</param>
        public void SetComponentTags(ComponentTypeSet tags)
        {
            m_addComponentsCommandBufferUntyped.SetTags(tags);
        }

        /// <summary>
        /// Adds an additional component type to the list of types to add to the target entities. This type will be default-initialized.
        /// </summary>
        /// <param name="tag">The type to add to each target entity</param>
        public void AddComponentTag(ComponentType tag)
        {
            m_addComponentsCommandBufferUntyped.AddTag(tag);
        }

        /// <summary>
        /// Adds an additional component type to the list of types to add to the target entities. This type will be default-initialized.
        /// </summary>
        /// <typeparam name="T">The type to add to each target entity</typeparam>
        public void AddComponentTag<T>() where T : struct, IComponentData
        {
            AddComponentTag(ComponentType.ReadOnly<T>());
        }

        /// <summary>
        /// Gets the ParallelWriter for this AddComponentsCommandBuffer.
        /// </summary>
        /// <returns>The ParallelWriter which shares this AddComponentsCommandBuffer's backing storage.</returns>
        public ParallelWriter AsParallelWriter()
        {
            return new ParallelWriter(m_addComponentsCommandBufferUntyped);
        }
        #endregion

        #region ParallelWriter
        /// <summary>
        /// The parallelWriter implementation of AddComponentsCommandBuffer. Use AsParallelWriter to obtain one from an AddComponentsCommandBuffer
        /// </summary>
        public struct ParallelWriter
        {
            private AddComponentsCommandBufferUntyped.ParallelWriter m_addComponentsCommandBufferUntyped;

            internal ParallelWriter(AddComponentsCommandBufferUntyped icb)
            {
                m_addComponentsCommandBufferUntyped = icb.AsParallelWriter();
            }

            /// <summary>
            /// Adds an Entity to the AddComponentsCommandBuffer which should have components added to
            /// </summary>
            /// <param name="entity">The entity to have components added to</param>
            /// <param name="c0">The first component value to initialize for the target entity</param>
            /// <param name="c1">The second component value to initialize for the target entity</param>
            /// <param name="sortKey">The sort key for deterministic playback</param>
            public void Add(Entity entity, T0 c0, T1 c1, int sortKey)
            {
                m_addComponentsCommandBufferUntyped.Add(entity, c0, c1, sortKey);
            }
        }
        #endregion
    }

    /// <summary>
    /// A specialized variant of the EntityCommandBuffer exclusively for adding components to entities.
    /// This variant initializes the three specified component types with specified values per instance.
    /// </summary>
    public struct AddComponentsCommandBuffer<T0, T1, T2> : INativeDisposable where T0 : unmanaged, IComponentData where T1 : unmanaged, IComponentData where T2 : unmanaged,
                                                           IComponentData
    {
        #region Structure
        internal AddComponentsCommandBufferUntyped m_addComponentsCommandBufferUntyped { get; private set; }
        #endregion

        #region CreateDestroy
        /// <summary>
        /// Create an AddComponentsCommandBuffer which can be used to addComponents entities and play them back later.
        /// </summary>
        /// <param name="allocator">The type of allocator to use for allocating the buffer</param>
        public AddComponentsCommandBuffer(AllocatorManager.AllocatorHandle allocator, AddComponentsDestroyedEntityResolution destroyedEntityResolution)
        {
            FixedList128Bytes<ComponentType> types = new FixedList128Bytes<ComponentType>();
            types.Add(ComponentType.ReadWrite<T0>());
            types.Add(ComponentType.ReadWrite<T1>());
            types.Add(ComponentType.ReadWrite<T2>());
            m_addComponentsCommandBufferUntyped = new AddComponentsCommandBufferUntyped(allocator, types, destroyedEntityResolution);
        }

        /// <summary>
        /// Disposes the AddComponentsCommandBuffer after the jobs which use it have finished.
        /// </summary>
        /// <param name="inputDeps">The JobHandle for any jobs previously using this AddComponentsCommandBuffer</param>
        /// <returns></returns>
        public JobHandle Dispose(JobHandle inputDeps)
        {
            return m_addComponentsCommandBufferUntyped.Dispose(inputDeps);
        }

        /// <summary>
        /// Disposes the AddComponentsCommandBuffer
        /// </summary>
        public void Dispose()
        {
            m_addComponentsCommandBufferUntyped.Dispose();
        }
        #endregion

        #region PublicAPI
        /// <summary>
        /// Adds an Entity to the AddComponentsCommandBuffer which should have components added to
        /// </summary>
        /// <param name="entity">The entity to have components added to</param>
        /// <param name="c0">The first component value to initialize for the target entity</param>
        /// <param name="c1">The second component value to initialize for the target entity</param>
        /// <param name="c2">The third component value to initialize for the target entity</param>
        /// <param name="sortKey">The sort key for deterministic playback if interleaving single and parallel writes</param>
        public void Add(Entity entity, T0 c0, T1 c1, T2 c2, int sortKey = int.MaxValue)
        {
            m_addComponentsCommandBufferUntyped.Add(entity, c0, c1, c2, sortKey);
        }

        /// <summary>
        /// Plays back the AddComponentsCommandBuffer.
        /// </summary>
        /// <param name="entityManager">The EntityManager with which to play back the AddComponentsCommandBuffer</param>
        public void Playback(EntityManager entityManager)
        {
            m_addComponentsCommandBufferUntyped.Playback(entityManager);
        }

        /// <summary>
        /// Get the number of entities stored in this AddComponentsCommandBuffer. This method performs a summing operation on every invocation.
        /// </summary>
        /// <returns>The number of elements stored in this AddComponentsCommandBuffer</returns>
        public int Count() => m_addComponentsCommandBufferUntyped.Count();

        /// <summary>
        /// Set additional component types to be added to the target entities. These components will be default-initialized.
        /// </summary>
        /// <param name="tags">The types to add to each target entity</param>
        public void SetComponentTags(ComponentTypeSet tags)
        {
            m_addComponentsCommandBufferUntyped.SetTags(tags);
        }

        /// <summary>
        /// Adds an additional component type to the list of types to add to the target entities. This type will be default-initialized.
        /// </summary>
        /// <param name="tag">The type to add to each target entity</param>
        public void AddComponentTag(ComponentType tag)
        {
            m_addComponentsCommandBufferUntyped.AddTag(tag);
        }

        /// <summary>
        /// Adds an additional component type to the list of types to add to the target entities. This type will be default-initialized.
        /// </summary>
        /// <typeparam name="T">The type to add to each target entity</typeparam>
        public void AddComponentTag<T>() where T : struct, IComponentData
        {
            AddComponentTag(ComponentType.ReadOnly<T>());
        }

        /// <summary>
        /// Gets the ParallelWriter for this AddComponentsCommandBuffer.
        /// </summary>
        /// <returns>The ParallelWriter which shares this AddComponentsCommandBuffer's backing storage.</returns>
        public ParallelWriter AsParallelWriter()
        {
            return new ParallelWriter(m_addComponentsCommandBufferUntyped);
        }
        #endregion

        #region ParallelWriter
        /// <summary>
        /// The parallelWriter implementation of AddComponentsCommandBuffer. Use AsParallelWriter to obtain one from an AddComponentsCommandBuffer
        /// </summary>
        public struct ParallelWriter
        {
            private AddComponentsCommandBufferUntyped.ParallelWriter m_addComponentsCommandBufferUntyped;

            internal ParallelWriter(AddComponentsCommandBufferUntyped icb)
            {
                m_addComponentsCommandBufferUntyped = icb.AsParallelWriter();
            }

            /// <summary>
            /// Adds an Entity to the AddComponentsCommandBuffer which should have components added to
            /// </summary>
            /// <param name="entity">The entity to have components added to</param>
            /// <param name="c0">The first component value to initialize for the target entity</param>
            /// <param name="c1">The second component value to initialize for the target entity</param>
            /// <param name="c2">The third component value to initialize for the target entity</param>
            /// <param name="sortKey">The sort key for deterministic playback</param>
            public void Add(Entity entity, T0 c0, T1 c1, T2 c2, int sortKey)
            {
                m_addComponentsCommandBufferUntyped.Add(entity, c0, c1, c2, sortKey);
            }
        }
        #endregion
    }

    /// <summary>
    /// A specialized variant of the EntityCommandBuffer exclusively for adding components to entities.
    /// This variant initializes the four specified component types with specified values per instance.
    /// </summary>
    public struct AddComponentsCommandBuffer<T0, T1, T2, T3> : INativeDisposable where T0 : unmanaged, IComponentData where T1 : unmanaged, IComponentData where T2 : unmanaged,
                                                               IComponentData where T3 : unmanaged, IComponentData
    {
        #region Structure
        internal AddComponentsCommandBufferUntyped m_addComponentsCommandBufferUntyped { get; private set; }
        #endregion

        #region CreateDestroy
        /// <summary>
        /// Create an AddComponentsCommandBuffer which can be used to addComponents entities and play them back later.
        /// </summary>
        /// <param name="allocator">The type of allocator to use for allocating the buffer</param>
        public AddComponentsCommandBuffer(AllocatorManager.AllocatorHandle allocator, AddComponentsDestroyedEntityResolution destroyedEntityResolution)
        {
            FixedList128Bytes<ComponentType> types = new FixedList128Bytes<ComponentType>();
            types.Add(ComponentType.ReadWrite<T0>());
            types.Add(ComponentType.ReadWrite<T1>());
            types.Add(ComponentType.ReadWrite<T2>());
            types.Add(ComponentType.ReadWrite<T3>());
            m_addComponentsCommandBufferUntyped = new AddComponentsCommandBufferUntyped(allocator, types, destroyedEntityResolution);
        }

        /// <summary>
        /// Disposes the AddComponentsCommandBuffer after the jobs which use it have finished.
        /// </summary>
        /// <param name="inputDeps">The JobHandle for any jobs previously using this AddComponentsCommandBuffer</param>
        /// <returns></returns>
        public JobHandle Dispose(JobHandle inputDeps)
        {
            return m_addComponentsCommandBufferUntyped.Dispose(inputDeps);
        }

        /// <summary>
        /// Disposes the AddComponentsCommandBuffer
        /// </summary>
        public void Dispose()
        {
            m_addComponentsCommandBufferUntyped.Dispose();
        }
        #endregion

        #region PublicAPI
        /// <summary>
        /// Adds an Entity to the AddComponentsCommandBuffer which should have components added to
        /// </summary>
        /// <param name="entity">The entity to have components added to</param>
        /// <param name="c0">The first component value to initialize for the target entity</param>
        /// <param name="c1">The second component value to initialize for the target entity</param>
        /// <param name="c2">The third component value to initialize for the target entity</param>
        /// <param name="c3">The fourth component value to initialize for the target entity</param>
        /// <param name="sortKey">The sort key for deterministic playback if interleaving single and parallel writes</param>
        public void Add(Entity entity, T0 c0, T1 c1, T2 c2, T3 c3, int sortKey = int.MaxValue)
        {
            m_addComponentsCommandBufferUntyped.Add(entity, c0, c1, c2, c3, sortKey);
        }

        /// <summary>
        /// Plays back the AddComponentsCommandBuffer.
        /// </summary>
        /// <param name="entityManager">The EntityManager with which to play back the AddComponentsCommandBuffer</param>
        public void Playback(EntityManager entityManager)
        {
            m_addComponentsCommandBufferUntyped.Playback(entityManager);
        }

        /// <summary>
        /// Get the number of entities stored in this AddComponentsCommandBuffer. This method performs a summing operation on every invocation.
        /// </summary>
        /// <returns>The number of elements stored in this AddComponentsCommandBuffer</returns>
        public int Count() => m_addComponentsCommandBufferUntyped.Count();

        /// <summary>
        /// Set additional component types to be added to the target entities. These components will be default-initialized.
        /// </summary>
        /// <param name="tags">The types to add to each target entity</param>
        public void SetComponentTags(ComponentTypeSet tags)
        {
            m_addComponentsCommandBufferUntyped.SetTags(tags);
        }

        /// <summary>
        /// Adds an additional component type to the list of types to add to the target entities. This type will be default-initialized.
        /// </summary>
        /// <param name="tag">The type to add to each target entity</param>
        public void AddComponentTag(ComponentType tag)
        {
            m_addComponentsCommandBufferUntyped.AddTag(tag);
        }

        /// <summary>
        /// Adds an additional component type to the list of types to add to the target entities. This type will be default-initialized.
        /// </summary>
        /// <typeparam name="T">The type to add to each target entity</typeparam>
        public void AddComponentTag<T>() where T : struct, IComponentData
        {
            AddComponentTag(ComponentType.ReadOnly<T>());
        }

        /// <summary>
        /// Gets the ParallelWriter for this AddComponentsCommandBuffer.
        /// </summary>
        /// <returns>The ParallelWriter which shares this AddComponentsCommandBuffer's backing storage.</returns>
        public ParallelWriter AsParallelWriter()
        {
            return new ParallelWriter(m_addComponentsCommandBufferUntyped);
        }
        #endregion

        #region ParallelWriter
        /// <summary>
        /// The parallelWriter implementation of AddComponentsCommandBuffer. Use AsParallelWriter to obtain one from an AddComponentsCommandBuffer
        /// </summary>
        public struct ParallelWriter
        {
            private AddComponentsCommandBufferUntyped.ParallelWriter m_addComponentsCommandBufferUntyped;

            internal ParallelWriter(AddComponentsCommandBufferUntyped icb)
            {
                m_addComponentsCommandBufferUntyped = icb.AsParallelWriter();
            }

            /// <summary>
            /// Adds an Entity to the AddComponentsCommandBuffer which should have components added to
            /// </summary>
            /// <param name="entity">The entity to have components added to</param>
            /// <param name="c0">The first component value to initialize for the target entity</param>
            /// <param name="c1">The second component value to initialize for the target entity</param>
            /// <param name="c2">The third component value to initialize for the target entity</param>
            /// <param name="c3">The fourth component value to initialize for the target entity</param>
            /// <param name="sortKey">The sort key for deterministic playback</param>
            public void Add(Entity entity, T0 c0, T1 c1, T2 c2, T3 c3, int sortKey)
            {
                m_addComponentsCommandBufferUntyped.Add(entity, c0, c1, c2, c3, sortKey);
            }
        }
        #endregion
    }

    /// <summary>
    /// A specialized variant of the EntityCommandBuffer exclusively for adding components to entities.
    /// This variant initializes the four specified component types with specified values per instance.
    /// </summary>
    public struct AddComponentsCommandBuffer<T0, T1, T2, T3, T4> : INativeDisposable where T0 : unmanaged, IComponentData where T1 : unmanaged, IComponentData where T2 : unmanaged,
                                                                   IComponentData where T3 : unmanaged, IComponentData where T4 : unmanaged, IComponentData
    {
        #region Structure
        internal AddComponentsCommandBufferUntyped m_addComponentsCommandBufferUntyped { get; private set; }
        #endregion

        #region CreateDestroy
        /// <summary>
        /// Create an AddComponentsCommandBuffer which can be used to addComponents entities and play them back later.
        /// </summary>
        /// <param name="allocator">The type of allocator to use for allocating the buffer</param>
        public AddComponentsCommandBuffer(AllocatorManager.AllocatorHandle allocator, AddComponentsDestroyedEntityResolution destroyedEntityResolution)
        {
            FixedList128Bytes<ComponentType> types = new FixedList128Bytes<ComponentType>();
            types.Add(ComponentType.ReadWrite<T0>());
            types.Add(ComponentType.ReadWrite<T1>());
            types.Add(ComponentType.ReadWrite<T2>());
            types.Add(ComponentType.ReadWrite<T3>());
            types.Add(ComponentType.ReadWrite<T4>());
            m_addComponentsCommandBufferUntyped = new AddComponentsCommandBufferUntyped(allocator, types, destroyedEntityResolution);
        }

        /// <summary>
        /// Disposes the AddComponentsCommandBuffer after the jobs which use it have finished.
        /// </summary>
        /// <param name="inputDeps">The JobHandle for any jobs previously using this AddComponentsCommandBuffer</param>
        /// <returns></returns>
        public JobHandle Dispose(JobHandle inputDeps)
        {
            return m_addComponentsCommandBufferUntyped.Dispose(inputDeps);
        }

        /// <summary>
        /// Disposes the AddComponentsCommandBuffer
        /// </summary>
        public void Dispose()
        {
            m_addComponentsCommandBufferUntyped.Dispose();
        }
        #endregion

        #region PublicAPI
        /// <summary>
        /// Adds an Entity to the AddComponentsCommandBuffer which should have components added to
        /// </summary>
        /// <param name="entity">The entity to have components added to</param>
        /// <param name="c0">The first component value to initialize for the target entity</param>
        /// <param name="c1">The second component value to initialize for the target entity</param>
        /// <param name="c2">The third component value to initialize for the target entity</param>
        /// <param name="c3">The fourth component value to initialize for the target entity</param>
        /// <param name="c4">The fifth component value to initialize for the target entity</param>
        /// <param name="sortKey">The sort key for deterministic playback if interleaving single and parallel writes</param>
        public void Add(Entity entity, T0 c0, T1 c1, T2 c2, T3 c3, T4 c4, int sortKey = int.MaxValue)
        {
            m_addComponentsCommandBufferUntyped.Add(entity, c0, c1, c2, c3, c4, sortKey);
        }

        /// <summary>
        /// Plays back the AddComponentsCommandBuffer.
        /// </summary>
        /// <param name="entityManager">The EntityManager with which to play back the AddComponentsCommandBuffer</param>
        public void Playback(EntityManager entityManager)
        {
            m_addComponentsCommandBufferUntyped.Playback(entityManager);
        }

        /// <summary>
        /// Get the number of entities stored in this AddComponentsCommandBuffer. This method performs a summing operation on every invocation.
        /// </summary>
        /// <returns>The number of elements stored in this AddComponentsCommandBuffer</returns>
        public int Count() => m_addComponentsCommandBufferUntyped.Count();

        /// <summary>
        /// Set additional component types to be added to the target entities. These components will be default-initialized.
        /// </summary>
        /// <param name="tags">The types to add to each target entity</param>
        public void SetComponentTags(ComponentTypeSet tags)
        {
            m_addComponentsCommandBufferUntyped.SetTags(tags);
        }

        /// <summary>
        /// Adds an additional component type to the list of types to add to the target entities. This type will be default-initialized.
        /// </summary>
        /// <param name="tag">The type to add to each target entity</param>
        public void AddComponentTag(ComponentType tag)
        {
            m_addComponentsCommandBufferUntyped.AddTag(tag);
        }

        /// <summary>
        /// Adds an additional component type to the list of types to add to the target entities. This type will be default-initialized.
        /// </summary>
        /// <typeparam name="T">The type to add to each target entity</typeparam>
        public void AddComponentTag<T>() where T : struct, IComponentData
        {
            AddComponentTag(ComponentType.ReadOnly<T>());
        }

        /// <summary>
        /// Gets the ParallelWriter for this AddComponentsCommandBuffer.
        /// </summary>
        /// <returns>The ParallelWriter which shares this AddComponentsCommandBuffer's backing storage.</returns>
        public ParallelWriter AsParallelWriter()
        {
            return new ParallelWriter(m_addComponentsCommandBufferUntyped);
        }
        #endregion

        #region ParallelWriter
        /// <summary>
        /// The parallelWriter implementation of AddComponentsCommandBuffer. Use AsParallelWriter to obtain one from an AddComponentsCommandBuffer
        /// </summary>
        public struct ParallelWriter
        {
            private AddComponentsCommandBufferUntyped.ParallelWriter m_addComponentsCommandBufferUntyped;

            internal ParallelWriter(AddComponentsCommandBufferUntyped icb)
            {
                m_addComponentsCommandBufferUntyped = icb.AsParallelWriter();
            }

            /// <summary>
            /// Adds an Entity to the AddComponentsCommandBuffer which should have components added to
            /// </summary>
            /// <param name="entity">The entity to have components added to</param>
            /// <param name="c0">The first component value to initialize for the target entity</param>
            /// <param name="c1">The second component value to initialize for the target entity</param>
            /// <param name="c2">The third component value to initialize for the target entity</param>
            /// <param name="c3">The fourth component value to initialize for the target entity</param>
            /// <param name="c4">The fifth component value to initialize for the target entity</param>
            /// <param name="sortKey">The sort key for deterministic playback</param>
            public void Add(Entity entity, T0 c0, T1 c1, T2 c2, T3 c3, T4 c4, int sortKey)
            {
                m_addComponentsCommandBufferUntyped.Add(entity, c0, c1, c2, c3, c4, sortKey);
            }
        }
        #endregion
    }
}

