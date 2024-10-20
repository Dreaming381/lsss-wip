using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;

namespace Latios.LifeFX
{
    public struct GraphicsSyncState
    {
        internal int trackedEntityID;
        internal int trackedEntityVersion;
        internal int bufferIndex;
    }

    // If IEnableable, then enabled means "changed" and requires updates
    public interface IGraphicsSyncComponent<T> : IComponentData where T : unmanaged
    {
        // Normally zero if you simply make GraphicsSyncState the first field.
        public abstract int GetGraphicsSyncStateFieldOffset();

        // Assign the values of structs obtained by calls to GetCodeAssignedComponents() to fieldCodeAssignable to create byte offset bindings.
        // Use GetCodeAssignedBitPack() for bit-level granularity.
        // You can only fetch 256 bytes worth of source data in a pass (call to this function). Return true if you need an additional pass.
        // Otherwise, return false.
        public abstract bool Register(GraphicsSyncGatherRegistration registration, ref T fieldCodeAssignable, int passIndex);
    }

    public struct GraphicsSyncSpawnEventTunnels : ISharedComponentData
    {
        public UnityObjectRef<GraphicsEventTunnel<int> > tunnelA;
        public UnityObjectRef<GraphicsEventTunnel<int> > tunnelB;
        public UnityObjectRef<GraphicsEventTunnel<int> > tunnelC;
        public UnityObjectRef<GraphicsEventTunnel<int> > tunnelD;
        public ulong                                     graphicsSyncComponentStableHashA;
        public ulong                                     graphicsSyncComponentStableHashB;
        public ulong                                     graphicsSyncComponentStableHashC;
        public ulong                                     graphicsSyncComponentStableHashD;
    }

    public class GraphicsSyncGatherRegistration
    {
        public unsafe T GetCodeAssignedComponent<T>() where T : unmanaged, IComponentData
        {
            if (nextByte + UnsafeUtility.SizeOf<T>() >= 256)
                throw new System.InvalidOperationException("Too many bytes used in a single registration pass.");
            T     result = default(T);
            byte* ptr    = (byte*)&result;
            var   range  = new AssignmentRange
            {
                type         = ComponentType.ReadWrite<T>(),
                start        = nextByte,
                count        = (byte)UnsafeUtility.SizeOf<T>(),
                bitPackStart = 0,
                bitPackCount = 0
            };
            for (int i = 0; i < range.count; i++)
            {
                ptr[i] = nextByte;
                nextByte++;
            }
            assignmentRanges.Add(range);
            return result;
        }

        public unsafe T GetCodeAssignedBitPack<T>(BitPackBuilder<T> builder) where T : unmanaged
        {
            if (nextByte + UnsafeUtility.SizeOf<T>() >= 256)
                throw new System.InvalidOperationException("Too many bytes used in a single registration pass.");

            T     result = default(T);
            byte* ptr    = (byte*)&result;
            var   range  = new AssignmentRange
            {
                type         = default,
                start        = nextByte,
                count        = (byte)UnsafeUtility.SizeOf<T>(),
                bitPackStart = (byte)bitPackRanges.Length,
                bitPackCount = 0
            };
            for (int i = 0; i < range.count; i++)
            {
                ptr[i] = nextByte;
                nextByte++;
            }
            assignmentRanges.Add(range);
            return result;
        }

        public unsafe struct BitPackBuilder<TResult> where TResult : unmanaged
        {
            internal UnsafeList<BitPackRange> pendingRanges;
            internal fixed ulong              usedBits[32];

            public BitPackBuilder<TResult> AddEnabledBit<TComponent>(int bitIndex) where TComponent : unmanaged, IEnableableComponent
            {
                SetAndTestBits(bitIndex, 1);
                pendingRanges.Add(new BitPackRange
                {
                    type              = ComponentType.ReadWrite<TComponent>(),
                    componentBitStart = 0,
                    componentBitCount = 1,
                    isEnabledBit      = true,
                    packBitStart      = bitIndex
                });
                return this;
            }

            public BitPackBuilder<TResult> AddBitsFromComponent<TComponent>(short componentBitStart, short bitCount, short packBitStart)
            {
                SetAndTestBits(packBitStart, bitCount);
                pendingRanges.Add(new BitPackRange
                {
                    type              = ComponentType.ReadWrite<TComponent>(),
                    componentBitStart = componentBitStart,
                    componentBitCount = bitCount,
                    isEnabledBit      = false,
                    packBitStart      = packBitStart
                });
                return this;
            }

            internal void SetAndTestBits(int firstBit, int bitCount)
            {
                if (firstBit + bitCount > 8 * UnsafeUtility.SizeOf<TResult>() || firstBit < 0 || bitCount < 1)
                    throw new System.ArgumentOutOfRangeException();

                for (int i = 0; i < bitCount; i++)
                {
                    var index      = firstBit + i;
                    var ulongIndex = index >> 6;
                    var bitIndex   = index & 0x3f;
                    var field      = new BitField64(usedBits[ulongIndex]);
                    if (field.IsSet(index))
                        throw new System.ArgumentException($"Bit {firstBit + i} is already used");
                    field.SetBits(index, true);
                    usedBits[ulongIndex] = field.Value;
                }
            }
        }

        internal struct AssignmentRange
        {
            public ComponentType type;
            public int           start;
            public int           count;
            public int           bitPackStart;
            public int           bitPackCount;
        }

        internal struct BitPackRange
        {
            public ComponentType type;
            public int           componentBitStart;
            public int           componentBitCount;
            public int           packBitStart;
            public bool          isEnabledBit;
        }

        internal UnsafeList<AssignmentRange> assignmentRanges;
        internal UnsafeList<BitPackRange>    bitPackRanges;
        internal byte                        nextByte;
    }
}

