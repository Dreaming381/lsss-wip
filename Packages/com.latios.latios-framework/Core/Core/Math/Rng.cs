using Unity.Mathematics;

namespace Latios
{
    public struct Rng
    {
        uint m_state;

        public Rng(uint seed)
        {
            m_state = seed;
        }

        public Rng(string seedString)
        {
            m_state = math.asuint(seedString.GetHashCode());
        }

        public Rng Update()
        {
            var sequence = new RngSequence(new uint2(m_state, math.asuint(int.MinValue)));
            m_state      = sequence.NextUInt();
            return this;
        }

        public RngSequence GetSequence(int index)
        {
            return new RngSequence(new uint2(math.asuint(index), m_state));
        }

        public struct RngSequence
        {
            uint2 m_state;

            public RngSequence(uint2 initialState)
            {
                m_state = initialState;
            }

            uint NextState()
            {
                //From https://www.youtube.com/watch?v=LWFzPP8ZbdU
                var val    = m_state.x * 0x68e31da4;
                val       *= m_state.y;
                val       ^= (val >> 8);
                val       += 0xb5297a4d;
                val       ^= (val << 8);
                val       *= 0x1b56c4e9;
                val       ^= (val >> 8);
                m_state.x  = val;
                return val;
            }

            public bool NextBool() => RngToolkit.AsBool(NextState());
            public bool2 NextBool2() => RngToolkit.AsBool2(NextState());
            public bool3 NextBool3() => RngToolkit.AsBool3(NextState());
            public bool4 NextBool4() => RngToolkit.AsBool4(NextState());

            public uint NextUInt() => NextState();
            public uint2 NextUInt2() => new uint2(NextState(), NextState());
            public uint3 NextUInt3() => new uint3(NextState(), NextState(), NextState());
            public uint4 NextUInt4() => new uint4(NextState(), NextState(), NextState(), NextState());
            public uint  NextUInt (uint min, uint max) => RngToolkit.AsUInt(NextState(), min, max);
            public uint2 NextUInt2(uint2 min, uint2 max) => RngToolkit.AsUInt2(NextUInt2(), min, max);
            public uint3 NextUInt3(uint3 min, uint3 max) => RngToolkit.AsUInt3(NextUInt3(), min, max);
            public uint4 NextUInt4(uint4 min, uint4 max) => RngToolkit.AsUInt4(NextUInt4(), min, max);

            public int NextInt() => RngToolkit.AsInt(NextState());
            public int2 NextInt2() => RngToolkit.AsInt2(NextUInt2());
            public int3 NextInt3() => RngToolkit.AsInt3(NextUInt3());
            public int4 NextInt4() => RngToolkit.AsInt4(NextUInt4());
            public int NextInt(int min, int max) => RngToolkit.AsInt(NextState(), min, max);
            public int2 NextInt2(int2 min, int2 max) => RngToolkit.AsInt2(NextUInt2(), min, max);
            public int3 NextInt3(int3 min, int3 max) => RngToolkit.AsInt3(NextUInt3(), min, max);
            public int4 NextInt4(int4 min, int4 max) => RngToolkit.AsInt4(NextUInt4(), min, max);

            public float NextFloat() => RngToolkit.AsFloat(NextState());
            public float2 NextFloat2() => RngToolkit.AsFloat2(NextUInt2());
            public float3 NextFloat3() => RngToolkit.AsFloat3(NextUInt3());
            public float4 NextFloat4() => RngToolkit.AsFloat4(NextUInt4());
            public float NextFloat(float min, float max) => RngToolkit.AsFloat(NextState(), min, max);
            public float2 NextFloat2(float2 min, float2 max) => RngToolkit.AsFloat2(NextUInt2(), min, max);
            public float3 NextFloat3(float3 min, float3 max) => RngToolkit.AsFloat3(NextUInt3(), min, max);
            public float4 NextFloat4(float4 min, float4 max) => RngToolkit.AsFloat4(NextUInt4(), min, max);

            public float2 NextFloat2Direction() => RngToolkit.AsFloat2Direction(NextState());
            public float3 NextFloat3Direction() => RngToolkit.AsFloat3Direction(NextUInt2());
        }
    }
}

