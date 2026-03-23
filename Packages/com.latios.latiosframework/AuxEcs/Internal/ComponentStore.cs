using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Latios.AuxEcs
{
    internal unsafe struct ComponentStore : IDisposable
    {
        UnsafePtrList<byte> chunkPtrs;
        UnsafePtrList<int>  chunkVersionPtrs;
        UnsafeList<int>     freelist;
        int                 elementsPerChunk;
        int                 elementSize;
        int                 elementAlignment;
        int                 elementCount;
        // VPtr disposePtr;

        public ComponentStore(int elementSize, int elementAlignment, AllocatorManager.AllocatorHandle allocator)
        {
            chunkPtrs             = new UnsafePtrList<byte>(8, allocator);
            chunkVersionPtrs      = new UnsafePtrList<int>(8, allocator);
            freelist              = new UnsafeList<int>(16, allocator);
            elementsPerChunk      = CollectionHelper.Align(math.max(1, 1024 / elementSize), 16);
            this.elementSize      = elementSize;
            this.elementAlignment = elementAlignment;
            elementCount          = 0;
        }

        public void Dispose()
        {
            var allocator = chunkPtrs.Allocator;
            foreach (var chunk in chunkPtrs)
            {
                AllocatorManager.Free(allocator, chunk.ToPointer(), elementSize, elementAlignment, elementsPerChunk);
            }
            foreach (var chunk in chunkVersionPtrs)
            {
                AllocatorManager.Free(allocator, chunk.ToPointer(), UnsafeUtility.SizeOf<int>(), UnsafeUtility.AlignOf<int>(), elementsPerChunk);
            }
            chunkPtrs.Dispose();
            chunkVersionPtrs.Dispose();
        }

        public int instanceCount => elementCount;
        public int maxIndex => elementsPerChunk * chunkPtrs.Length;

        public int Add()
        {
            if (freelist.IsEmpty)
            {
                var nextFreeIndexInChunk = elementCount % elementsPerChunk;
                if (nextFreeIndexInChunk == 0)
                {
                    // Allocate new chunk
                    var allocator = chunkPtrs.Allocator;
                    chunkPtrs.Add(AllocatorManager.Allocate(allocator, elementSize, elementAlignment, elementsPerChunk));
                    var versionPtr = AllocatorManager.Allocate(allocator, UnsafeUtility.SizeOf<int>(), UnsafeUtility.AlignOf<int>(), elementsPerChunk);
                    UnsafeUtility.MemClear(versionPtr, UnsafeUtility.SizeOf<int>() * elementsPerChunk);
                    *(int*)versionPtr = 1;
                    chunkVersionPtrs.Add(versionPtr);
                    var result = elementCount;
                    elementCount++;
                    return result;
                }
                else
                {
                    var versionPtr = chunkVersionPtrs[chunkVersionPtrs.Length - 1];
                    versionPtr[nextFreeIndexInChunk]++;
                    var result = elementCount;
                    elementCount++;
                    return result;
                }
            }
            else
            {
                int result = freelist[freelist.Length - 1];
                freelist.Length--;
                int chunkIndex   = result / elementsPerChunk;
                int indexInChunk = result % elementsPerChunk;
                var versionPtr   = chunkVersionPtrs[chunkIndex];
                versionPtr[indexInChunk]++;
                elementCount++;
                return result;
            }
        }

        public void Remove(int index)
        {
            int chunkIndex   = index / elementsPerChunk;
            int indexInChunk = index % elementsPerChunk;
            var versionPtr   = chunkVersionPtrs[chunkIndex];
            versionPtr[indexInChunk]++;
            elementCount--;
            freelist.Add(index);
        }

        public void Replace(int index)
        {
            // Todo: Dispose existing if required.
            //int chunkIndex            = index / elementsPerChunk;
            //int indexInChunk          = index % elementsPerChunk;
        }

        public AuxRef<T> GetRef<T>(int index) where T : unmanaged
        {
            int chunkIndex   = index / elementsPerChunk;
            int indexInChunk = index % elementsPerChunk;
            var versionPtr   = chunkVersionPtrs[chunkIndex] + indexInChunk;
            var componentPtr = (T*)chunkPtrs[chunkIndex] + indexInChunk;
            return new AuxRef<T>
            {
                componentPtr = componentPtr,
                versionPtr   = versionPtr,
                version      = *versionPtr
            };
        }
    }
}

