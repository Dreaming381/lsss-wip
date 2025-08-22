using System;
using Unity.Mathematics;

namespace Latios
{
    [Unity.Burst.BurstCompile]
    public static partial class BezierMath
    {
        /// <summary>
        /// Evaluates the curve at the parameter t in the range [0, 1]
        /// </summary>
        public static float3 PositionAt(in BezierCurve curve, float t)
        {
            var t2     = t * t;
            var t3     = t2 * t;
            var coeffs = new float4(-1f, 3f, -3f, 1f) * t3 +
                         new float4(3f, -6f, 3f, 0f) * t2 +
                         new float4(-3f, 3f, 0f, 0f) * t;
            coeffs.x += 1f;
            return coeffs.x * curve.endpointA + coeffs.y * curve.controlA + coeffs.z * curve.controlB + coeffs.w * curve.endpointB;
        }

        /// <summary>
        /// Evaluates the curve heading multiplied by speed (relative to a period from t = 0 to t = 1) at the parameter t in the range [0, 1]
        /// </summary>
        public static float3 VelocityAt(in BezierCurve curve, float t)
        {
            var t2     = t * t;
            var coeffs = new float4(-3f, 9f, -9f, 3f) * t2 +
                         new float4(6f, -12f, 6f, 0f) * t +
                         new float4(-3f, 3f, 0f, 0f);
            return coeffs.x * curve.endpointA + coeffs.y * curve.controlA + coeffs.z * curve.controlB + coeffs.w * curve.endpointB;
        }

        /// <summary>
        /// Evaluates the directional acceleration (relative to a period from t = 0 to t = 1) at the parameter t in the range [0, 1]
        /// </summary>
        public static float3 AccelerationAt(in BezierCurve curve, float t)
        {
            var coeffs = new float4(-6f, 18f, -18f, 6f) * t +
                         new float4(6f, -12f, 6f, 0f);
            return coeffs.x * curve.endpointA + coeffs.y * curve.controlA + coeffs.z * curve.controlB + coeffs.w * curve.endpointB;
        }

        [Unity.Burst.BurstCompile]
        [Unity.Burst.CompilerServices.SkipLocalsInit]
        public unsafe static void SegmentLengthsOf(in BezierCurve curve, out BezierCurve.SegmentLengths lengths)
        {
            Span<float> pointsX = stackalloc float[33];
            Span<float> pointsY = stackalloc float[33];
            Span<float> pointsZ = stackalloc float[33];

            var endpointAx = curve.endpointA.x;
            var endpointAy = curve.endpointA.y;
            var endpointAz = curve.endpointA.z;
            var controlAx  = curve.controlA.x;
            var controlAy  = curve.controlA.y;
            var controlAz  = curve.controlA.z;
            var controlBx  = curve.controlB.x;
            var controlBy  = curve.controlB.y;
            var controlBz  = curve.controlB.z;
            var endpointBx = curve.endpointB.x;
            var endpointBy = curve.endpointB.y;
            var endpointBz = curve.endpointB.z;

            for (int i = 0; i < 32; i++)
            {
                var t       = i / 32f;
                var t2      = t * t;
                var t3      = t2 * t;
                var coeffsX = -t3 + 3f * t2 - 2f * t + 1f;
                var coeffsY = 3f * t3 - 6f * t2 + 3f * t;
                var coeffsZ = -3f * t3 + 3f * t2;
                //var coeffsW = t3;
                pointsX[i] = coeffsX * endpointAx + coeffsY * controlAx + coeffsZ * controlBx + t3 * endpointBx;
                pointsY[i] = coeffsX * endpointAy + coeffsY * controlAy + coeffsZ * controlBy + t3 * endpointBy;
                pointsZ[i] = coeffsX * endpointAz + coeffsY * controlAz + coeffsZ * controlBz + t3 * endpointBz;
            }
            pointsX[32] = endpointBx;
            pointsY[32] = endpointBy;
            pointsZ[32] = endpointBz;

            for (int i = 0; i < 32; i++)
            {
                lengths.lengths[i] = math.sqrt(math.square(pointsX[i + 1] - pointsX[i]) + math.square(pointsY[i + 1] - pointsY[i]) + math.square(pointsZ[i + 1] - pointsZ[i]));
            }
        }
    }
}

