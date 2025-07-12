using System;
using System.Runtime.InteropServices;
using Unity.Assertions;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.Profiling;
using Unity.Rendering;
using UnityEngine.Rendering;

namespace Latios.Kinemation.Systems
{
    public partial struct GenerateBrgDrawCommandsSystem
    {
        unsafe struct DrawCommandSettings : IEquatable<DrawCommandSettings>
        {
            // TODO: This could be thinned to fit in 128 bits?

            public int                   FilterIndex;
            public BatchDrawCommandFlags Flags;
            public BatchMaterialID       MaterialID;
            public BatchMeshID           MeshID;
            public ushort                SplitMask;
            public ushort                SubMeshIndex;
            public BatchID               BatchID;
            private int                  m_CachedHash;

            public bool Equals(DrawCommandSettings other)
            {
                // Use temp variables so CPU can co-issue all comparisons
                bool eq_batch = BatchID == other.BatchID;
                bool eq_rest  = math.all(PackedUint4 == other.PackedUint4);

                return eq_batch && eq_rest;
            }

            private uint4 PackedUint4
            {
                get
                {
                    Assert.IsTrue(MeshID.value < (1 << 24));
                    Assert.IsTrue(SubMeshIndex < (1 << 8));
                    Assert.IsTrue((uint)Flags < (1 << 24));
                    Assert.IsTrue(SplitMask < (1 << 8));

                    return new uint4(
                        (uint)FilterIndex,
                        (((uint)SplitMask & 0xff) << 24) | ((uint)Flags & 0x00ffffffff),
                        MaterialID.value,
                        ((MeshID.value & 0x00ffffff) << 8) | ((uint)SubMeshIndex & 0xff)
                        );
                }
            }

            public int CompareTo(DrawCommandSettings other)
            {
                uint4 a           = PackedUint4;
                uint4 b           = other.PackedUint4;
                int   cmp_batchID = BatchID.CompareTo(other.BatchID);

                int4 lt  = math.select(int4.zero, new int4(-1), a < b);
                int4 gt  = math.select(int4.zero, new int4(1), a > b);
                int4 neq = lt | gt;

                int* firstNonZero = stackalloc int[4];

                bool4 nz    = neq != int4.zero;
                bool  anyNz = math.any(nz);
                math.compress(firstNonZero, 0, neq, nz);

                return anyNz ? firstNonZero[0] : cmp_batchID;
            }

            // Used to verify correctness of fast CompareTo
            public int CompareToReference(DrawCommandSettings other)
            {
                int cmpFilterIndex  = FilterIndex.CompareTo(other.FilterIndex);
                int cmpFlags        = ((int)Flags).CompareTo((int)other.Flags);
                int cmpMaterialID   = MaterialID.CompareTo(other.MaterialID);
                int cmpMeshID       = MeshID.CompareTo(other.MeshID);
                int cmpSplitMask    = SplitMask.CompareTo(other.SubMeshIndex);
                int cmpSubMeshIndex = SubMeshIndex.CompareTo(other.SubMeshIndex);
                int cmpBatchID      = BatchID.CompareTo(other.BatchID);

                if (cmpFilterIndex != 0)
                    return cmpFilterIndex;
                if (cmpFlags != 0)
                    return cmpFlags;
                if (cmpMaterialID != 0)
                    return cmpMaterialID;
                if (cmpMeshID != 0)
                    return cmpMeshID;
                if (cmpSubMeshIndex != 0)
                    return cmpSubMeshIndex;
                if (cmpSplitMask != 0)
                    return cmpSplitMask;

                return cmpBatchID;
            }

            public override int GetHashCode() => m_CachedHash;

            public void ComputeHashCode()
            {
                m_CachedHash = ChunkDrawCommandOutput.FastHash(this);
            }

            public bool HasSortingPosition => (int)(Flags & BatchDrawCommandFlags.HasSortingPosition) != 0;

            public override string ToString()
            {
                return
                    $"DrawCommandSettings(batchID: {BatchID.value}, materialID: {MaterialID.value}, meshID: {MeshID.value}, submesh: {SubMeshIndex}, filter: {FilterIndex}, flags: {Flags:x}, splitMask: {SplitMask:x})";
            }
        }

        struct ChunkDrawCommand : IComparable<ChunkDrawCommand>
        {
            public DrawCommandSettings   Settings;
            public DrawCommandVisibility Visibility;

            public int CompareTo(ChunkDrawCommand other) => Settings.CompareTo(other.Settings);
        }

        internal unsafe struct DrawCommandWorkItem
        {
            public DrawStream<DrawCommandVisibility>.Header* Arrays;
            public DrawStream<IntPtr>.Header*                TransformArrays;
            public int                                       BinIndex;
            public int                                       PrefixSumNumInstances;
        }

        internal unsafe struct DrawCommandVisibility
        {
            public int         ChunkStartIndex;
            public fixed ulong VisibleInstances[2];

            public DrawCommandVisibility(int startIndex)
            {
                ChunkStartIndex     = startIndex;
                VisibleInstances[0] = 0;
                VisibleInstances[1] = 0;
            }

            public int VisibleInstanceCount => math.countbits(VisibleInstances[0]) + math.countbits(VisibleInstances[1]);

            public override string ToString()
            {
                return $"Visibility({ChunkStartIndex}, {VisibleInstances[1]:x16}, {VisibleInstances[0]:x16})";
            }
        }

        [NoAlias]
        internal unsafe struct DrawCommandStream
        {
            private DrawStream<DrawCommandVisibility> m_Stream;
            private DrawStream<IntPtr>                m_ChunkTransformsStream;
            private int                               m_PrevChunkStartIndex;
            [NoAlias]
            private DrawCommandVisibility* m_PrevVisibility;

            public DrawCommandStream(RewindableAllocator* allocator)
            {
                m_Stream                = new DrawStream<DrawCommandVisibility>(allocator);
                m_ChunkTransformsStream = default;  // Don't allocate here, only on demand
                m_PrevChunkStartIndex   = -1;
                m_PrevVisibility        = null;
            }

            public void Emit(RewindableAllocator* allocator, int qwordIndex, int bitIndex, int chunkStartIndex)
            {
                DrawCommandVisibility* visibility;

                if (chunkStartIndex == m_PrevChunkStartIndex)
                {
                    visibility = m_PrevVisibility;
                }
                else
                {
                    visibility  = m_Stream.AppendElement(allocator);
                    *visibility = new DrawCommandVisibility(chunkStartIndex);
                }

                visibility->VisibleInstances[qwordIndex] |= 1ul << bitIndex;

                m_PrevChunkStartIndex = chunkStartIndex;
                m_PrevVisibility      = visibility;
                m_Stream.AddInstances(1);
            }

            public void EmitDepthSorted(RewindableAllocator* allocator,
                                        int qwordIndex, int bitIndex, int chunkStartIndex,
                                        float4x4* chunkTransforms)
            {
                DrawCommandVisibility* visibility;

                if (chunkStartIndex == m_PrevChunkStartIndex)
                {
                    visibility = m_PrevVisibility;

                    // Transforms have already been written when the element was added
                }
                else
                {
                    visibility  = m_Stream.AppendElement(allocator);
                    *visibility = new DrawCommandVisibility(chunkStartIndex);

                    // Store a pointer to the chunk transform array, which
                    // instance expansion can use to get the positions.

                    if (!m_ChunkTransformsStream.IsCreated)
                        m_ChunkTransformsStream.Init(allocator);

                    var transforms = m_ChunkTransformsStream.AppendElement(allocator);
                    *   transforms = (IntPtr)chunkTransforms;
                }

                visibility->VisibleInstances[qwordIndex] |= 1ul << bitIndex;

                m_PrevChunkStartIndex = chunkStartIndex;
                m_PrevVisibility      = visibility;
                m_Stream.AddInstances(1);
            }

            public DrawStream<DrawCommandVisibility> Stream => m_Stream;
            public DrawStream<IntPtr> TransformsStream => m_ChunkTransformsStream;
        }

        [StructLayout(LayoutKind.Sequential, Size = 128)]  // Force instances on separate cache lines
        unsafe struct ThreadLocalDrawCommands
        {
            // Store the actual streams in a separate array so we can mutate them in place,
            // the hash map only supports a get/set API.
            public UnsafeParallelHashMap<DrawCommandSettings, int> DrawCommandStreamIndices;
            public UnsafeList<DrawCommandStream>                   DrawCommands;
            public ThreadLocalAllocator                            ThreadLocalAllocator;

            public ThreadLocalDrawCommands(int capacity, ThreadLocalAllocator tlAllocator, int threadIndex)
            {
                var allocator            = tlAllocator.ThreadAllocator(threadIndex)->Handle;
                DrawCommandStreamIndices = new UnsafeParallelHashMap<DrawCommandSettings, int>(capacity, allocator);
                DrawCommands             = new UnsafeList<DrawCommandStream>(capacity, allocator);
                ThreadLocalAllocator     = tlAllocator;
            }

            public bool IsCreated => DrawCommandStreamIndices.IsCreated;

            public bool Emit(DrawCommandSettings settings, int qwordIndex, int bitIndex, int chunkStartIndex, int threadIndex)
            {
                var allocator = ThreadLocalAllocator.ThreadAllocator(threadIndex);

                if (DrawCommandStreamIndices.TryGetValue(settings, out int streamIndex))
                {
                    DrawCommandStream* stream = DrawCommands.Ptr + streamIndex;
                    stream->Emit(allocator, qwordIndex, bitIndex, chunkStartIndex);
                    return false;
                }
                else
                {
                    streamIndex = DrawCommands.Length;
                    DrawCommands.Add(new DrawCommandStream(allocator));
                    DrawCommandStreamIndices.Add(settings, streamIndex);

                    DrawCommandStream* stream = DrawCommands.Ptr + streamIndex;
                    stream->Emit(allocator, qwordIndex, bitIndex, chunkStartIndex);

                    return true;
                }
            }

            public bool EmitDepthSorted(
                DrawCommandSettings settings, int qwordIndex, int bitIndex, int chunkStartIndex,
                float4x4* chunkTransforms,
                int threadIndex)
            {
                var allocator = ThreadLocalAllocator.ThreadAllocator(threadIndex);

                if (DrawCommandStreamIndices.TryGetValue(settings, out int streamIndex))
                {
                    DrawCommandStream* stream = DrawCommands.Ptr + streamIndex;
                    stream->EmitDepthSorted(allocator, qwordIndex, bitIndex, chunkStartIndex, chunkTransforms);
                    return false;
                }
                else
                {
                    streamIndex = DrawCommands.Length;
                    DrawCommands.Add(new DrawCommandStream(allocator));
                    DrawCommandStreamIndices.Add(settings, streamIndex);

                    DrawCommandStream* stream = DrawCommands.Ptr + streamIndex;
                    stream->EmitDepthSorted(allocator, qwordIndex, bitIndex, chunkStartIndex, chunkTransforms);

                    return true;
                }
            }
        }

        [StructLayout(LayoutKind.Sequential, Size = 64)]  // Force instances on separate cache lines
        internal unsafe struct ThreadLocalCollectBuffer
        {
            public static readonly int kCollectBufferSize = ChunkDrawCommandOutput.NumThreads;

            public UnsafeList<DrawCommandWorkItem> WorkItems;

            public void EnsureCapacity(UnsafeList<DrawCommandWorkItem>.ParallelWriter dst, int count, ThreadLocalAllocator tlAllocator, int threadIndex)
            {
                Assert.IsTrue(count <= kCollectBufferSize);

                if (!WorkItems.IsCreated)
                {
                    var allocator = tlAllocator.ThreadAllocator(threadIndex)->Handle;
                    WorkItems     = new UnsafeList<DrawCommandWorkItem>(
                        kCollectBufferSize,
                        allocator,
                        NativeArrayOptions.UninitializedMemory);
                }

                if (WorkItems.Length + count > WorkItems.Capacity)
                    Flush(dst);
            }

            public void Flush(UnsafeList<DrawCommandWorkItem>.ParallelWriter dst)
            {
                dst.AddRangeNoResize(WorkItems.Ptr, WorkItems.Length);
                WorkItems.Clear();
            }

            public void Add(DrawCommandWorkItem workItem) => WorkItems.Add(workItem);
        }

        unsafe struct DrawBinCollector
        {
            public static readonly int NumThreads = ChunkDrawCommandOutput.NumThreads;

            public IndirectList<DrawCommandSettings>           Bins;
            private UnsafeParallelHashSet<DrawCommandSettings> m_BinSet;
            private UnsafeList<ThreadLocalDrawCommands>        m_ThreadLocalDrawCommands;

            public DrawBinCollector(UnsafeList<ThreadLocalDrawCommands> tlDrawCommands, RewindableAllocator* allocator)
            {
                Bins                      = new IndirectList<DrawCommandSettings>(0, allocator);
                m_BinSet                  = new UnsafeParallelHashSet<DrawCommandSettings>(0, allocator->Handle);
                m_ThreadLocalDrawCommands = tlDrawCommands;
            }

            [BurstCompile]
            struct AllocateBinsJob : IJob
            {
                public IndirectList<DrawCommandSettings>          Bins;
                public UnsafeParallelHashSet<DrawCommandSettings> BinSet;
                public UnsafeList<ThreadLocalDrawCommands>        ThreadLocalDrawCommands;

                public void Execute()
                {
                    int numBinsUpperBound = 0;

                    for (int i = 0; i < NumThreads; ++i)
                        numBinsUpperBound += ThreadLocalDrawCommands.ElementAt(i).DrawCommands.Length;

                    Bins.SetCapacity(numBinsUpperBound);
                    BinSet.Capacity = numBinsUpperBound;
                }
            }

            [BurstCompile]
            struct CollectBinsJob : IJobParallelFor
            {
                public const int ThreadLocalArraySize = 256;

                public IndirectList<DrawCommandSettings>                         Bins;
                public UnsafeParallelHashSet<DrawCommandSettings>.ParallelWriter BinSet;
                public UnsafeList<ThreadLocalDrawCommands>                       ThreadLocalDrawCommands;

                private UnsafeList<DrawCommandSettings>.ParallelWriter m_BinsParallel;

                public void Execute(int index)
                {
                    ref var drawCommands = ref ThreadLocalDrawCommands.ElementAt(index);
                    if (!drawCommands.IsCreated)
                        return;

                    m_BinsParallel = Bins.List->AsParallelWriter();

                    var uniqueSettings = new NativeArray<DrawCommandSettings>(
                        ThreadLocalArraySize,
                        Allocator.Temp,
                        NativeArrayOptions.UninitializedMemory);
                    int numSettings = 0;

                    var keys = drawCommands.DrawCommandStreamIndices.GetEnumerator();
                    while (keys.MoveNext())
                    {
                        var settings = keys.Current.Key;
                        if (BinSet.Add(settings))
                            AddBin(uniqueSettings, ref numSettings, settings);
                    }
                    keys.Dispose();

                    Flush(uniqueSettings, numSettings);
                }

                private void AddBin(
                    NativeArray<DrawCommandSettings> uniqueSettings,
                    ref int numSettings,
                    DrawCommandSettings settings)
                {
                    if (numSettings >= ThreadLocalArraySize)
                    {
                        Flush(uniqueSettings, numSettings);
                        numSettings = 0;
                    }

                    uniqueSettings[numSettings] = settings;
                    ++numSettings;
                }

                private void Flush(
                    NativeArray<DrawCommandSettings> uniqueSettings,
                    int numSettings)
                {
                    if (numSettings <= 0)
                        return;

                    m_BinsParallel.AddRangeNoResize(
                        uniqueSettings.GetUnsafeReadOnlyPtr(),
                        numSettings);
                }
            }

            public JobHandle ScheduleFinalize(JobHandle dependency)
            {
                var allocateDependency = new AllocateBinsJob
                {
                    Bins                    = Bins,
                    BinSet                  = m_BinSet,
                    ThreadLocalDrawCommands = m_ThreadLocalDrawCommands,
                }.Schedule(dependency);

                return new CollectBinsJob
                {
                    Bins                    = Bins,
                    BinSet                  = m_BinSet.AsParallelWriter(),
                    ThreadLocalDrawCommands = m_ThreadLocalDrawCommands,
                }.Schedule(NumThreads, 1, allocateDependency);
            }

            public void RunFinalizeImmediate()
            {
                var allocateJob = new AllocateBinsJob
                {
                    Bins                    = Bins,
                    BinSet                  = m_BinSet,
                    ThreadLocalDrawCommands = m_ThreadLocalDrawCommands,
                };
                allocateJob.Execute();
                var collectJob = new CollectBinsJob
                {
                    Bins                    = Bins,
                    BinSet                  = m_BinSet.AsParallelWriter(),
                    ThreadLocalDrawCommands = m_ThreadLocalDrawCommands,
                };
                for (int i = 0; i < NumThreads; i++)
                {
                    collectJob.Execute(i);
                }
            }
        }

        [NoAlias]
        unsafe struct ChunkDrawCommandOutput
        {
            public const Allocator kAllocator = Allocator.TempJob;

#if UNITY_2022_2_14F1_OR_NEWER
            public static readonly int NumThreads = JobsUtility.ThreadIndexCount;
#else
            public static readonly int NumThreads = JobsUtility.MaxJobThreadCount;
#endif

            public static readonly int kNumThreadsBitfieldLength = (NumThreads + 63) / 64;
            public const int           kBinPresentFilterSize     = 1 << 10;

            public UnsafeList<ThreadLocalDrawCommands>  ThreadLocalDrawCommands;
            public UnsafeList<ThreadLocalCollectBuffer> ThreadLocalCollectBuffers;

            public UnsafeList<long> BinPresentFilter;

            public DrawBinCollector BinCollector;
            public IndirectList<DrawCommandSettings> UnsortedBins => BinCollector.Bins;

            [NativeDisableUnsafePtrRestriction]
            public IndirectList<int> SortedBins;

            [NativeDisableUnsafePtrRestriction]
            public IndirectList<DrawCommandBin> BinIndices;

            [NativeDisableUnsafePtrRestriction]
            public IndirectList<DrawCommandWorkItem> WorkItems;

            [NativeDisableParallelForRestriction]
            [NativeDisableContainerSafetyRestriction]
            public NativeArray<BatchCullingOutputDrawCommands> CullingOutput;

            public int BinCapacity;

            public ThreadLocalAllocator ThreadLocalAllocator;

            public ProfilerMarker ProfilerEmit;

#pragma warning disable 649
            [NativeSetThreadIndex] public int ThreadIndex;
#pragma warning restore 649

            public ChunkDrawCommandOutput(
                int initialBinCapacity,
                ThreadLocalAllocator tlAllocator,
                BatchCullingOutput cullingOutput)
            {
                BinCapacity   = initialBinCapacity;
                CullingOutput = cullingOutput.drawCommands;

                ThreadLocalAllocator = tlAllocator;
                var generalAllocator = ThreadLocalAllocator.GeneralAllocator;

                ThreadLocalDrawCommands = new UnsafeList<ThreadLocalDrawCommands>(
                    NumThreads,
                    generalAllocator->Handle,
                    NativeArrayOptions.ClearMemory);
                ThreadLocalDrawCommands.Resize(ThreadLocalDrawCommands.Capacity);
                ThreadLocalCollectBuffers = new UnsafeList<ThreadLocalCollectBuffer>(
                    NumThreads,
                    generalAllocator->Handle,
                    NativeArrayOptions.ClearMemory);
                ThreadLocalCollectBuffers.Resize(ThreadLocalCollectBuffers.Capacity);
                BinPresentFilter = new UnsafeList<long>(
                    kBinPresentFilterSize * kNumThreadsBitfieldLength,
                    generalAllocator->Handle,
                    NativeArrayOptions.ClearMemory);
                BinPresentFilter.Resize(BinPresentFilter.Capacity);

                BinCollector = new DrawBinCollector(ThreadLocalDrawCommands, generalAllocator);
                SortedBins   = new IndirectList<int>(0, generalAllocator);
                BinIndices   = new IndirectList<DrawCommandBin>(0, generalAllocator);
                WorkItems    = new IndirectList<DrawCommandWorkItem>(0, generalAllocator);

                // Initialized by job system
                ThreadIndex = 0;

                ProfilerEmit = new ProfilerMarker("Emit");
            }

            public void InitializeForEmitThread()
            {
                // First to use the thread local initializes is, but don't double init
                if (!ThreadLocalDrawCommands[ThreadIndex].IsCreated)
                    ThreadLocalDrawCommands[ThreadIndex] = new ThreadLocalDrawCommands(BinCapacity, ThreadLocalAllocator, ThreadIndex);
            }

            public BatchCullingOutputDrawCommands* CullingOutputDrawCommands =>
            (BatchCullingOutputDrawCommands*)CullingOutput.GetUnsafePtr();

            public static T* Malloc<T>(int count) where T : unmanaged
            {
                return (T*)UnsafeUtility.Malloc(
                    UnsafeUtility.SizeOf<T>() * count,
                    UnsafeUtility.AlignOf<T>(),
                    kAllocator);
            }

            private ThreadLocalDrawCommands* DrawCommands
            {
                [return : NoAlias]
                get => ThreadLocalDrawCommands.Ptr + ThreadIndex;
            }

            public ThreadLocalCollectBuffer* CollectBuffer
            {
                [return : NoAlias]
                get => ThreadLocalCollectBuffers.Ptr + ThreadIndex;
            }

            public void Emit(DrawCommandSettings settings, int entityQword, int entityBit, int chunkStartIndex)
            {
                // Update the cached hash code here, so all processing after this can just use the cached value
                // without recomputing the hash each time.
                settings.ComputeHashCode();

                bool newBinAdded = DrawCommands->Emit(settings, entityQword, entityBit, chunkStartIndex, ThreadIndex);
                if (newBinAdded)
                {
                    MarkBinPresentInThread(settings, ThreadIndex);
                }
            }

            public void EmitDepthSorted(
                DrawCommandSettings settings, int entityQword, int entityBit, int chunkStartIndex,
                float4x4* chunkTransforms)
            {
                // Update the cached hash code here, so all processing after this can just use the cached value
                // without recomputing the hash each time.
                settings.ComputeHashCode();

                bool newBinAdded = DrawCommands->EmitDepthSorted(settings, entityQword, entityBit, chunkStartIndex, chunkTransforms, ThreadIndex);
                if (newBinAdded)
                {
                    MarkBinPresentInThread(settings, ThreadIndex);
                }
            }

            [return : NoAlias]
            public long* BinPresentFilterForSettings(DrawCommandSettings settings)
            {
                uint hash  = (uint)settings.GetHashCode();
                uint index = hash % (uint)kBinPresentFilterSize;
                return BinPresentFilter.Ptr + index * kNumThreadsBitfieldLength;
            }

            private void MarkBinPresentInThread(DrawCommandSettings settings, int threadIndex)
            {
                long* settingsFilter = BinPresentFilterForSettings(settings);

                uint threadQword = (uint)threadIndex / 64;
                uint threadBit   = (uint)threadIndex % 64;

                AtomicHelpers.AtomicOr(
                    settingsFilter,
                    (int)threadQword,
                    1L << (int)threadBit);
            }

            public static int FastHash<T>(T value) where T : struct
            {
                // TODO: Replace with hardware CRC32?
                return (int)xxHash3.Hash64(UnsafeUtility.AddressOf(ref value), UnsafeUtility.SizeOf<T>()).x;
            }
        }
    }
}

