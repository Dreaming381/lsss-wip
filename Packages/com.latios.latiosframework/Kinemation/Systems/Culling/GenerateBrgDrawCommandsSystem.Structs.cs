using System;
using System.Runtime.InteropServices;
using Unity.Assertions;
using Unity.Burst;
using Unity.Burst.Intrinsics;
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
        [StructLayout(LayoutKind.Explicit)]
        unsafe struct DrawCommandSettings : IEquatable<DrawCommandSettings>
        {
            [FieldOffset(0)] private int             m_hash;
            [FieldOffset(4)] public BatchID          batch;
            [FieldOffset(8)] public ushort           splitMask;
            [FieldOffset(10)] public ushort          meshLod;
            [FieldOffset(12)] public ushort          submesh;
            [FieldOffset(14)] private uint           m_mesh;
            [FieldOffset(18)] public BatchMaterialID material;
            [FieldOffset(22)] private ushort         m_flags;
            [FieldOffset(24)] public int             filterIndex;
            [FieldOffset(28)] public int             renderingPriority;

            [FieldOffset(0)] int4x2 asPackedGeneric;
            [FieldOffset(0)] v256   asPacked256;

            public BatchMeshID mesh
            {
                // Todo: Does it even matter if meshes are sorted different from BatchMeshID?
                get => new BatchMeshID { value = m_mesh ^ 0x00008000 };
                set => m_mesh                  = value.value ^ 0x00008000;
            }
            public BatchDrawCommandFlags flags
            {
                get => (BatchDrawCommandFlags)m_flags;
                set => m_flags = (ushort)value;
            }

            public bool Equals(DrawCommandSettings other) => asPackedGeneric.Equals(other.asPackedGeneric);

            public int CompareTo(DrawCommandSettings other)
            {
                if (X86.Avx2.IsAvx2Supported)
                {
                    var a     = asPacked256;
                    var b     = other.asPacked256;
                    var aGt   = X86.Avx2.mm256_cmpgt_epi64(a, b);
                    var bGt   = X86.Avx2.mm256_cmpgt_epi64(b, a);
                    var aMask = math.asuint(X86.Avx2.mm256_movemask_epi8(aGt));
                    var bMask = math.asuint(X86.Avx2.mm256_movemask_epi8(bGt));
                    return aMask.CompareTo(bMask);
                }
                else if (Arm.Neon.IsNeonSupported)
                {
                    var a           = asPackedGeneric;
                    var b           = other.asPackedGeneric;
                    var ac0         = new v128(a.c0.x, a.c0.y, a.c0.z, a.c0.w);
                    var bc0         = new v128(b.c0.x, b.c0.y, b.c0.z, b.c0.w);
                    var ac1         = new v128(a.c1.x, a.c1.y, a.c1.z, a.c1.w);
                    var bc1         = new v128(b.c1.x, b.c1.y, b.c1.z, b.c1.w);
                    var a0          = Arm.Neon.vcgtq_s64(ac0, bc0);
                    var b0          = Arm.Neon.vcgtq_s64(bc0, ac0);
                    var a1          = Arm.Neon.vcgtq_s64(ac1, bc1);
                    var b1          = Arm.Neon.vcgtq_s64(bc1, ac1);
                    var aLower      = Arm.Neon.vshrn_n_u16(a0, 4);
                    var bLower      = Arm.Neon.vshrn_n_u16(b0, 4);
                    var aLowerUpper = Arm.Neon.vshrn_high_n_u16(aLower, a1, 4);
                    var bLowerUpper = Arm.Neon.vshrn_high_n_u16(bLower, b1, 4);
                    var aMask       = Arm.Neon.vshrn_n_u16(aLowerUpper, 4).ULong0;
                    var bMask       = Arm.Neon.vshrn_n_u16(bLowerUpper, 4).ULong0;
                    return aMask.CompareTo(bMask);
                }
                else
                {
                    var a     = asPackedGeneric;
                    var b     = other.asPackedGeneric;
                    var a0    = a.c0 > b.c0;
                    var b0    = b.c0 > a.c0;
                    var a1    = a.c1 > b.c1;
                    var b1    = b.c1 > a.c1;
                    var aMask = math.bitmask(a0) | (math.bitmask(a1) << 16);
                    var bMask = math.bitmask(b0) | (math.bitmask(b1) << 16);
                    return aMask.CompareTo(bMask);
                }
            }

            public override int GetHashCode() => m_hash;
            public void ComputeHashCode()
            {
                m_hash = 0;
                m_hash = asPackedGeneric.GetHashCode();
            }
            public bool hasSortingPosition => (flags & BatchDrawCommandFlags.HasSortingPosition) != 0;
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
            public int                                       BinIndex;
            public int                                       PrefixSumNumInstances;
        }

        internal unsafe struct DrawCommandVisibility
        {
            public fixed ulong   visibleInstances[2];
            public fixed ulong   crossfadeComplements[2];
            public float*        transformsPtr;
            public LodCrossfade* crossfadesPtr;
            public int           chunkStartIndex;
            public int           transformStrideInFloats;
            public int           positionOffsetInFloats;

            public DrawCommandVisibility(int startIndex, float* transformsPtr, LodCrossfade* crossfadesPtr, int transformStrideInFloats, int positionOffsetInFloats)
            {
                chunkStartIndex              = startIndex;
                visibleInstances[0]          = 0;
                visibleInstances[1]          = 0;
                crossfadeComplements[0]      = 0;
                crossfadeComplements[1]      = 0;
                this.transformsPtr           = transformsPtr;
                this.crossfadesPtr           = crossfadesPtr;
                this.transformStrideInFloats = transformStrideInFloats;
                this.positionOffsetInFloats  = positionOffsetInFloats;
            }
        }

        [NoAlias]
        internal unsafe struct DrawCommandStream
        {
            private DrawStream<DrawCommandVisibility> m_Stream;
            private int                               m_PrevChunkStartIndex;
            [NoAlias]
            private DrawCommandVisibility* m_PrevVisibility;

            public DrawCommandStream(RewindableAllocator* allocator)
            {
                m_Stream              = new DrawStream<DrawCommandVisibility>(allocator);
                m_PrevChunkStartIndex = -1;
                m_PrevVisibility      = null;
            }

            public void Emit(RewindableAllocator* allocator,
                             int qwordIndex, int bitIndex, int chunkStartIndex,
                             LodCrossfade* lodCrossfades, bool complementLodCrossfade,
                             float* chunkTransforms, int transformStrideInFloats, int positionOffsetInFloats)
            {
                DrawCommandVisibility* visibility;

                if (chunkStartIndex == m_PrevChunkStartIndex)
                {
                    visibility = m_PrevVisibility;
                }
                else
                {
                    visibility  = m_Stream.AppendElement(allocator);
                    *visibility = new DrawCommandVisibility(chunkStartIndex, chunkTransforms, lodCrossfades, transformStrideInFloats, positionOffsetInFloats);
                }

                var bit                                   = 1ul << bitIndex;
                visibility->visibleInstances[qwordIndex] |= bit;
                if (complementLodCrossfade)
                    visibility->crossfadeComplements[qwordIndex] |= bit;

                m_PrevChunkStartIndex = chunkStartIndex;
                m_PrevVisibility      = visibility;
                m_Stream.AddInstances(1);
            }

            public DrawStream<DrawCommandVisibility> Stream => m_Stream;
        }

        [StructLayout(LayoutKind.Sequential, Size = 128)]  // Force instances on separate cache lines
        unsafe struct ThreadLocalDrawCommands
        {
            // Store the actual streams in a separate array so we can mutate them in place,
            // the hash map only supports a get/set API.
            public UnsafeHashMap<DrawCommandSettings, int> DrawCommandStreamIndices;
            public UnsafeList<DrawCommandStream>           DrawCommands;
            public ThreadLocalAllocator                    ThreadLocalAllocator;

            public ThreadLocalDrawCommands(int capacity, ThreadLocalAllocator tlAllocator, int threadIndex)
            {
                var allocator            = tlAllocator.ThreadAllocator(threadIndex)->Handle;
                DrawCommandStreamIndices = new UnsafeHashMap<DrawCommandSettings, int>(capacity, allocator);
                DrawCommands             = new UnsafeList<DrawCommandStream>(capacity, allocator);
                ThreadLocalAllocator     = tlAllocator;
            }

            public bool IsCreated => DrawCommandStreamIndices.IsCreated;

            public bool Emit(in DrawCommandSettings settings, int qwordIndex, int bitIndex, int chunkStartIndex, int threadIndex,
                             LodCrossfade* lodCrossfades, bool complementLodCrossfade,
                             float* chunkTransforms, int transformStrideInFloats, int positionOffsetInFloats)
            {
                var allocator = ThreadLocalAllocator.ThreadAllocator(threadIndex);

                if (DrawCommandStreamIndices.TryGetValue(settings, out int streamIndex))
                {
                    DrawCommandStream* stream = DrawCommands.Ptr + streamIndex;
                    stream->Emit(allocator,
                                 qwordIndex,
                                 bitIndex,
                                 chunkStartIndex,
                                 lodCrossfades,
                                 complementLodCrossfade,
                                 chunkTransforms,
                                 transformStrideInFloats,
                                 positionOffsetInFloats);
                    return false;
                }
                else
                {
                    streamIndex = DrawCommands.Length;
                    DrawCommands.Add(new DrawCommandStream(allocator));
                    DrawCommandStreamIndices.Add(settings, streamIndex);

                    DrawCommandStream* stream = DrawCommands.Ptr + streamIndex;
                    stream->Emit(allocator,
                                 qwordIndex,
                                 bitIndex,
                                 chunkStartIndex,
                                 lodCrossfades,
                                 complementLodCrossfade,
                                 chunkTransforms,
                                 transformStrideInFloats,
                                 positionOffsetInFloats);

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

            public void Emit(ref DrawCommandSettings settings, int entityQword, int entityBit, int chunkStartIndex,
                             LodCrossfade* lodCrossfades, bool complementLodCrossfade,
                             float* chunkTransforms = null, int transformStrideInFloats = 0, int positionOffsetInFloats = 0)
            {
                // Update the cached hash code here, so all processing after this can just use the cached value
                // without recomputing the hash each time.
                settings.ComputeHashCode();

                bool newBinAdded = DrawCommands->Emit(in settings,
                                                      entityQword,
                                                      entityBit,
                                                      chunkStartIndex,
                                                      ThreadIndex,
                                                      lodCrossfades,
                                                      complementLodCrossfade,
                                                      chunkTransforms,
                                                      transformStrideInFloats,
                                                      positionOffsetInFloats);
                if (newBinAdded)
                {
                    MarkBinPresentInThread(in settings, ThreadIndex);
                }
            }

            [return : NoAlias]
            public long* BinPresentFilterForSettings(in DrawCommandSettings settings)
            {
                uint hash  = (uint)settings.GetHashCode();
                uint index = hash % (uint)kBinPresentFilterSize;
                return BinPresentFilter.Ptr + index * kNumThreadsBitfieldLength;
            }

            private void MarkBinPresentInThread(in DrawCommandSettings settings, int threadIndex)
            {
                long* settingsFilter = BinPresentFilterForSettings(in settings);

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

