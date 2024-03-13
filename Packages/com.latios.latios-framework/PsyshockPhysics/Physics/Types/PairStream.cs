using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace Latios.Psyshock
{
    public unsafe struct StreamSpan<T> where T : unmanaged
    {
        public int length => m_length;
        public T* GetUnsafePtr() => m_ptr;

        public ref T this[int index] => ref AsSpan()[index];

        public Span<T>.Enumerator GetEnumerator() => AsSpan().GetEnumerator();

        public Span<T> AsSpan() => new Span<T>(m_ptr, length);

        internal T*  m_ptr;
        internal int m_length;
    }

    [NativeContainer]
    public unsafe struct PairStream : INativeDisposable
    {
        public JobHandle Dispose(JobHandle inputDeps)
        {
            throw new System.NotImplementedException();
        }

        public void Dispose()
        {
            throw new System.NotImplementedException();
        }
    }
}

