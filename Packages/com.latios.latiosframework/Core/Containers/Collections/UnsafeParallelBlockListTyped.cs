using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

namespace Latios.Unsafe
{
    /// <summary>
    /// An unsafe container which can be written to by multiple threads and multiple jobs.
    /// The container is lock-free and items never change addresses once written.
    /// Items written in the same thread in the same job will be read back in the same order.
    /// Unlike Unity's Unsafe* containers, it is safe to copy this type by value.
    /// </summary>
    public unsafe struct UnsafeParallelBlockList<T> where T : unmanaged
    {
        private UnsafeParallelBlockList m_blockList;

        /// <summary>
        /// Construct a new UnsafeParallelBlockList using a UnityEngine allocator
        /// </summary>
        /// <param name="elementsPerBlock">
        /// The number of elements stored per native thread index before needing to perform an additional allocation.
        /// Higher values may allocate more memory that is left unused. Lower values may perform more allocations.
        /// </param>
        /// <param name="allocator">The allocator to use for allocations</param>
        public UnsafeParallelBlockList(int elementsPerBlock, AllocatorManager.AllocatorHandle allocator)
        {
            m_blockList = new UnsafeParallelBlockList(UnsafeUtility.SizeOf<T>(), elementsPerBlock, allocator);
        }

        /// <summary>
        /// Write an element for a given thread index
        /// </summary>
        /// <param name="value">The value to write</param>
        /// <param name="threadIndex">The thread index to use when writing. This should come from [NativeSetThreadIndex] or JobsUtility.ThreadIndex.</param>
        public void Write(in T value, int threadIndex)
        {
            m_blockList.Write(in value, threadIndex);
        }

        /// <summary>
        /// Reserve memory for an element and return the result by ref. The memory is NOT zero-initialized.
        /// </summary>
        /// <param name="threadIndex">The thread index to use when allocating. This should come from [NativeSetThreadIndex] or JobsUtility.ThreadIndex.</param>
        /// <returns>An uninitialized reference to the newly allocated data</returns>
        public ref T Allocate(int threadIndex)
        {
            return ref *(T*)m_blockList.Allocate(threadIndex);
        }

        /// <summary>
        /// Count the number of elements. Do this once and cache the result.
        /// </summary>
        /// <returns>The number of elements stored</returns>
        public int Count()
        {
            return m_blockList.Count();
        }

        /// <summary>
        /// Count the number of elements for a specific thread index
        /// </summary>
        /// <param name="threadIndex">The thread index to get the count of elements written</param>
        /// <returns>The number of elements written to the specifed thread index</returns>
        public int CountForThreadIndex(int threadIndex)
        {
            return m_blockList.CountForThreadIndex(threadIndex);
        }

        /// <summary>
        /// Returns true if the struct is not in a default uninitialized state.
        /// This may report true incorrectly if the memory where this instance
        /// exists was left uninitialized rather than cleared.
        /// </summary>
        public bool isCreated => m_blockList.isCreated;

        /// <summary>
        /// Copies all the elements stored into values.
        /// </summary>
        /// <param name="values">An array where the elements should be copied to. Its Length should be equal to Count().</param>
        public void GetElementValues(NativeArray<T> values)
        {
            m_blockList.GetElementValues(values);
        }

        /// <summary>
        /// Uses a job to dispose this container
        /// </summary>
        /// <param name="inputDeps">A JobHandle for all jobs which should finish before disposal.</param>
        /// <returns>A JobHandle for the disposal job.</returns>
        public JobHandle Dispose(JobHandle inputDeps)
        {
            return m_blockList.Dispose(inputDeps);
        }

        /// <summary>
        /// Disposes the container immediately. It is legal to call this from within a job,
        /// as long as no other jobs or threads are using it.
        /// </summary>
        public void Dispose()
        {
            m_blockList.Dispose();
        }
    }
}

