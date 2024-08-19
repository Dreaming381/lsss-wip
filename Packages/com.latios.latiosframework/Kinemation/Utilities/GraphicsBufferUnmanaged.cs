using System;
using System.Diagnostics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace Latios.Kinemation
{
    public struct GraphicsBufferUnmanaged : IDisposable
    {
        internal int index;
        internal int version;

        public static GraphicsBufferUnmanaged Null => default;

        public GraphicsBufferUnmanaged(GraphicsBuffer.Target target, GraphicsBuffer.UsageFlags usageFlags, int count, int stride)
        {
            this = GraphicsUnmanaged.CreateGraphicsBuffer(target, usageFlags, count, stride);
        }

        public bool IsValid() => GraphicsUnmanaged.IsValid(this);

        public GraphicsBuffer ToManaged() => IsValid() ? GraphicsUnmanaged.GetAtIndex(index) : null;

        public void Dispose()
        {
            if (IsValid())
                GraphicsUnmanaged.DisposeGraphicsBuffer(this);
        }

        public NativeArray<T> LockBufferForWrite<T>(int bufferStartIndex, int count) where T : unmanaged
        {
            CheckValid();
            var size   = UnsafeUtility.SizeOf<T>();
            var result = GraphicsUnmanaged.GraphicsBufferLockForWrite(this, bufferStartIndex * size, count * size);
            return result.Reinterpret<T>(1);
        }

        public void UnlockBufferAfterWrite<T>(int countWritten) where T : unmanaged
        {
            CheckValid();
            var size = UnsafeUtility.SizeOf<T>();
            GraphicsUnmanaged.GraphicsBufferUnlockAfterWrite(this, countWritten * size);
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        internal void CheckValid()
        {
            if (!IsValid())
                throw new NullReferenceException("The GraphicsBufferUnmanaged is not valid.");
        }
    }
}

