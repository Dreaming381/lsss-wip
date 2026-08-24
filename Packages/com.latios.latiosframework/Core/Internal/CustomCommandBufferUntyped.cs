using System;
using System.Diagnostics;
using Latios.Unsafe;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;

using CommandFunction = Unity.Burst.FunctionPointer<Latios.ICustomCommand.OnPlayback>;

namespace Latios
{
    [NativeContainer]
    [BurstCompile]
    internal unsafe struct CustomCommandBufferUntyped : INativeDisposable
    {
        #region Structure
        [NativeDisableUnsafePtrRestriction] private UnsafeParallelBlockList<int>* m_sortKeyBlockList;
        [NativeDisableUnsafePtrRestriction] private UnsafeParallelBlockList*      m_dataBlockList;
        [NativeDisableUnsafePtrRestriction] private State*                        m_state;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        AtomicSafetyHandle m_Safety;

        static readonly SharedStatic<int> s_staticSafetyId = SharedStatic<int>.GetOrCreate<CustomCommandBufferUntyped>();
#endif

        private struct State
        {
            public FixedList64Bytes<CommandFunction> commandFunctions;
            public FixedList64Bytes<int>             commandSizes;
            public AllocatorManager.AllocatorHandle  allocator;
            public bool                              playedBack;

            [NativeDisableUnsafePtrRestriction] public BlockStreamAllocator* spanAllocators;
        }

        internal struct SortKey : IRadixSortableInt
        {
            public int sortKey;

            public int GetKey() => sortKey;
        }

        internal struct CommandMeta
        {
            public CommandFunction function;
            public int             commandSize;
        }
        #endregion

        #region CreateDestroy
        public CustomCommandBufferUntyped(AllocatorManager.AllocatorHandle allocator,
                                          FixedList64Bytes<CommandMeta>    commands = default) : this(allocator, commands, 1)
        {
        }

        internal CustomCommandBufferUntyped(AllocatorManager.AllocatorHandle allocator,
                                            FixedList64Bytes<CommandMeta>    commands,
                                            int disposeSentinelStackDepth)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            CheckAllocator(allocator);

            m_Safety = CollectionHelper.CreateSafetyHandle(allocator);
            CollectionHelper.SetStaticSafetyId<EntityOperationCommandBuffer>(ref m_Safety, ref s_staticSafetyId.Data);
            AtomicSafetyHandle.SetBumpSecondaryVersionOnScheduleWrite(m_Safety, true);
#endif

            int                               dataPayloadSize = 0;
            FixedList64Bytes<int>             commandSizes    = new FixedList64Bytes<int>();
            FixedList64Bytes<CommandFunction> functions       = new FixedList64Bytes<CommandFunction>();
            for (int i = 0; i < commands.Length; i++)
            {
                functions.Add(commands[i].function);
                commandSizes.Add(commands[i].commandSize);
                dataPayloadSize += commands[i].commandSize;
            }
            m_sortKeyBlockList  = AllocatorManager.Allocate<UnsafeParallelBlockList<int> >(allocator, 1);
            m_dataBlockList     = AllocatorManager.Allocate<UnsafeParallelBlockList>(allocator, 1);
            m_state             = AllocatorManager.Allocate<State>(allocator, 1);
            *m_sortKeyBlockList = new UnsafeParallelBlockList<int>(256, allocator);
            *m_dataBlockList    = new UnsafeParallelBlockList(dataPayloadSize, 256, allocator);
            *m_state            = new State
            {
                commandFunctions = functions,
                commandSizes     = commandSizes,
                allocator        = allocator,
                playedBack       = false,
                spanAllocators   = AllocatorManager.Allocate<BlockStreamAllocator>(allocator, JobsUtility.MaxJobThreadCount)
            };
            for (int i = 0; i < JobsUtility.MaxJobThreadCount; i++)
                m_state->spanAllocators[i] = new BlockStreamAllocator(allocator);
        }

        [BurstCompile]
        private struct DisposeJob : IJob
        {
            [NativeDisableUnsafePtrRestriction]
            public State* state;

            [NativeDisableUnsafePtrRestriction]
            public UnsafeParallelBlockList<int>* sortKeyBlockList;
            [NativeDisableUnsafePtrRestriction]
            public UnsafeParallelBlockList* dataBlockList;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            internal AtomicSafetyHandle m_Safety;
#endif

            public void Execute()
            {
                Deallocate(state, sortKeyBlockList, dataBlockList);
            }
        }

        public JobHandle Dispose(JobHandle inputDeps)
        {
            var jobHandle = new DisposeJob
            {
                sortKeyBlockList = m_sortKeyBlockList,
                dataBlockList    = m_dataBlockList,
                state            = m_state,
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                m_Safety = m_Safety
#endif
            }.Schedule(inputDeps);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.Release(m_Safety);
#endif
            m_state            = null;
            m_dataBlockList    = null;
            m_sortKeyBlockList = null;
            return jobHandle;
        }

        public void Dispose()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            CollectionHelper.DisposeSafetyHandle(ref m_Safety);
#endif
            Deallocate(m_state, m_sortKeyBlockList, m_dataBlockList);
        }

        private static void Deallocate(State* state, UnsafeParallelBlockList<int>* sortKeyBlockList, UnsafeParallelBlockList* dataBlockList)
        {
            var allocator = state->allocator;
            for (int i = 0; i < JobsUtility.MaxJobThreadCount; i++)
                state->spanAllocators[i].Dispose();
            AllocatorManager.Free(allocator, state->spanAllocators, JobsUtility.MaxJobThreadCount);
            sortKeyBlockList->Dispose();
            dataBlockList->Dispose();
            AllocatorManager.Free(allocator, sortKeyBlockList, 1);
            AllocatorManager.Free(allocator, dataBlockList,    1);
            AllocatorManager.Free(allocator, state,            1);
        }
        #endregion

        #region API
        [WriteAccessRequired]
        public void Add<T0>(T0 c0, int sortKey = int.MaxValue) where T0 : unmanaged
        {
            CheckWriteAccess();
            CheckHasNotPlayedBack();
            m_sortKeyBlockList->Write(sortKey, 0);
            byte* ptr = (byte*)m_dataBlockList->Allocate(0);
            UnsafeUtility.CopyStructureToPtr(ref c0, ptr);
        }

        [WriteAccessRequired]
        public void Add<T0, T1>(T0 c0, T1 c1, int sortKey = int.MaxValue) where T0 : unmanaged where T1 : unmanaged
        {
            CheckWriteAccess();
            CheckHasNotPlayedBack();
            m_sortKeyBlockList->Write(sortKey, 0);
            byte* ptr = (byte*)m_dataBlockList->Allocate(0);
            UnsafeUtility.CopyStructureToPtr(ref c0, ptr);
            ptr += m_state->commandSizes[0];
            UnsafeUtility.CopyStructureToPtr(ref c1, ptr);
        }

        [WriteAccessRequired]
        public CommandSpan<T> CreateCommandSpan<T>(int elementCount) where T : unmanaged
        {
            CheckWriteAccess();
            CheckHasNotPlayedBack();
            return CreateCommandSpan<T>(m_state, elementCount, 0);
        }

        static CommandSpan<T> CreateCommandSpan<T>(State* state, int elementCount, int threadIndex) where T : unmanaged
        {
            CheckElementCountValid(elementCount);
            return new CommandSpan<T>
            {
                m_ptr    = state->spanAllocators[threadIndex].Allocate<T>(elementCount),
                m_length = elementCount
            };
        }

        public int Count()
        {
            CheckReadAccess();
            return m_sortKeyBlockList->Count();
        }

        public void Playback(EntityManager entityManager)
        {
            CheckWriteAccess();
            CheckHasNotPlayedBack();
            Playbacker.Playback((CustomCommandBufferUntyped*)UnsafeUtility.AddressOf(ref this), (EntityManager*)UnsafeUtility.AddressOf(ref entityManager));
        }

        public ParallelWriter AsParallelWriter()
        {
            var writer = new ParallelWriter(m_sortKeyBlockList, m_dataBlockList, m_state);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            writer.m_Safety = m_Safety;
            CollectionHelper.SetStaticSafetyId<ParallelWriter>(ref writer.m_Safety, ref ParallelWriter.s_staticSafetyId.Data);
#endif
            return writer;
        }
        #endregion

        #region Implementation
        [BurstCompile]
        static class Playbacker
        {
            [BurstCompile]
            public static void Playback(CustomCommandBufferUntyped* ccb, EntityManager* em)
            {
                PlaybackOnThread(*ccb, *em);
            }

            static void PlaybackOnThread(CustomCommandBufferUntyped ccb, EntityManager em)
            {
                int count = ccb.Count();
                if (count == 0)
                    return;

                var sortKeyArray = new NativeArray<int>(count, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                ccb.m_sortKeyBlockList->GetElementValues(sortKeyArray);

                var unsortedDataPtrs = new NativeArray<UnsafeIndexedBlockList.ElementPtr>(count, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                ccb.m_dataBlockList->GetElementPtrs(unsortedDataPtrs);

                var ranks = new NativeArray<int>(count, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                RadixSort.RankSortInt(ranks, sortKeyArray.Reinterpret<SortKey>());

                var sortedDataPtrs = new NativeArray<UnsafeIndexedBlockList.ElementPtr>(count, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                for (int i = 0; i < count; i++)
                {
                    sortedDataPtrs[i] = unsortedDataPtrs[ranks[i]];
                }

                int commandOffset = 0;
                for (int i = 0; i < ccb.m_state->commandFunctions.Length; i++)
                {
                    var commandSize = ccb.m_state->commandSizes[i];
                    var context     = new ICustomCommand.Context
                    {
                        entityManager = em,
                        count         = count,
                        dataPtrs      = sortedDataPtrs,
                        commandOffset = commandOffset,
                        expectedSize  = commandSize,
                    };
                    ccb.m_state->commandFunctions[i].Invoke(ref context);
                    commandOffset += commandSize;
                }

                ccb.m_state->playedBack = true;
            }
        }
        #endregion

        #region Checks
        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        void CheckWriteAccess()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        void CheckReadAccess()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS"), Conditional("UNITY_DOTS_DEBUG")]
        void CheckHasNotPlayedBack()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            if (m_state->playedBack)
                throw new InvalidOperationException("CustomCommandBuffer has already been played back.");
#endif
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS"), Conditional("UNITY_DOTS_DEBUG")]
        static void CheckElementCountValid(int elementCount)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            if (elementCount < 0)
                throw new InvalidOperationException($"A CommandSpan must be created with zero or more elements, but {elementCount} was requested.");
#endif
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS"), Conditional("UNITY_DOTS_DEBUG")]
        static void CheckAllocator(AllocatorManager.AllocatorHandle allocator)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            if (allocator.ToAllocator <= Allocator.None)
                throw new System.InvalidOperationException("Allocator cannot be Invalid or None");
#endif
        }
        #endregion

        #region ParallelWriter
        [NativeContainer]
        [NativeContainerIsAtomicWriteOnly]
        public struct ParallelWriter
        {
            [NativeDisableUnsafePtrRestriction] private UnsafeParallelBlockList<int>* m_sortKeyBlockList;
            [NativeDisableUnsafePtrRestriction] private UnsafeParallelBlockList*      m_dataBlockList;
            [NativeDisableUnsafePtrRestriction] private State*                        m_state;
            [NativeSetThreadIndex] private int                                        m_ThreadIndex;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            internal AtomicSafetyHandle m_Safety;
            internal static readonly SharedStatic<int> s_staticSafetyId = SharedStatic<int>.GetOrCreate<ParallelWriter>();
#endif

            internal ParallelWriter(UnsafeParallelBlockList<int>* sortKeyBlockList, UnsafeParallelBlockList* dataBlockList, void* state)
            {
                m_sortKeyBlockList = sortKeyBlockList;
                m_dataBlockList    = dataBlockList;
                m_state            = (State*)state;
                m_ThreadIndex      = 0;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                m_Safety = default;
#endif
            }

            public void Add<T0>(T0 c0, int sortKey) where T0 : unmanaged
            {
                CheckWriteAccess();
                CheckHasNotPlayedBack();
                m_sortKeyBlockList->Write(sortKey, m_ThreadIndex);
                byte* ptr = (byte*)m_dataBlockList->Allocate(m_ThreadIndex);
                UnsafeUtility.CopyStructureToPtr(ref c0, ptr);
            }

            public void Add<T0, T1>(T0 c0, T1 c1, int sortKey) where T0 : unmanaged where T1 : unmanaged
            {
                CheckWriteAccess();
                CheckHasNotPlayedBack();
                m_sortKeyBlockList->Write(sortKey, m_ThreadIndex);
                byte* ptr = (byte*)m_dataBlockList->Allocate(m_ThreadIndex);
                UnsafeUtility.CopyStructureToPtr(ref c0, ptr);
                ptr += m_state->commandSizes[0];
                UnsafeUtility.CopyStructureToPtr(ref c1, ptr);
            }

            public CommandSpan<T> CreateCommandSpan<T>(int elementCount) where T : unmanaged
            {
                CheckWriteAccess();
                CheckHasNotPlayedBack();
                return CustomCommandBufferUntyped.CreateCommandSpan<T>(m_state, elementCount, m_ThreadIndex);
            }

            [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
            void CheckWriteAccess()
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
            }

            [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS"), Conditional("UNITY_DOTS_DEBUG")]
            void CheckHasNotPlayedBack()
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
                if (m_state->playedBack)
                    throw new InvalidOperationException("CustomCommandBuffer has already been played back.");
#endif
            }
        }
        #endregion
    }
}

