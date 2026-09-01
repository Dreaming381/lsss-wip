using Unity.Collections;
using Unity.Mathematics;

namespace Latios.Calci
{
    public static partial class BezierMath
    {
        internal static float FindT(float normalizedTime, float leftWeight, float rightWeight)
        {
            // This implementation is taken from the old Unity.Animation package method BezierExtractU.
            // No attempt has been made to optimize it yet.
            // The algorithm here is simply solving a cubic to find the real root t for a given x on the Bezier curve.
            static float CubeRootPositive(float a) => math.exp(math.log(a) / 3f);
            static float CubeRoot(float a) => a < 0f ? -math.exp(math.log(-a) / 3f) : CubeRootPositive(a);
        
            var t  = normalizedTime;
            var w1 = leftWeight;
            var w2 = rightWeight;
        
            float a = 3f * w1 - 3f * w2 + 1f;
            float b = -6f * w1 + 3f * w2;
            float c = 3f * w1;
            float d = -t;
        
            if (math.abs(a) > 1e-3f)
            {
                float p  = -b / (3f * a);
                float p2 = p * p;
                float p3 = p2 * p;
        
                float q  = p3 + (b * c - 3f * a * d) / (6f * a * a);
                float q2 = q * q;
        
                float r    = c / (3f * a);
                float rmp2 = r - p2;
        
                float s = q2 + rmp2 * rmp2 * rmp2;
        
                if (s < 0f)
                {
                    float ssi = math.sqrt(-s);
                    float r_1 = math.sqrt(-s + q2);
                    float phi = math.atan2(ssi, q);
        
                    float r_3   = CubeRootPositive(r_1);
                    float phi_3 = phi / 3f;
        
                    // Extract cubic roots.
                    float u1 = 2f * r_3 * math.cos(phi_3) + p;
                    float u2 = 2f * r_3 * math.cos(phi_3 + 2f * math.PI / 3.0f) + p;
                    float u3 = 2f * r_3 * math.cos(phi_3 - 2f * math.PI / 3.0f) + p;
        
                    if (u1 >= 0f && u1 <= 1f)
                        return u1;
                    else if (u2 >= 0f && u2 <= 1f)
                        return u2;
                    else if (u3 >= 0f && u3 <= 1f)
                        return u3;
        
                    // Aiming at solving numerical imprecisions when root is outside [0,1].
                    return (t < 0.5f) ? 0f : 1f;
                }
                else
                {
                    float ss = math.sqrt(s);
                    float u  = CubeRoot(q + ss) + CubeRoot(q - ss) + p;
        
                    if (u >= 0f && u <= 1f)
                        return u;
        
                    // Aiming at solving numerical imprecisions when root is outside [0,1].
                    return (t < 0.5f) ? 0f : 1f;
                }
            }
        
            if (math.abs(b) > 1e-3f)
            {
                float s  = c * c - 4f * b * d;
                float ss = math.sqrt(s);
        
                float u1 = (-c - ss) / (2f * b);
                float u2 = (-c + ss) / (2f * b);
        
                if (u1 >= 0f && u1 <= 1f)
                    return u1;
                else if (u2 >= 0f && u2 <= 1f)
                    return u2;
        
                // Aiming at solving numerical imprecisions when root is outside [0,1].
                return (t < 0.5f) ? 0f : 1f;
            }
        
            if (math.abs(c) > 1e-3f)
            {
                return -d / c;
            }
        
            return 0f;
        }
    }
}
