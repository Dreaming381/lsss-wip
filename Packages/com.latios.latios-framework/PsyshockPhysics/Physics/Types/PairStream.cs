using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Latios.Unsafe;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
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
        #region Create and Destroy
        public PairStream(int3 worldSubdivisionsPerAxis,
                          AllocatorManager.AllocatorHandle allocator) : this(worldSubdivisionsPerAxis.x * worldSubdivisionsPerAxis.y * worldSubdivisionsPerAxis.z + 1, allocator)
        {
        }

        public PairStream(in CollisionLayerSettings settings, AllocatorManager.AllocatorHandle allocator) : this(settings.worldSubdivisionsPerAxis + 1, allocator)
        {
        }

        public PairStream(in CollisionLayer layerWithSettings, AllocatorManager.AllocatorHandle allocator) : this(layerWithSettings.bucketCount, allocator)
        {
        }

        public PairStream(int bucketCountExcludingNan, AllocatorManager.AllocatorHandle allocator)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            CheckAllocator(allocator);

            m_Safety = CollectionHelper.CreateSafetyHandle(allocator);
            CollectionHelper.SetStaticSafetyId<PairStream>(ref m_Safety, ref s_staticSafetyId.Data);
            AtomicSafetyHandle.SetBumpSecondaryVersionOnScheduleWrite(m_Safety, true);
#endif

            // Parallel Unsafe Bipartite uses 3n - 2 threads where n is the number of cells plus the cross bucket.
            // However, 2n - 2 can fall into one of three different streams depending on write-access requirements.
            // A pair can fall into the cross bucket, the cell, or a mixed stream. Thus we are at 5n - 2 streams.
            // If we add the NaN bucket, we get 5n - 1. And if we reserve one extra slot for islanding, we get 5n.
            int totalStreams = 5 * math.max(bucketCountExcludingNan, 1);
            data             = new SharedContainerData
            {
                pairHeaders         = new UnsafeIndexedBlockList(UnsafeUtility.SizeOf<PairHeader>(), 4096 / UnsafeUtility.SizeOf<PairHeader>(), totalStreams, allocator),
                blockStreamArray    = AllocatorManager.Allocate<BlockStream>(allocator, totalStreams),
                state               = AllocatorManager.Allocate<State>(allocator),
                expectedBucketCount = bucketCountExcludingNan,
                allocator           = allocator
            };

            *data.state = default;

            for (int i = 0; i < data.pairHeaders.indexCount; i++)
                data.blockStreamArray[i] = default;
        }

        /// <summary>
        /// Disposes the PairStream after the jobs which use it have finished.
        /// </summary>
        /// <param name="inputDeps">The JobHandle for any jobs previously using this PairStream</param>
        /// <returns>The JobHandle for the disposing job scheduled, or inputDeps if no job was scheduled</returns>
        public JobHandle Dispose(JobHandle inputDeps)
        {
            var jobHandle = new DisposeJob
            {
                blockList    = data.pairHeaders,
                state        = data.state,
                blockStreams = data.blockStreamArray,
                allocator    = data.allocator,
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                m_Safety = m_Safety
#endif
            }.Schedule(inputDeps);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.Release(m_Safety);
#endif
            this = default;
            return jobHandle;
        }

        /// <summary>
        /// Disposes the EntityOperationCommandBuffer
        /// </summary>
        public void Dispose()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            CollectionHelper.DisposeSafetyHandle(ref m_Safety);
#endif
            Deallocate(data.state, data.pairHeaders, data.blockStreamArray, data.allocator);
            this = default;
        }
        #endregion

        #region Public API
        public ref T AddPairAndGetRef<T>(Entity entityA, int bucketA, bool aIsRW, Entity entityB, int bucketB, bool bIsRW, out Pair pair) where T : unmanaged
        {
            var root = AddPairImpl(entityA,
                                   bucketA,
                                   aIsRW,
                                   entityB,
                                   bucketB,
                                   bIsRW,
                                   UnsafeUtility.SizeOf<T>(),
                                   UnsafeUtility.AlignOf<T>(),
                                   BurstRuntime.GetHashCode32<T>(),
                                   false,
                                   out pair);
            pair.header->rootPtr = root;
            return ref UnsafeUtility.AsRef<T>(root);
        }

        public void* AddPairRaw(Entity entityA, int bucketA, bool aIsRW, Entity entityB, int bucketB, bool bIsRW, int sizeInBytes, int alignInBytes, out Pair pair)
        {
            return AddPairImpl(entityA, bucketA, aIsRW, entityB, bucketB, bIsRW, sizeInBytes, alignInBytes, 0, true, out pair);
        }

        public ref T AddPairFromOtherStreamAndGetRef<T>(in Pair pairFromOtherStream, out Pair pairInThisStream) where T : unmanaged
        {
            return ref AddPairAndGetRef<T>(pairFromOtherStream.entityA,
                                           pairFromOtherStream.index,
                                           pairFromOtherStream.aIsRW,
                                           pairFromOtherStream.entityB,
                                           pairFromOtherStream.index,
                                           pairFromOtherStream.bIsRW,
                                           out pairInThisStream);
        }

        public void* AddPairFromOtherStreamRaw(in Pair pairFromOtherStream, int sizeInBytes, int alignInBytes, out Pair pairInThisStream)
        {
            return AddPairRaw(pairFromOtherStream.entityA,
                              pairFromOtherStream.index,
                              pairFromOtherStream.aIsRW,
                              pairFromOtherStream.entityB,
                              pairFromOtherStream.index,
                              pairFromOtherStream.bIsRW,
                              sizeInBytes,
                              alignInBytes,
                              out pairInThisStream);
        }

        public void ConcatenateFrom(ref PairStream pairStreamToStealFrom)
        {
            CheckWriteAccess();
            pairStreamToStealFrom.CheckWriteAccess();
            CheckStreamsMatch(ref pairStreamToStealFrom);

            data.state->enumeratorVersion++;
            data.state->pairPtrVersion++;
            pairStreamToStealFrom.data.state->enumeratorVersion++;
            pairStreamToStealFrom.data.state->pairPtrVersion++;

            data.pairHeaders.ConcatenateAndStealFromUnordered(ref pairStreamToStealFrom.data.pairHeaders);
            for (int i = 0; i < data.pairHeaders.indexCount; i++)
            {
                ref var stream      = ref data.blockStreamArray[i];
                ref var otherStream = ref pairStreamToStealFrom.data.blockStreamArray[i];
                if (!stream.blocks.IsCreated)
                {
                    stream = otherStream;
                }
                else if (otherStream.blocks.IsCreated)
                {
                    stream.blocks.AddRange(otherStream.blocks);
                    stream.bytesRemainingInBlock = otherStream.bytesRemainingInBlock;
                    stream.nextFreeAddress       = otherStream.nextFreeAddress;
                    otherStream.blocks.Clear();
                    otherStream.bytesRemainingInBlock = 0;
                }
            }
        }

        public ParallelWriter AsParallelWriter()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            var safety = m_Safety;
            CollectionHelper.SetStaticSafetyId<ParallelWriter>(ref safety, ref ParallelWriter.s_staticSafetyId.Data);
#endif
            return new ParallelWriter
            {
                data        = data,
                threadIndex = -1,
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                m_Safety = safety,
#endif
            };
        }
        #endregion

        #region Public Types
        [NativeContainer]
        public partial struct Pair
        {
            public StreamSpan<T> Allocate<T>(int count) where T : unmanaged
            {
                CheckWriteAccess();
                CheckPairPtrVersionMatches(data.state, version);
                ref var blocks                   = ref data.blockStreamArray[index];
                var     ptr                      = blocks.Allocate<T>(count, data.allocator);
                return new StreamSpan<T> { m_ptr = ptr, m_length = count };
            }

            public void* AllocateRaw(int sizeInBytes, int alignInBytes)
            {
                CheckWriteAccess();
                CheckPairPtrVersionMatches(data.state, version);
                if (sizeInBytes == 0)
                    return null;
                ref var blocks = ref data.blockStreamArray[index];
                return blocks.Allocate(sizeInBytes, alignInBytes, data.allocator);
            }

            public ref T ReplaceRef<T>() where T : unmanaged
            {
                WriteHeader().flags  &= (~PairHeader.kRootPtrIsRaw) & 0xff;
                header->rootPtr       = AllocateRaw(UnsafeUtility.SizeOf<T>(), UnsafeUtility.AlignOf<T>());
                header->rootTypeHash  = BurstRuntime.GetHashCode32<T>();
                return ref UnsafeUtility.AsRef<T>(header->rootPtr);
            }

            public void* ReplaceRaw(int sizeInBytes, int alignInBytes)
            {
                WriteHeader().flags  |= PairHeader.kRootPtrIsRaw;
                header->rootPtr       = AllocateRaw(sizeInBytes, alignInBytes);
                header->rootTypeHash  = 0;
                return header->rootPtr;
            }

            public ref T GetRef<T>() where T : unmanaged
            {
                var root = GetRaw();
                CheckTypeHash<T>();
                return ref UnsafeUtility.AsRef<T>(root);
            }

            public void* GetRaw() => WriteHeader().rootPtr;

            public ushort userUShort
            {
                get => ReadHeader().userUshort;
                set => WriteHeader().userUshort = value;
            }

            public byte userByte
            {
                get => ReadHeader().userByte;
                set => WriteHeader().userByte = value;
            }

            public bool enabled
            {
                get => (ReadHeader().flags & PairHeader.kEnabled) == PairHeader.kEnabled;
                set => WriteHeader().flags |= PairHeader.kEnabled;
            }

            public bool isRaw => (ReadHeader().flags & PairHeader.kRootPtrIsRaw) == PairHeader.kRootPtrIsRaw;

            public bool aIsRW => (ReadHeader().flags & PairHeader.kWritableA) == PairHeader.kWritableA;
            public bool bIsRW => (ReadHeader().flags & PairHeader.kWritableB) == PairHeader.kWritableB;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            /// <summary>
            /// A safe entity handle that can be used inside of PhysicsComponentLookup or PhysicsBufferLookup and corresponds to the
            /// owning entity of the first collider in the pair. It can also be implicitly casted and used as a normal entity reference.
            /// </summary>
            public SafeEntity entityA => new SafeEntity
            {
                m_entity = new Entity
                {
                    Index   = math.select(-header->entityA.Index - 1, header->entityA.Index, aIsRW && areEntitiesSafeInContext),
                    Version = header->entityA.Version
                }
            };
            /// <summary>
            /// A safe entity handle that can be used inside of PhysicsComponentLookup or PhysicsBufferLookup and corresponds to the
            /// owning entity of the second collider in the pair. It can also be implicitly casted and used as a normal entity reference.
            /// </summary>
            public SafeEntity entityB => new SafeEntity
            {
                m_entity = new Entity
                {
                    Index   = math.select(-header->entityB.Index - 1, header->entityB.Index, bIsRW && areEntitiesSafeInContext),
                    Version = header->entityB.Version
                }
            };
#else
            public SafeEntity entityA => new SafeEntity { m_entity = header->entityA };
            public SafeEntity entityB => new SafeEntity { m_entity = header->entityB };
#endif
        }

        [NativeContainer]  // Similar to FindPairsResult, keep this from escaping the local context
        public struct ParallelWriteKey
        {
            internal Entity entityA;
            internal Entity entityB;
            internal int    streamIndexA;
            internal int    streamIndexB;
            internal int    streamIndexCombined;
            internal int    expectedBucketCount;
        }

        [NativeContainer]
        [NativeContainerIsAtomicWriteOnly]
        public partial struct ParallelWriter
        {
            // Todo: Passing the key as an in parameter confuses the compiler.
            public ref T AddPairAndGetRef<T>(ParallelWriteKey key, bool aIsRW, bool bIsRW, out Pair pair) where T : unmanaged
            {
                var root = AddPairImpl(in key,
                                       aIsRW,
                                       bIsRW,
                                       UnsafeUtility.SizeOf<T>(),
                                       UnsafeUtility.AlignOf<T>(),
                                       BurstRuntime.GetHashCode32<T>(),
                                       false,
                                       out pair);
                pair.header->rootPtr = root;
                return ref UnsafeUtility.AsRef<T>(root);
            }

            public void* AddPairRaw(in ParallelWriteKey key, bool aIsRW, bool bIsRW, int sizeInBytes, int alignInBytes, out Pair pair)
            {
                return AddPairImpl(in key, aIsRW, bIsRW, sizeInBytes, alignInBytes, 0, true, out pair);
            }

            public ref T AddPairFromOtherStreamAndGetRef<T>(in Pair pairFromOtherStream, out Pair pairInThisStream) where T : unmanaged
            {
                CheckPairCanBeAddedInParallel(in pairFromOtherStream);
                var key = new ParallelWriteKey
                {
                    entityA             = pairFromOtherStream.entityA,
                    entityB             = pairFromOtherStream.entityB,
                    streamIndexA        = pairFromOtherStream.index,
                    streamIndexB        = pairFromOtherStream.index,
                    streamIndexCombined = pairFromOtherStream.index,
                    expectedBucketCount = pairFromOtherStream.data.expectedBucketCount
                };
                return ref AddPairAndGetRef<T>(key, pairFromOtherStream.aIsRW, pairFromOtherStream.bIsRW, out pairInThisStream);
            }

            public void* AddPairFromOtherStreamRaw(in Pair pairFromOtherStream, int sizeInBytes, int alignInBytes, out Pair pairInThisStream)
            {
                CheckPairCanBeAddedInParallel(in pairFromOtherStream);
                var key = new ParallelWriteKey
                {
                    entityA             = pairFromOtherStream.entityA,
                    entityB             = pairFromOtherStream.entityB,
                    streamIndexA        = pairFromOtherStream.index,
                    streamIndexB        = pairFromOtherStream.index,
                    streamIndexCombined = pairFromOtherStream.index,
                    expectedBucketCount = pairFromOtherStream.data.expectedBucketCount
                };
                return AddPairRaw(in key, pairFromOtherStream.aIsRW, pairFromOtherStream.bIsRW, sizeInBytes, alignInBytes, out pairInThisStream);
            }
        }
        #endregion

        #region Public Types Internal Members
        partial struct Pair
        {
            internal SharedContainerData data;
            internal int                 index;
            internal int                 version;
            internal PairHeader*         header;
            internal bool                isParallelKeySafe;
            internal bool                areEntitiesSafeInContext;

            ref PairHeader ReadHeader()
            {
                CheckReadAccess();
                CheckPairPtrVersionMatches(data.state, version);
                return ref *header;
            }

            ref PairHeader WriteHeader()
            {
                CheckWriteAccess();
                CheckPairPtrVersionMatches(data.state, version);
                return ref *header;
            }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            //Unfortunately this name is hardcoded into Unity. No idea how EntityCommandBuffer gets away with multiple safety handles.
            internal AtomicSafetyHandle m_Safety;
#endif

            [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
            void CheckWriteAccess()
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
            }

            [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
            void CheckReadAccess()
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
            }

            [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
            void CheckTypeHash<T>() where T : unmanaged
            {
                if ((header->flags & PairHeader.kRootPtrIsRaw) == PairHeader.kRootPtrIsRaw)
                    throw new InvalidOperationException(
                        $"Attempted to access a raw allocation using an explicit type. If this is intended, use GetRaw in combination with UnsafeUtility.AsRef.");
                if (header->rootTypeHash != BurstRuntime.GetHashCode32<T>())
                    throw new InvalidOperationException($"Attempted to access an allocation of a pair using the wrong type.");
            }
        }

        partial struct ParallelWriter
        {
            internal SharedContainerData data;

            [NativeSetThreadIndex]
            internal int threadIndex;

            bool needsAliasingChecks;
            bool needsIslanding;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            //Unfortunately this name is hardcoded into Unity. No idea how EntityCommandBuffer gets away with multiple safety handles.
            internal AtomicSafetyHandle m_Safety;

            internal static readonly SharedStatic<int> s_staticSafetyId = SharedStatic<int>.GetOrCreate<ParallelWriter>();
#endif

            void* AddPairImpl(in ParallelWriteKey key, bool aIsRW, bool bIsRW, int sizeInBytes, int alignInBytes, int typeHash, bool isRaw, out Pair pairAllocator)
            {
                CheckWriteAccess();
                CheckKeyCompatible(in key);

                // If for some reason the ParallelWriter was created and used in the same thread as an Enumerator,
                // We need to bump the version. But we don't want to do this if we are actually running in parallel.
                if (threadIndex == -1)
                    data.state->enumeratorVersion++;

                int targetStream;
                if (key.streamIndexA == key.streamIndexB)
                    targetStream = key.streamIndexA;
                else if (!bIsRW)
                    targetStream = key.streamIndexA;
                else if (!aIsRW)
                    targetStream = key.streamIndexB;
                else
                    targetStream = key.streamIndexCombined;

                // We can safely rely on eventual consistency here as this is a forced value write.
                // We only write the first time to avoid dirtying the cache line.
                if (!needsIslanding && targetStream == key.streamIndexCombined)
                {
                    needsIslanding             = true;
                    data.state->needsIslanding = true;
                }
                else if (!needsAliasingChecks && targetStream != key.streamIndexCombined)
                {
                    needsAliasingChecks          = true;
                    data.state->needsAliasChecks = true;
                }

                var headerPtr = (PairHeader*)data.pairHeaders.Allocate(targetStream);
                *   headerPtr = new PairHeader
                {
                    entityA      = key.entityA,
                    entityB      = key.entityB,
                    rootTypeHash = typeHash,
                    flags        =
                        (byte)((aIsRW ? PairHeader.kWritableA : default) + (bIsRW ? PairHeader.kWritableB : default) + PairHeader.kEnabled +
                               (isRaw ? PairHeader.kRootPtrIsRaw : default))
                };

                pairAllocator = new Pair
                {
                    data                     = data,
                    header                   = headerPtr,
                    index                    = targetStream,
                    version                  = data.state->pairPtrVersion,
                    isParallelKeySafe        = true,
                    areEntitiesSafeInContext = false,
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                    m_Safety = m_Safety,
#endif
                };

                var root           = pairAllocator.AllocateRaw(sizeInBytes, alignInBytes);
                headerPtr->rootPtr = root;
                return root;
            }

            [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
            void CheckWriteAccess()
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
            }

            [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
            void CheckKeyCompatible(in ParallelWriteKey key)
            {
                if (key.expectedBucketCount != data.expectedBucketCount)
                    throw new InvalidOperationException(
                        $"The key is generated from a different base bucket count {key.expectedBucketCount} from what the PairStream was constructed with {data.expectedBucketCount}");
            }

            [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
            void CheckPairCanBeAddedInParallel(in Pair pairFromOtherStream)
            {
                if (!pairFromOtherStream.isParallelKeySafe)
                    throw new InvalidOperationException(
                        $"The pair cannot be safely added to the ParallelWriter because the pair was created from an immediate operation. Add directly to the PairStream instead of the ParallelWriter.");
            }
        }
        #endregion

        #region Internal Types
        [StructLayout(LayoutKind.Sequential, Size = 32)]  // Force to 8-byte alignment
        internal struct PairHeader
        {
            public Entity entityA;
            public Entity entityB;
            public void*  rootPtr;
            public int    rootTypeHash;
            public ushort userUshort;
            public byte   userByte;
            public byte   flags;

            public const byte kWritableA    = 0x1;
            public const byte kWritableB    = 0x2;
            public const byte kEnabled      = 0x4;
            public const byte kRootPtrIsRaw = 0x8;
        }

        internal struct BlockPtr
        {
            public byte* ptr;
            public int   byteCount;
        }

        [StructLayout(LayoutKind.Sequential, Size = JobsUtility.CacheLineSize)]
        internal struct BlockStream
        {
            public UnsafeList<BlockPtr> blocks;
            public byte*                nextFreeAddress;
            public int                  bytesRemainingInBlock;

            public T* Allocate<T>(int count, AllocatorManager.AllocatorHandle allocator) where T : unmanaged
            {
                var neededBytes = UnsafeUtility.SizeOf<T>() * count;
                return (T*)Allocate(neededBytes, UnsafeUtility.AlignOf<T>(), allocator);
            }

            public void* Allocate(int sizeInBytes, int alignInBytes, AllocatorManager.AllocatorHandle allocator)
            {
                var neededBytes = sizeInBytes;
                if (Hint.Unlikely(!CollectionHelper.IsAligned(nextFreeAddress, alignInBytes)))
                {
                    var newAddress         = (byte*)CollectionHelper.Align((ulong)nextFreeAddress, (ulong)alignInBytes);
                    var diff               = newAddress - nextFreeAddress;
                    bytesRemainingInBlock -= (int)diff;
                }

                if (Hint.Unlikely(neededBytes > bytesRemainingInBlock))
                {
                    if (Hint.Unlikely(!blocks.IsCreated))
                    {
                        blocks = new UnsafeList<BlockPtr>(8, allocator);
                    }
                    var blockSize = math.max(neededBytes, 16 * 1024);
                    var newBlock  = new BlockPtr
                    {
                        byteCount = blockSize,
                        ptr       = AllocatorManager.Allocate<byte>(allocator, blockSize)
                    };
                    UnityEngine.Debug.Assert(CollectionHelper.IsAligned(newBlock.ptr, alignInBytes));
                    blocks.Add(newBlock);
                    nextFreeAddress       = newBlock.ptr;
                    bytesRemainingInBlock = neededBytes;
                }

                var result             = nextFreeAddress;
                bytesRemainingInBlock -= neededBytes;
                nextFreeAddress       += neededBytes;
                return result;
            }
        }

        internal struct State
        {
            public int  enumeratorVersion;
            public int  pairPtrVersion;
            public bool needsAliasChecks;
            public bool needsIslanding;
        }

        internal struct SharedContainerData
        {
            public UnsafeIndexedBlockList pairHeaders;

            [NativeDisableUnsafePtrRestriction]
            public BlockStream* blockStreamArray;

            [NativeDisableUnsafePtrRestriction]
            public State* state;

            public int                              expectedBucketCount;
            public AllocatorManager.AllocatorHandle allocator;
        }
        #endregion

        #region Internal Structure
        internal SharedContainerData data;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        //Unfortunately this name is hardcoded into Unity. No idea how EntityCommandBuffer gets away with multiple safety handles.
        public AtomicSafetyHandle m_Safety;

        static readonly SharedStatic<int> s_staticSafetyId = SharedStatic<int>.GetOrCreate<PairStream>();
#endif
        #endregion

        #region Internal Helpers
        internal int firstMixedBucketStream => 3 * data.expectedBucketCount;
        internal int nanBucketStream => 5 * data.expectedBucketCount - 2;
        internal int mixedIslandAggregateStream => 5 * data.expectedBucketCount - 1;

        void* AddPairImpl(Entity entityA,
                          int bucketA,
                          bool aIsRW,
                          Entity entityB,
                          int bucketB,
                          bool bIsRW,
                          int sizeInBytes,
                          int alignInBytes,
                          int typeHash,
                          bool isRaw,
                          out Pair pair)
        {
            CheckWriteAccess();
            CheckTargetBucketIsValid(bucketA);
            CheckTargetBucketIsValid(bucketB);

            data.state->enumeratorVersion++;

            int targetStream;
            if (bucketA == bucketB)
                targetStream = bucketA;
            else if (!bIsRW)
                targetStream = bucketA;
            else if (!aIsRW)
                targetStream = bucketB;
            else
                targetStream = firstMixedBucketStream;

            if (targetStream == firstMixedBucketStream)
                data.state->needsIslanding = true;
            else
                data.state->needsAliasChecks = true;

            var headerPtr = (PairHeader*)data.pairHeaders.Allocate(targetStream);
            *headerPtr    = new PairHeader
            {
                entityA      = entityA,
                entityB      = entityB,
                rootTypeHash = typeHash,
                flags        =
                    (byte)((aIsRW ? PairHeader.kWritableA : default) + (bIsRW ? PairHeader.kWritableB : default) + PairHeader.kEnabled +
                           (isRaw ? PairHeader.kRootPtrIsRaw : default))
            };

            pair = new Pair
            {
                data                     = data,
                header                   = headerPtr,
                index                    = targetStream,
                version                  = data.state->pairPtrVersion,
                isParallelKeySafe        = false,
                areEntitiesSafeInContext = false,
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                m_Safety = m_Safety,
#endif
            };

            var root           = pair.AllocateRaw(sizeInBytes, alignInBytes);
            headerPtr->rootPtr = root;
            return root;
        }

        private static void Deallocate(State* state, UnsafeIndexedBlockList blockList, BlockStream* blockStreams, AllocatorManager.AllocatorHandle allocator)
        {
            for (int i = 0; i < blockList.indexCount; i++)
            {
                var blockStream = blockStreams[i];
                if (blockStream.blocks.IsCreated)
                {
                    foreach (var block in blockStream.blocks)
                        AllocatorManager.Free(allocator, block.ptr, block.byteCount);
                    blockStream.blocks.Dispose();
                }
            }

            AllocatorManager.Free(allocator, blockStreams, blockList.indexCount);
            AllocatorManager.Free(allocator, state,        1);
            blockList.Dispose();
        }

        [BurstCompile]
        private struct DisposeJob : IJob
        {
            [NativeDisableUnsafePtrRestriction]
            public State* state;

            public UnsafeIndexedBlockList blockList;

            [NativeDisableUnsafePtrRestriction]
            public BlockStream* blockStreams;

            public AllocatorManager.AllocatorHandle allocator;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            internal AtomicSafetyHandle m_Safety;
#endif

            public void Execute()
            {
                Deallocate(state, blockList, blockStreams, allocator);
            }
        }
        #endregion

        #region Safety Checks
        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        void CheckWriteAccess()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        static void CheckAllocator(AllocatorManager.AllocatorHandle allocator)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (allocator.ToAllocator <= Allocator.None)
                throw new System.InvalidOperationException("Allocator cannot be Invalid or None");
#endif
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        void CheckTargetBucketIsValid(int bucket)
        {
            if (bucket < 0 || bucket > data.expectedBucketCount) // greater than because add 1 for NaN bucket
                throw new ArgumentOutOfRangeException($"The target bucket {bucket} is out of range of max buckets {data.expectedBucketCount}");
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        void CheckStreamsMatch(ref PairStream other)
        {
            if (data.expectedBucketCount != other.data.expectedBucketCount)
                throw new InvalidOperationException($"The streams do not have matching bucket counts: {data.expectedBucketCount} vs {other.data.expectedBucketCount}.");
            if (data.allocator != other.data.allocator)
                throw new InvalidOperationException($"The allocators are not the same. Memory stealing cannot be safely performed.");
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        static void CheckPairPtrVersionMatches(State* state, int version)
        {
            if (state->pairPtrVersion != version)
                throw new InvalidOperationException($"The pair allocator has been invalidated by a concatenate operation.");
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        static void CheckEnumerationVersionMatches(State* state, int version)
        {
            if (state->pairPtrVersion != version)
                throw new InvalidOperationException($"The enumerator has been invalidated by an addition or concatenate operation.");
        }

        #endregion
    }
}

