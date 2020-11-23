using Unity.Mathematics;

namespace Latios
{
    public static partial class simd
    {
        public static float4 dot(simdFloat3 a, float3 b) => a.x * b.x + a.y * b.y + a.z * b.z;
        public static float4 dot(float3 a, simdFloat3 b) => a.x * b.x + a.y * b.y + a.z * b.z;
        public static float4 dot(simdFloat3 a, simdFloat3 b) => a.x * b.x + a.y * b.y + a.z * b.z;

        public static simdFloat3 cross(simdFloat3 a, float3 b)
        {
            return new simdFloat3 { m_float3s = new float4x3(a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x) };
        }
        public static simdFloat3 cross(float3 a, simdFloat3 b)
        {
            return new simdFloat3 { m_float3s = new float4x3(a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x) };
        }
        public static simdFloat3 cross(simdFloat3 a, simdFloat3 b)
        {
            return new simdFloat3 { m_float3s = new float4x3(a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x) };
        }

        public static float4 distancesq(simdFloat3 a, simdFloat3 b)
        {
            var t = a - b;
            return dot(t, t);
        }

        public static float4 lengthsq(simdFloat3 a) => dot(a, a);

        public static simdFloat3 select(simdFloat3 a, simdFloat3 b, bool4 c)
        {
            simdFloat3 result = default;
            result.x          = math.select(a.x, b.x, c);
            result.y          = math.select(a.y, b.y, c);
            result.z          = math.select(a.z, b.z, c);
            return result;
        }

        public static float3 shuffle(simdFloat3 left, simdFloat3 right, math.ShuffleComponent shuffleA)
        {
            float3 result = default;
            result.x      = math.shuffle(left.x, right.x, shuffleA);
            result.y      = math.shuffle(left.y, right.y, shuffleA);
            result.z      = math.shuffle(left.z, right.z, shuffleA);
            return result;
        }

        public static simdFloat3 shuffle(simdFloat3 left,
                                         simdFloat3 right,
                                         math.ShuffleComponent shuffleA,
                                         math.ShuffleComponent shuffleB,
                                         math.ShuffleComponent shuffleC,
                                         math.ShuffleComponent shuffleD)
        {
            simdFloat3 result = default;
            result.x          = math.shuffle(left.x, right.x, shuffleA, shuffleB, shuffleC, shuffleD);
            result.y          = math.shuffle(left.y, right.y, shuffleA, shuffleB, shuffleC, shuffleD);
            result.z          = math.shuffle(left.z, right.z, shuffleA, shuffleB, shuffleC, shuffleD);
            return result;
        }

        public static simdFloat3 transform(RigidTransform transform, simdFloat3 positions)
        {
            simdFloat3 t       = 2 * cross(transform.rot.value.xyz, positions);
            var        rotated = positions + transform.rot.value.w * t + cross(transform.rot.value.xyz, t);
            return rotated + transform.pos;
        }

        public static simdFloat3 mul(quaternion rotation, simdFloat3 directions)
        {
            simdFloat3 t = 2 * cross(rotation.value.xyz, directions);
            return directions + rotation.value.w * t + cross(rotation.value.xyz, t);
        }

        public static float3 cminabcd(simdFloat3 s)
        {
            return new float3(math.cmin(s.x), math.cmin(s.y), math.cmin(s.z));
        }

        public static float3 cmaxabcd(simdFloat3 s)
        {
            return new float3(math.cmax(s.x), math.cmax(s.y), math.cmax(s.z));
        }
    }
}

