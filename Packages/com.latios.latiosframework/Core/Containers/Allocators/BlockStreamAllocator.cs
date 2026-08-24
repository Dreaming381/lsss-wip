using System;
using System.Runtime.InteropServices;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Latios.Unsafe
{
    /// <summary>
    /// A bump allocator which suballocates from large blocks of memory acquired from a backing allocator.
    /// The struct is sized to a cache line so that it can be stored in an array of per-thread allocators.
    /// This is a low-level API intended for use by higher-level custom collections.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = JobsUtility.CacheLineSize)]  // Force to 8-byte alignment
    public unsafe struct BlockStreamAllocator : IDisposable
    {
        internal struct BlockPtr
        {
            public byte* ptr;
            public int   byteCount;
        }

        internal UnsafeList<BlockPtr>             blocks;
        internal byte*                            nextFreeAddress;
        internal int                              bytesRemainingInBlock;
        internal int                              minimumBlockSize;
        internal AllocatorManager.AllocatorHandle allocator;

        /// <summary>
        /// Creates a new BlockStreamAllocator. No memory blocks are acquired until the first allocation.
        /// </summary>
        /// <param name="allocator">The backing allocator to acquire memory blocks from</param>
        /// <param name="minimumBlockSize">The minimum number of bytes in a block</param>
        public BlockStreamAllocator(AllocatorManager.AllocatorHandle allocator, int minimumBlockSize = 1024 * 16)
        {
            blocks                = default;
            nextFreeAddress       = null;
            bytesRemainingInBlock = 0;
            this.minimumBlockSize = minimumBlockSize;
            this.allocator        = allocator;
        }

        /// <summary>
        /// Returns true if this instance currently owns any memory blocks. This is false until the
        /// first allocation is made, and false again after Dispose().
        /// </summary>
        public bool hasAllocatedBlocks => blocks.IsCreated;

        /// <summary>
        /// Allocates multiple contiguous elements of T. The memory is left uninitialized.
        /// </summary>
        /// <typeparam name="T">The type of element to allocate</typeparam>
        /// <param name="count">The number of elements to allocate</param>
        /// <returns>A pointer to the first element allocated</returns>
        public T* Allocate<T>(int count) where T : unmanaged
        {
            var neededBytes = UnsafeUtility.SizeOf<T>() * count;
            return (T*)Allocate(neededBytes, UnsafeUtility.AlignOf<T>());
        }

        /// <summary>
        /// Allocates raw memory. The memory is left uninitialized.
        /// </summary>
        /// <param name="sizeInBytes">The number of bytes to allocate</param>
        /// <param name="alignInBytes">The alignment of the allocation</param>
        /// <returns>A pointer to the allocated memory</returns>
        public void* Allocate(int sizeInBytes, int alignInBytes)
        {
            var neededBytes = sizeInBytes;
            if (Hint.Unlikely(!CollectionHelper.IsAligned(nextFreeAddress, alignInBytes)))
            {
                var newAddress         = (byte*)CollectionHelper.Align((ulong)nextFreeAddress, (ulong)alignInBytes);
                var diff               = newAddress - nextFreeAddress;
                bytesRemainingInBlock -= (int)diff;
                nextFreeAddress        = newAddress;
            }

            if (Hint.Unlikely(neededBytes > bytesRemainingInBlock))
            {
                if (Hint.Unlikely(!blocks.IsCreated))
                {
                    blocks = new UnsafeList<BlockPtr>(8, allocator);
                }
                var blockSize = math.max(neededBytes, minimumBlockSize);
                var newBlock  = new BlockPtr
                {
                    byteCount = blockSize,
                    ptr       = AllocatorManager.Allocate<byte>(allocator, blockSize)
                };
                UnityEngine.Debug.Assert(CollectionHelper.IsAligned(newBlock.ptr, alignInBytes));
                blocks.Add(newBlock);
                nextFreeAddress       = newBlock.ptr;
                bytesRemainingInBlock = blockSize;
            }

            var result             = nextFreeAddress;
            bytesRemainingInBlock -= neededBytes;
            nextFreeAddress       += neededBytes;
            return result;
        }

        /// <summary>
        /// Takes ownership of all memory blocks owned by another instance, leaving that instance empty
        /// but still valid for future allocations. Suballocations made from either instance remain valid
        /// and are released when this instance is disposed. You are responsible for ensuring the allocators
        /// are compatible.
        /// </summary>
        /// <param name="other">The instance whose memory blocks should be transferred to this instance</param>
        public void ConcatenateFrom(ref BlockStreamAllocator other)
        {
            if (!other.blocks.IsCreated)
                return;

            if (!blocks.IsCreated)
                blocks = other.blocks;
            else
            {
                blocks.AddRange(other.blocks);
                other.blocks.Dispose();
            }
            nextFreeAddress       = other.nextFreeAddress;
            bytesRemainingInBlock = other.bytesRemainingInBlock;

            other.blocks                = default;
            other.nextFreeAddress       = null;
            other.bytesRemainingInBlock = 0;
        }

        /// <summary>
        /// Releases all memory blocks acquired by this allocator. Does nothing if no allocation was ever made.
        /// </summary>
        public void Dispose()
        {
            if (!blocks.IsCreated)
                return;

            foreach (var block in blocks)
                AllocatorManager.Free(allocator, block.ptr, block.byteCount);
            blocks.Dispose();
            nextFreeAddress       = null;
            bytesRemainingInBlock = 0;
        }
    }
}

