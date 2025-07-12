using System;
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

        struct DepthSortedDrawCommand
        {
            public DrawCommandSettings Settings;
            public int                 InstanceIndex;
            public float3              SortingWorldPosition;
        }

        struct ChunkDrawCommand : IComparable<ChunkDrawCommand>
        {
            public DrawCommandSettings   Settings;
            public DrawCommandVisibility Visibility;

            public int CompareTo(ChunkDrawCommand other) => Settings.CompareTo(other.Settings);
        }

        unsafe struct ThreadLocalDrawCommands
        {
            public const Allocator kAllocator = Allocator.TempJob;

            // Store the actual streams in a separate array so we can mutate them in place,
            // the hash map only supports a get/set API.
            public UnsafeParallelHashMap<DrawCommandSettings, int> DrawCommandStreamIndices;
            public UnsafeList<DrawCommandStream>                   DrawCommands;
            public ThreadLocalAllocator                            ThreadLocalAllocator;

            private fixed int m_CacheLinePadding[8];  // The padding here assumes some internal sizes

            public ThreadLocalDrawCommands(int capacity, ThreadLocalAllocator tlAllocator)
            {
                // Make sure we don't get false sharing by placing the thread locals on different cache lines.
                Assert.IsTrue(sizeof(ThreadLocalDrawCommands) >= JobsUtility.CacheLineSize);
                DrawCommandStreamIndices = new UnsafeParallelHashMap<DrawCommandSettings, int>(capacity, kAllocator);
                DrawCommands             = new UnsafeList<DrawCommandStream>(capacity, kAllocator);
                ThreadLocalAllocator     = tlAllocator;
            }

            public bool IsCreated => DrawCommandStreamIndices.IsCreated;

            public void Dispose()
            {
                if (!IsCreated)
                    return;

                for (int i = 0; i < DrawCommands.Length; ++i)
                    DrawCommands[i].Dispose();

                if (DrawCommandStreamIndices.IsCreated)
                    DrawCommandStreamIndices.Dispose();
                if (DrawCommands.IsCreated)
                    DrawCommands.Dispose();
            }

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

        unsafe struct DrawBinCollector
        {
            public const Allocator     kAllocator = Allocator.TempJob;
            public static readonly int NumThreads = ChunkDrawCommandOutput.NumThreads;

            public IndirectList<DrawCommandSettings>           Bins;
            private UnsafeParallelHashSet<DrawCommandSettings> m_BinSet;
            private UnsafeList<ThreadLocalDrawCommands>        m_ThreadLocalDrawCommands;

            public DrawBinCollector(UnsafeList<ThreadLocalDrawCommands> tlDrawCommands, RewindableAllocator* allocator)
            {
                Bins                      = new IndirectList<DrawCommandSettings>(0, allocator);
                m_BinSet                  = new UnsafeParallelHashSet<DrawCommandSettings>(0, kAllocator);
                m_ThreadLocalDrawCommands = tlDrawCommands;
            }

            public bool Add(DrawCommandSettings settings)
            {
                return true;
            }

            [BurstCompile]
            internal struct AllocateBinsJob : IJob
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
            internal struct CollectBinsJob : IJobParallelFor
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

            public JobHandle Dispose(JobHandle dependency)
            {
                return JobHandle.CombineDependencies(
                    Bins.Dispose(dependency),
                    m_BinSet.Dispose(dependency));
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
            public const int           kNumReleaseThreads        = 4;
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
                    ThreadLocalDrawCommands[ThreadIndex] = new ThreadLocalDrawCommands(BinCapacity, ThreadLocalAllocator);
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
                    BinCollector.Add(settings);
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
                    BinCollector.Add(settings);
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

            public JobHandle Dispose(JobHandle dependencies)
            {
                // First schedule a job to release all the thread local arrays, which requires
                // that the data structures are still in place so we can find them.
                var releaseChunkDrawCommandsDependency = new ReleaseChunkDrawCommandsJob
                {
                    DrawCommandOutput = this,
                    NumThreads        = kNumReleaseThreads,
                }.Schedule(kNumReleaseThreads, 1, dependencies);

                // When those have been released, release the data structures.
                var disposeDone = new JobHandle();
                disposeDone     = JobHandle.CombineDependencies(disposeDone,
                                                                ThreadLocalDrawCommands.Dispose(releaseChunkDrawCommandsDependency));
                disposeDone = JobHandle.CombineDependencies(disposeDone,
                                                            ThreadLocalCollectBuffers.Dispose(releaseChunkDrawCommandsDependency));
                disposeDone = JobHandle.CombineDependencies(disposeDone,
                                                            BinPresentFilter.Dispose(releaseChunkDrawCommandsDependency));
                disposeDone = JobHandle.CombineDependencies(disposeDone,
                                                            BinCollector.Dispose(releaseChunkDrawCommandsDependency));
                disposeDone = JobHandle.CombineDependencies(disposeDone,
                                                            SortedBins.Dispose(releaseChunkDrawCommandsDependency));
                disposeDone = JobHandle.CombineDependencies(disposeDone,
                                                            BinIndices.Dispose(releaseChunkDrawCommandsDependency));
                disposeDone = JobHandle.CombineDependencies(disposeDone,
                                                            WorkItems.Dispose(releaseChunkDrawCommandsDependency));

                return disposeDone;
            }

            [BurstCompile]
            private struct ReleaseChunkDrawCommandsJob : IJobParallelFor
            {
                public ChunkDrawCommandOutput DrawCommandOutput;
                public int                    NumThreads;

                public void Execute(int index)
                {
                    for (int i = index; i < ChunkDrawCommandOutput.NumThreads; i += NumThreads)
                    {
                        DrawCommandOutput.ThreadLocalDrawCommands[i].Dispose();
                        DrawCommandOutput.ThreadLocalCollectBuffers[i].Dispose();
                    }
                }
            }
        }
    }
}

