using Latios.Unsafe;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace Latios.LifeFX
{
    public partial struct GraphicsEventPostal : ICollectionComponent
    {
        public struct Mailbox<T> where T : unmanaged
        {
            internal NativeArray<BlocklistPair> blocklistPair;
            [NativeSetThreadIndex] internal int threadIndex;

            public unsafe void Send<TunnelType>(in T graphicsEvent, UnityObjectRef<TunnelType> tunnel) where TunnelType : GraphicsEventTunnel<T>
            {
                var bl = blocklistPair[0];
                bl.events.Write(graphicsEvent, threadIndex);
                bl.tunnelTargets.Write(tunnel, threadIndex);
            }
        }

        public Mailbox<T> GetMailbox<T>() where T : unmanaged
        {
            return new Mailbox<T>
            {
                threadIndex   = threadIndex,
                blocklistPair = blocklistPairArray.GetSubArray(GraphicsEventTypeRegistry.TypeToIndex<T>.typeToIndex, 1)
            };
        }

        public JobHandle TryDispose(JobHandle inputDeps) => inputDeps;  // Allocated with custom rewindable allocator

        internal struct BlocklistPair
        {
            public UnsafeParallelBlockList tunnelTargets;
            public UnsafeParallelBlockList events;
        }

        internal NativeArray<BlocklistPair> blocklistPairArray;
        [NativeSetThreadIndex] internal int threadIndex;

        internal GraphicsEventPostal(AllocatorManager.AllocatorHandle allocator)
        {
            var typeCount      = GraphicsEventTypeRegistry.s_eventMetadataList.Data.Length;
            blocklistPairArray = CollectionHelper.CreateNativeArray<BlocklistPair>(typeCount, allocator, NativeArrayOptions.UninitializedMemory);
            for (int i = 0; i < blocklistPairArray.Length; i++)
            {
                var eventSize         = GraphicsEventTypeRegistry.s_eventMetadataList.Data[i].size;
                blocklistPairArray[i] = new BlocklistPair
                {
                    tunnelTargets = new UnsafeParallelBlockList(UnsafeUtility.SizeOf<UnityObjectRef<GraphicsEventTunnelBase> >(), 1024, allocator),
                    events        = new UnsafeParallelBlockList(eventSize, 1024, allocator)
                };
            }
            threadIndex = 0;
        }
    }

    // Wanted to check to ensure Burst could resolve managed inheritance type constraints in generics.
    // Seems it can handle it just fine!
    //[Unity.Burst.BurstCompile]
    //internal static class BurstManagedGenericTestClass
    //{
    //    [Unity.Burst.BurstCompile]
    //    public static unsafe void TryThis(GraphicsEventPostal* postal)
    //    {
    //        postal->GetMailbox<int>().Send<SpawnEventTunnel>(0, default);
    //        postal->GetMailbox<int>().Send<GraphicsEventTunnel<int> >(1, default);
    //    }
    //
    //    public static unsafe void TryThisFromManaged(GraphicsEventPostal* postal) => TryThis(postal);
    //}
}

