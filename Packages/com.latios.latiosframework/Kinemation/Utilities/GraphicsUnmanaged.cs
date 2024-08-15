using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using AOT;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace Latios.Kinemation
{
    public static unsafe class GraphicsUnmanaged
    {
        public static void Initialize()
        {
            if (initialized)
                return;

            buffers               = new List<GraphicsBuffer>();
            handles.Data.freeList = new UnsafeList<int>(128, Allocator.Persistent);
            handles.Data.versions = new UnsafeList<int>(128, Allocator.Persistent);

            managedDelegate                 = ManagedExecute;
            handles.Data.managedFunctionPtr = new FunctionPointer<ManagedDelegate>(Marshal.GetFunctionPointerForDelegate<ManagedDelegate>(ManagedExecute));

            initialized = true;

            // important: this will always be called from a special unload thread (main thread will be blocking on this)
            AppDomain.CurrentDomain.DomainUnload += (_, __) => { Shutdown(); };

            // There is no domain unload in player builds, so we must be sure to shutdown when the process exits.
            AppDomain.CurrentDomain.ProcessExit += (_, __) => { Shutdown(); };
        }

        #region Internal
        internal static GraphicsBufferUnmanaged CreateGraphicsBuffer(GraphicsBuffer.Target target, GraphicsBuffer.UsageFlags usageFlags, int count, int stride)
        {
            int  listIndex    = -1;
            bool appendToList = false;
            if (handles.Data.freeList.IsEmpty)
            {
                listIndex = handles.Data.versions.Length;
                handles.Data.versions.Add(1);
                appendToList = true;
            }
            else
            {
                listIndex = handles.Data.freeList[handles.Data.versions.Length - 1];
                handles.Data.freeList.Length--;
                handles.Data.versions[listIndex]++;
                appendToList = false;
            }
            var context = new GraphicsBufferCreateContext
            {
                target       = target,
                usageFlags   = usageFlags,
                count        = count,
                stride       = stride,
                listIndex    = listIndex,
                appendToList = appendToList,
                success      = false
            };
            handles.Data.managedFunctionPtr.Invoke((IntPtr)(&context), 1);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (!context.success)
                throw new System.InvalidOperationException("Creating the GraphicsBufferUnmanaged failed.");
#endif
            return new GraphicsBufferUnmanaged { index = listIndex, version = handles.Data.versions[listIndex] };
        }

        internal static void DisposeGraphicsBuffer(GraphicsBufferUnmanaged unmanaged)
        {
            var context = new GraphicsBufferDisposeContext { listIndex = unmanaged.index };
            handles.Data.managedFunctionPtr.Invoke((IntPtr)(&context), 2);
        }

        internal static bool IsValid(GraphicsBufferUnmanaged unmanaged)
        {
            return unmanaged.version == handles.Data.versions[unmanaged.index];
        }

        internal static GraphicsBuffer GetAtIndex(int index) => buffers[index];

        internal static NativeArray<byte> GraphicsBufferLockForWrite(GraphicsBufferUnmanaged unmanaged, int byteOffset, int byteCount)
        {
            var context = new GraphicsBufferLockForWriteContext
            {
                bytes      = default,
                listIndex  = unmanaged.index,
                byteOffset = byteOffset,
                byteCount  = byteCount,
                success    = false
            };
            handles.Data.managedFunctionPtr.Invoke((IntPtr)(&context), 3);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (!context.success)
                throw new System.InvalidOperationException("Locking the GraphicsBufferUnmanaged for write failed.");
#endif
            return context.bytes;
        }

        internal static void GraphicsBufferUnlockAfterWrite(GraphicsBufferUnmanaged unmanaged, int byteCount)
        {
            var context = new GraphicsBufferUnlockAfterWriteContext
            {
                listIndex = unmanaged.index,
                byteCount = byteCount,
                success   = false
            };
            handles.Data.managedFunctionPtr.Invoke((IntPtr)(&context), 4);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (!context.success)
                throw new System.InvalidOperationException("Unlocking the GraphicsBufferUnmanaged after write failed.");
#endif
        }
        #endregion

        #region State
        static List<GraphicsBuffer>              buffers;
        static readonly SharedStatic<HandleData> handles     = SharedStatic<HandleData>.GetOrCreate<HandleData>();
        static bool                              initialized = false;

        private delegate void ManagedDelegate(IntPtr context, int operation);
        static ManagedDelegate managedDelegate;

        struct HandleData
        {
            public UnsafeList<int>                  versions;
            public UnsafeList<int>                  freeList;
            public FunctionPointer<ManagedDelegate> managedFunctionPtr;
        }

        static void Shutdown()
        {
            if (!initialized)
                return;
            foreach (var buffer in buffers)
                buffer.Dispose();
            buffers = null;
            handles.Data.freeList.Dispose();
            handles.Data.versions.Dispose();
            managedDelegate = null;
            initialized     = false;
        }
        #endregion

        #region Contexts
        // Code 1
        struct GraphicsBufferCreateContext
        {
            public GraphicsBuffer.Target     target;
            public GraphicsBuffer.UsageFlags usageFlags;
            public int                       count;
            public int                       stride;
            public int                       listIndex;
            public bool                      appendToList;
            public bool                      success;
        }

        // Code 2
        struct GraphicsBufferDisposeContext
        {
            public int listIndex;
        }

        // Code 3
        struct GraphicsBufferLockForWriteContext
        {
            public NativeArray<byte> bytes;
            public int               listIndex;
            public int               byteOffset;
            public int               byteCount;
            public bool              success;
        }

        // Code 4
        struct GraphicsBufferUnlockAfterWriteContext
        {
            public int  listIndex;
            public int  byteCount;
            public bool success;
        }
        #endregion

        [MonoPInvokeCallback(typeof(ManagedDelegate))]
        static void ManagedExecute(IntPtr context, int operation)
        {
            switch (operation)
            {
                case 1:
                {
                    ref var ctx = ref *(GraphicsBufferCreateContext*)context;
                    if (ctx.appendToList)
                        buffers.Add(default); // Gaurd against desync if constructor throws

                    var buffer             = new GraphicsBuffer(ctx.target, ctx.usageFlags, ctx.count, ctx.stride);
                    buffers[ctx.listIndex] = buffer;
                    ctx.success            = true;
                    break;
                }
                case 2:
                {
                    var index = ((GraphicsBufferDisposeContext*)context)->listIndex;
                    buffers[index].Dispose();
                    buffers[index] = null;
                    break;
                }
                case 3:
                {
                    ref var ctx    = ref *(GraphicsBufferLockForWriteContext*)context;
                    var     buffer = buffers[ctx.listIndex];
                    ctx.bytes      = buffer.LockBufferForWrite<byte>(ctx.byteOffset, ctx.byteCount);
                    ctx.success    = true;
                    break;
                }
                case 4:
                {
                    ref var ctx    = ref *(GraphicsBufferUnlockAfterWriteContext*)context;
                    var     buffer = buffers[ctx.listIndex];
                    buffer.UnlockBufferAfterWrite<byte>(ctx.byteCount);
                    ctx.success = true;
                    break;
                }
            }
        }
    }
}

