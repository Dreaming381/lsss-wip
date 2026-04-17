using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Latios.Myri
{
    public unsafe struct FeedbackPipeWriter
    {
        internal MegaPipe* m_megaPipe;

        public void WriteMessage<T>(in T message) where T : unmanaged
        {
            CreateMessage<T>() = message;
        }

        public ref T CreateMessage<T>() where T : unmanaged
        {
            return ref *m_megaPipe->AllocateMessage<T>();
        }

        public void* CreateMessageDynamic(long typeHash, int sizeInBytes, int alignInBytes)
        {
            return m_megaPipe->AllocateMessage(typeHash, sizeInBytes, alignInBytes);
        }

        public PipeSpan<T> CreatePipeSpan<T>(int elementCount) where T : unmanaged
        {
            return new PipeSpan<T>
            {
                m_ptr    = (T*)m_megaPipe->AllocateData(UnsafeUtility.SizeOf<T>() * elementCount, UnsafeUtility.AlignOf<T>()),
                m_length = elementCount
            };
        }
    }

    public unsafe struct FeedbackPipeReader
    {
        // Length of 1, comes from a SubArray of all feedbacks to be processed.
        [ReadOnly] internal NativeArray<MegaPipe> m_megaPipe;

        public Enumerator<T> Each<T>() where T : unmanaged
        {
            return new Enumerator<T>
            {
                m_megaPipe       = m_megaPipe,
                m_pipeEnumerator = m_megaPipe[0].GetEnumerator(BurstRuntime.GetHashCode64<T>())
            };
        }

        public EnumeratorUntyped Each(long typeHash)
        {
            return new EnumeratorUntyped
            {
                m_megaPipe       = m_megaPipe,
                m_pipeEnumerator = m_megaPipe[0].GetEnumerator(typeHash)
            };
        }

        public struct Enumerator<T> where T : unmanaged
        {
            [ReadOnly] internal NativeArray<MegaPipe> m_megaPipe;
            internal MegaPipe.Enumerator              m_pipeEnumerator;

            public Enumerator<T> GetEnumerator() => this;
            public ref T Current => ref *(T*)m_pipeEnumerator.Current;
            public bool MoveNext()
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                if (!m_megaPipe[0].isCreated)
                    return false;
#endif
                return m_pipeEnumerator.MoveNext();
            }
        }

        public struct EnumeratorUntyped
        {
            [ReadOnly] internal NativeArray<MegaPipe> m_megaPipe;
            internal MegaPipe.Enumerator              m_pipeEnumerator;

            public EnumeratorUntyped GetEnumerator() => this;
            public void* Current => m_pipeEnumerator.Current;
            public bool MoveNext()
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                if (!m_megaPipe[0].isCreated)
                    return false;
#endif
                return m_pipeEnumerator.MoveNext();
            }
        }
    }
}

