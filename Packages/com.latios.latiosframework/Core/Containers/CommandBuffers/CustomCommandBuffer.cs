using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;

namespace Latios
{
    /// <summary>
    /// A specialized command buffer for queueing custom commands implementing ICustomCommand.
    /// </summary>
    public struct CustomCommandBuffer<T0> : INativeDisposable where T0 : unmanaged, ICustomCommand
    {
        #region Structure
        internal CustomCommandBufferUntyped m_customCommandBufferUntyped { get; private set; }
        #endregion

        #region CreateDestroy
        public CustomCommandBuffer(AllocatorManager.AllocatorHandle allocator)
        {
            var commands = new FixedList64Bytes<CustomCommandBufferUntyped.CommandMeta>();
            commands.Add(new CustomCommandBufferUntyped.CommandMeta
            {
                function    = CustomCommandRegistry.TypeToPointer<T0>.functionPtr.Data,
                commandSize = UnsafeUtility.SizeOf<T0>()
            });
            m_customCommandBufferUntyped = new CustomCommandBufferUntyped(allocator, commands);
        }

        public JobHandle Dispose(JobHandle inputDeps) => m_customCommandBufferUntyped.Dispose(inputDeps);
        public void Dispose() => m_customCommandBufferUntyped.Dispose();
        #endregion

        #region PublicAPI
        public void Add(T0 c0, int sortKey = int.MaxValue)
        {
            m_customCommandBufferUntyped.Add(c0, sortKey);
        }

        public void Playback(EntityManager entityManager)
        {
            m_customCommandBufferUntyped.Playback(entityManager);
        }

        public int Count() => m_customCommandBufferUntyped.Count();

        public ParallelWriter AsParallelWriter()
        {
            return new ParallelWriter(m_customCommandBufferUntyped);
        }
        #endregion

        #region ParallelWriter
        public struct ParallelWriter
        {
            private CustomCommandBufferUntyped.ParallelWriter m_customCommandBufferUntyped;

            internal ParallelWriter(CustomCommandBufferUntyped ccb)
            {
                m_customCommandBufferUntyped = ccb.AsParallelWriter();
            }

            public void Add(T0 c0, int sortKey)
            {
                m_customCommandBufferUntyped.Add(c0, sortKey);
            }
        }
        #endregion
    }

    /// <summary>
    /// A specialized command buffer for queueing custom commands implementing ICustomCommand.
    /// </summary>
    public struct CustomCommandBuffer<T0, T1> : INativeDisposable where T0 : unmanaged, ICustomCommand where T1 : unmanaged, ICustomCommand
    {
        #region Structure
        internal CustomCommandBufferUntyped m_customCommandBufferUntyped { get; private set; }
        #endregion

        #region CreateDestroy
        public CustomCommandBuffer(AllocatorManager.AllocatorHandle allocator)
        {
            var commands = new FixedList64Bytes<CustomCommandBufferUntyped.CommandMeta>();
            commands.Add(new CustomCommandBufferUntyped.CommandMeta
            {
                function = CustomCommandRegistry.TypeToPointer<T0>.functionPtr.Data,
                commandSize = UnsafeUtility.SizeOf<T0>()
            });
            commands.Add(new CustomCommandBufferUntyped.CommandMeta
            {
                function = CustomCommandRegistry.TypeToPointer<T1>.functionPtr.Data,
                commandSize = UnsafeUtility.SizeOf<T1>()
            });
            m_customCommandBufferUntyped = new CustomCommandBufferUntyped(allocator, commands);
        }

        public JobHandle Dispose(JobHandle inputDeps) => m_customCommandBufferUntyped.Dispose(inputDeps);
        public void Dispose() => m_customCommandBufferUntyped.Dispose();
        #endregion

        #region PublicAPI
        public void Add(T0 c0, T1 c1, int sortKey = int.MaxValue)
        {
            m_customCommandBufferUntyped.Add(c0, c1, sortKey);
        }

        public void Playback(EntityManager entityManager)
        {
            m_customCommandBufferUntyped.Playback(entityManager);
        }

        public int Count() => m_customCommandBufferUntyped.Count();

        public ParallelWriter AsParallelWriter()
        {
            return new ParallelWriter(m_customCommandBufferUntyped);
        }
        #endregion

        #region ParallelWriter
        public struct ParallelWriter
        {
            private CustomCommandBufferUntyped.ParallelWriter m_customCommandBufferUntyped;

            internal ParallelWriter(CustomCommandBufferUntyped ccb)
            {
                m_customCommandBufferUntyped = ccb.AsParallelWriter();
            }

            public void Add(T0 c0, T1 c1, int sortKey)
            {
                m_customCommandBufferUntyped.Add(c0, c1, sortKey);
            }
        }
        #endregion
    }
}
