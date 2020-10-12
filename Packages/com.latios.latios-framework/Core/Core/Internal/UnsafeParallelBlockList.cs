using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;

namespace Latios
{
    internal unsafe struct UnsafeParallelBlockList : INativeDisposable
    {
        public readonly int  m_elementSize;
        private readonly int m_blockSize;
        private readonly int m_elementsPerBlock;
        private Allocator    m_allocator;

        [NativeSetThreadIndex]
        private int m_threadIndex;

        private PerThreadBlockList* m_perThreadBlockLists;

        public UnsafeParallelBlockList(int elementSize, int elementsPerBlock, Allocator allocator)
        {
            m_elementSize      = elementSize;
            m_elementsPerBlock = elementsPerBlock;
            m_blockSize        = elementSize * elementsPerBlock;
            m_allocator        = allocator;
            m_threadIndex      = 0;

            m_perThreadBlockLists = (PerThreadBlockList*)UnsafeUtility.Malloc(64 * JobsUtility.MaxJobThreadCount, 64, allocator);
            for (int i = 0; i < JobsUtility.MaxJobThreadCount; i++)
            {
                m_perThreadBlockLists[i].lastByteAddressInBlock = null;
                m_perThreadBlockLists[i].nextWriteAddress       = null;
                m_perThreadBlockLists[i].nextWriteAddress++;
                m_perThreadBlockLists[i].elementCount = 0;
            }
        }

        public void Write<T>(T value) where T : unmanaged
        {
            var blockList = m_perThreadBlockLists + m_threadIndex;
            if (blockList->nextWriteAddress > blockList->lastByteAddressInBlock)
            {
                if (blockList->elementCount == 0)
                {
                    blockList->blocks = new UnsafeList(m_allocator);
                    blockList->blocks.SetCapacity<BlockPtr>(10);
                }
                BlockPtr newBlockPtr = new BlockPtr
                {
                    ptr = (byte*)UnsafeUtility.Malloc(m_blockSize, UnsafeUtility.AlignOf<T>(), m_allocator)
                };
                blockList->nextWriteAddress       = newBlockPtr.ptr;
                blockList->lastByteAddressInBlock = newBlockPtr.ptr + m_blockSize - 1;
                blockList->blocks.Add(newBlockPtr);
            }

            UnsafeUtility.CopyStructureToPtr(ref value, blockList->nextWriteAddress);
            blockList->nextWriteAddress += m_elementSize;
            blockList->elementCount++;
        }

        public int Count
        {
            get
            {
                int result = 0;
                for (int i = 0; i < JobsUtility.MaxJobThreadCount; i++)
                {
                    result += m_perThreadBlockLists[i].elementCount;
                }
                return result;
            }
        }

        public struct ElementPtr
        {
            public byte* ptr;
        }

        public void GetElementPtrs(NativeArray<ElementPtr> ptrs)
        {
            int dst = 0;

            for (int threadBlockId = 0; threadBlockId < JobsUtility.MaxJobThreadCount; threadBlockId++)
            {
                var blockList = m_perThreadBlockLists + threadBlockId;
                if (blockList->elementCount > 0)
                {
                    int src = 0;
                    for (int blockId = 0; blockId < blockList->blocks.Length - 1; blockId++)
                    {
                        var address = ((BlockPtr*)blockList->blocks.Ptr)[blockId].ptr;
                        for (int i = 0; i < m_elementsPerBlock; i++)
                        {
                            ptrs[dst] = new ElementPtr { ptr  = address };
                            address                          += m_elementSize;
                            src++;
                            dst++;
                        }
                    }
                    {
                        var address = ((BlockPtr*)blockList->blocks.Ptr)[blockList->blocks.Length - 1].ptr;
                        for (int i = src; i < blockList->elementCount; i++)
                        {
                            ptrs[dst] = new ElementPtr { ptr  = address };
                            address                          += m_elementSize;
                            dst++;
                        }
                    }
                }
            }
        }

        [BurstCompile]
        struct DisposeJob : IJob
        {
            public UnsafeParallelBlockList upbl;

            public void Execute()
            {
                upbl.Dispose();
            }
        }

        public JobHandle Dispose(JobHandle inputDeps)
        {
            var jh = new DisposeJob { upbl = this }.Schedule(inputDeps);
            m_perThreadBlockLists          = null;
            return jh;
        }

        public void Dispose()
        {
            for (int i = 0; i < JobsUtility.MaxJobThreadCount; i++)
            {
                if (m_perThreadBlockLists[i].elementCount > 0)
                {
                    for (int j = 0; j < m_perThreadBlockLists[i].blocks.Length; j++)
                    {
                        BlockPtr* blockPtrArray = (BlockPtr*)m_perThreadBlockLists[i].blocks.Ptr;
                        UnsafeUtility.Free(blockPtrArray[j].ptr, m_allocator);
                    }
                    m_perThreadBlockLists[i].blocks.Dispose();
                }
            }
            UnsafeUtility.Free(m_perThreadBlockLists, m_allocator);
        }

        private struct BlockPtr
        {
            public byte* ptr;
        }

        [StructLayout(LayoutKind.Sequential, Size = 64)]
        private struct PerThreadBlockList
        {
            public UnsafeList blocks;
            public byte*      nextWriteAddress;
            public byte*      lastByteAddressInBlock;
            public int        elementCount;
        }

        void SizeCheck()
        {
            var res = sizeof(UnsafeList);
        }
    }
}

