using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Latios.Unsafe
{
    /// <summary>
    /// A convenience container for allocating a thread-local cached structure in a job. Use this in an
    /// IJobEach.IParameterHandle or similar to avoid making new allocations across multiple Execute()
    /// invocations.
    /// </summary>
    public unsafe struct ThreadCache<T> where T : unmanaged
    {
        [NativeDisableUnsafePtrRestriction] T* m_ptr;

        /// <summary>
        /// True if this cache has been allocated
        /// </summary>
        public bool isCreated => m_ptr != null;

        /// <summary>
        /// Allocates the cache from Allocator.Temp and initializes it with the specified value
        /// </summary>
        public ThreadCache(in T initialValue)
        {
            m_ptr  = AllocatorManager.Allocate<T>(Allocator.Temp);
            *m_ptr = initialValue;
        }

        /// <summary>
        /// A reference to the cached value. isCreated must be true before accessing this.
        /// </summary>
        public ref T cache => ref *m_ptr;
    }
}

