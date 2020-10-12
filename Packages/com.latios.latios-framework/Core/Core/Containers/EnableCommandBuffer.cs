using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;

namespace Latios
{
    /// <summary>
    /// A specialized variant of the EntityCommandBuffer exclusively for enabling entities.
    /// Enabled entities automatically account for LinkedEntityGroup at the time of playback.
    /// </summary>
    [NativeContainer]
    public unsafe struct EnableCommandBuffer : INativeDisposable
    {
        #region Structure
        [NativeDisableUnsafePtrRestriction]
        private UnsafeParallelBlockList* m_blockList;

        [NativeDisableUnsafePtrRestriction]
        private State* m_state;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        //Unfortunately this name is hardcoded into Unity. No idea how EntityCommandBuffer gets away with multiple safety handles.
        AtomicSafetyHandle m_Safety;

        static readonly SharedStatic<int> s_staticSafetyId = SharedStatic<int>.GetOrCreate<EnableCommandBuffer>();

        [BurstDiscard]
        static void CreateStaticSafetyId()
        {
            s_staticSafetyId.Data = AtomicSafetyHandle.NewStaticSafetyId<EnableCommandBuffer>();
        }

        [NativeSetClassTypeToNullOnSchedule]
        //Unfortunately this name is hardcoded into Unity.
        DisposeSentinel m_DisposeSentinel;
#endif

        private struct State
        {
            public Allocator allocator;
            public bool      hasPartiallyPlayedBack;
            public bool      shouldPlayback;
        }

        private struct EntityToEnable : IRadixSortable32
        {
            public Entity entity;
            public int    sortKey;

            public int GetKey() => sortKey;
        }
        #endregion

        #region CreateDestroy
        /// <summary>
        /// Create an EnableCommandBuffer which can be used to enable entities and play them back later.
        /// </summary>
        /// <param name="allocator">The type of allocator to use for allocating the buffer</param>
        public EnableCommandBuffer(Allocator allocator)
        {
            m_blockList = (UnsafeParallelBlockList*)UnsafeUtility.Malloc(UnsafeUtility.SizeOf<UnsafeParallelBlockList>(),
                                                                         UnsafeUtility.AlignOf<UnsafeParallelBlockList>(),
                                                                         allocator);
            m_state      = (State*)UnsafeUtility.Malloc(UnsafeUtility.SizeOf<State>(), UnsafeUtility.AlignOf<State>(), allocator);
            *m_blockList = new UnsafeParallelBlockList(UnsafeUtility.SizeOf<EntityToEnable>(), 256, allocator);
            *m_state     = new State
            {
                allocator              = allocator,
                hasPartiallyPlayedBack = false,
                shouldPlayback         = true
            };

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            DisposeSentinel.Create(out m_Safety, out m_DisposeSentinel, 0, allocator);

            if (s_staticSafetyId.Data == 0)
            {
                CreateStaticSafetyId();
            }
            AtomicSafetyHandle.SetStaticSafetyId(ref m_Safety, s_staticSafetyId.Data);
#endif
        }

        private struct DisposeJob : IJob
        {
            [NativeDisableUnsafePtrRestriction]
            public State* state;

            [NativeDisableUnsafePtrRestriction]
            public UnsafeParallelBlockList* blockList;

            public void Execute()
            {
                Deallocate(state, blockList);
            }
        }

        /// <summary>
        /// Disposes the EnableCommandBuffer after the jobs which use it have finished.
        /// </summary>
        /// <param name="inputDeps">The JobHandle for any jobs previously using this EnableCommandBuffer</param>
        /// <returns></returns>
        public JobHandle Dispose(JobHandle inputDeps)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            // [DeallocateOnJobCompletion] is not supported, but we want the deallocation
            // to happen in a thread. DisposeSentinel needs to be cleared on main thread.
            // AtomicSafetyHandle can be destroyed after the job was scheduled (Job scheduling
            // will check that no jobs are writing to the container).
            DisposeSentinel.Clear(ref m_DisposeSentinel);
#endif
            var jobHandle = new DisposeJob { blockList = m_blockList, state = m_state }.Schedule(inputDeps);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.Release(m_Safety);
#endif
            return jobHandle;
        }

        /// <summary>
        /// Disposes the EnableCommandBuffer
        /// </summary>
        public void Dispose()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            DisposeSentinel.Dispose(ref m_Safety, ref m_DisposeSentinel);
#endif
            Deallocate(m_state, m_blockList);
        }

        private static void Deallocate(State* state, UnsafeParallelBlockList* blockList)
        {
            var allocator = state->allocator;
            blockList->Dispose();
            UnsafeUtility.Free(blockList, allocator);
            UnsafeUtility.Free(state,     allocator);
        }
        #endregion

        #region Checks
        [Conditional("ENABLE_UNITY_COLLECTION_CHECKS")]
        void CheckWriteAccess()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
            if (m_state->hasPartiallyPlayedBack)
                throw new InvalidOperationException("The EnableCommandBuffer has already been at least partially played back. You cannot add any more commands to it.");
#endif
        }

        [Conditional("ENABLE_UNITY_COLLECTION_CHECKS")]
        void CheckReadAccess()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
        }

        [Conditional("ENABLE_UNITY_COLLECTION_CHECKS")]
        void CheckCanPlayback()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
            if (m_state->hasPartiallyPlayedBack)
                throw new InvalidOperationException(
                    "The EnableCommandBuffer has already been at least partially played back. If you created an EnableCommandBuffer.Playbacker, use that to playback data.");
#endif
        }
        #endregion

        #region PublicAPI
        /// <summary>
        /// Adds an Entity to the EnableCommandBuffer which should be enabled
        /// </summary>
        /// <param name="entity">The entity to be enabled, including its LinkedEntityGroup at the time of playback if it has one</param>
        /// <param name="sortKey">The sort key for deterministic playback if interleaving single and parallel writes</param>
        [WriteAccessRequired]
        public void Add(Entity entity, int sortKey = int.MaxValue)
        {
            CheckWriteAccess();
            m_blockList->Write(new EntityToEnable { entity = entity, sortKey = sortKey });
        }

        [WriteAccessRequired]
        public void Playback(EntityManager entityManager, BufferFromEntity<LinkedEntityGroup> linkedFEReadOnly)
        {
            CheckCanPlayback();
            if (m_state->shouldPlayback == false)
                return;

            m_state->hasPartiallyPlayedBack = true;

            //PlaybackJobs
            var rootEntities                           = new NativeList<Entity>(0, Allocator.TempJob);
            var gatheredEntities                       = new NativeList<Entity>(0, Allocator.TempJob);
            new PrepForPlaybackJob { ecb               = this, entities = rootEntities }.Run();
            new GatherLinkedEntitiesJob { rootEntities = rootEntities, gatheredEntities = gatheredEntities, linkedFE = linkedFEReadOnly }.Run();
            entityManager.RemoveComponent<Disabled>(gatheredEntities);
            rootEntities.Dispose();
            gatheredEntities.Dispose();
        }

        public int Count()
        {
            CheckReadAccess();
            return m_blockList->Count;
        }
        #endregion

        #region InternalAPI
        private void GetElementPtrs(NativeArray<UnsafeParallelBlockList.ElementPtr> ptrs)
        {
            CheckReadAccess();
            m_blockList->GetElementPtrs(ptrs);
        }
        #endregion

        #region PlaybackJobs
        [BurstCompile]
        private struct PrepForPlaybackJob : IJob
        {
            [ReadOnly] public EnableCommandBuffer ecb;
            public NativeList<Entity>             entities;

            public void Execute()
            {
                var ptrs                 = new NativeArray<UnsafeParallelBlockList.ElementPtr>(ecb.Count(), Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                var tempEntitiesToEnable = new NativeArray<EntityToEnable>(ptrs.Length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                var ranks                = new NativeArray<int>(ptrs.Length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                entities.ResizeUninitialized(ptrs.Length);
                ecb.GetElementPtrs(ptrs);
                for (int i = 0; i < ptrs.Length; i++)
                {
                    //On 64 bit systems, a pointer is the same size as an Entity, so it is best to just copy the whole payload to a NativeArray.
                    UnsafeUtility.CopyPtrToStructure<EntityToEnable>(ptrs[i].ptr, out var ete);
                    tempEntitiesToEnable[i] = ete;
                }

                //Radix sort
                RadixSort.RankSort(ranks, tempEntitiesToEnable);

                //Copy results
                for (int i = 0; i < ranks.Length; i++)
                {
                    entities[i] = tempEntitiesToEnable[ranks[i]].entity;
                }
            }
        }

        [BurstCompile]
        private struct GatherLinkedEntitiesJob : IJob
        {
            [ReadOnly] public NativeArray<Entity>                 rootEntities;
            [ReadOnly] public BufferFromEntity<LinkedEntityGroup> linkedFE;
            public NativeList<Entity>                             gatheredEntities;

            public void Execute()
            {
                int count = 0;
                for (int i = 0; i < rootEntities.Length; i++)
                {
                    if (linkedFE.HasComponent(rootEntities[i]))
                    {
                        count += linkedFE[rootEntities[i]].Length;
                    }
                    else
                    {
                        count++;
                    }
                }
                gatheredEntities.ResizeUninitialized(count);
                var gatheredEntitiesAsArray = gatheredEntities.AsArray();
                count                       = 0;
                for (int i = 0; i < rootEntities.Length; i++)
                {
                    if (linkedFE.HasComponent(rootEntities[i]))
                    {
                        var currentGroup = linkedFE[rootEntities[i]];
                        NativeArray<Entity>.Copy(currentGroup.AsNativeArray().Reinterpret<Entity>(), 0, gatheredEntitiesAsArray, count, currentGroup.Length);
                        count += currentGroup.Length;
                    }
                    else
                    {
                        gatheredEntitiesAsArray[count] = rootEntities[i];
                        count++;
                    }
                }
            }
        }

        #endregion
    }
}

