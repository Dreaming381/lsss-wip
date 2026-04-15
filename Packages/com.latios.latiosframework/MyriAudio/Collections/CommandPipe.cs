using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Latios.Myri
{
    public unsafe struct CommandPipeWriter
    {
        [ReadOnly] internal NativeReference<UnsafeList<MegaPipe> > m_perThreadPipes;
        internal AllocatorManager.AllocatorHandle                  m_allocator;
        [NativeSetThreadIndex] int                                 m_threadIndex;

        ref MegaPipe GetPipe()
        {
            ref var pipe = ref m_perThreadPipes.Value.ElementAt(m_threadIndex);
            if (!pipe.isCreated)
            {
                pipe = new MegaPipe(m_allocator);
            }
            return ref pipe;
        }

        public void WriteMessage<T>(in T message) where T : unmanaged
        {
            CreateMessage<T>() = message;
        }

        public ref T CreateMessage<T>() where T : unmanaged
        {
            return ref *GetPipe().AllocateMessage<T>();
        }

        public void* CreateMessageDynamic(long typeHash, int sizeInBytes, int alignInBytes)
        {
            return GetPipe().AllocateMessage(typeHash, sizeInBytes, alignInBytes);
        }

        public PipeSpan<T> CreatePipeSpan<T>(int elementCount) where T : unmanaged
        {
            return new PipeSpan<T>
            {
                m_ptr    = (T*)GetPipe().AllocateData(UnsafeUtility.SizeOf<T>() * elementCount, UnsafeUtility.AlignOf<T>()),
                m_length = elementCount
            };
        }
    }

    public unsafe struct CommandPipeReader
    {
        [ReadOnly] internal NativeReference<UnsafeList<MegaPipe> > m_perThreadPipes;

        public Enumerator<T> Each<T>() where T : unmanaged
        {
            return new Enumerator<T>
            {
                m_perThreadPipes     = m_perThreadPipes,
                m_currentThreadIndex = -1
            };
        }

        public EnumeratorUntyped Each(long typeHash)
        {
            return new EnumeratorUntyped
            {
                m_perThreadPipes     = m_perThreadPipes,
                m_typeHash           = typeHash,
                m_currentThreadIndex = -1
            };
        }

        public struct Enumerator<T> where T : unmanaged
        {
            [ReadOnly] internal NativeReference<UnsafeList<MegaPipe> > m_perThreadPipes;
            internal int                                               m_currentThreadIndex;
            MegaPipe.Enumerator                                        m_pipeEnumerator;

            public Enumerator<T> GetEnumerator() => this;
            public ref T Current => ref *(T*)m_pipeEnumerator.Current;
            public bool MoveNext()
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                // This forces a safety check.
                if (m_currentThreadIndex >= m_perThreadPipes.Value.Length)
                    return false;
#endif

                if (m_pipeEnumerator.MoveNext())
                    return true;

                m_currentThreadIndex++;
                for (; m_currentThreadIndex < m_perThreadPipes.Value.Length; m_currentThreadIndex++)
                {
                    ref var pipe = ref m_perThreadPipes.Value.ElementAt(m_currentThreadIndex);
                    if (!pipe.isCreated)
                        continue;
                    var candidateEnumerator = pipe.GetEnumerator(BurstRuntime.GetHashCode64<T>());
                    if (candidateEnumerator.MoveNext())
                    {
                        m_pipeEnumerator = candidateEnumerator;
                        return true;
                    }
                }

                return false;
            }
        }

        public struct EnumeratorUntyped
        {
            [ReadOnly] internal NativeReference<UnsafeList<MegaPipe> > m_perThreadPipes;
            internal long                                              m_typeHash;
            internal int                                               m_currentThreadIndex;
            MegaPipe.Enumerator                                        m_pipeEnumerator;

            public EnumeratorUntyped GetEnumerator() => this;
            public void* Current => m_pipeEnumerator.Current;
            public bool MoveNext()
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                // This forces a safety check.
                if (m_currentThreadIndex >= m_perThreadPipes.Value.Length)
                    return false;
#endif

                if (m_pipeEnumerator.MoveNext())
                    return true;

                m_currentThreadIndex++;
                for (; m_currentThreadIndex < m_perThreadPipes.Value.Length; m_currentThreadIndex++)
                {
                    ref var pipe = ref m_perThreadPipes.Value.ElementAt(m_currentThreadIndex);
                    if (!pipe.isCreated)
                        continue;
                    var candidateEnumerator = pipe.GetEnumerator(m_typeHash);
                    if (candidateEnumerator.MoveNext())
                    {
                        m_pipeEnumerator = candidateEnumerator;
                        return true;
                    }
                }

                return false;
            }
        }
    }
}

