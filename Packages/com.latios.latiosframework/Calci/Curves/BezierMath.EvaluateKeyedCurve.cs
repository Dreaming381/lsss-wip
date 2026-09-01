using Unity.Collections;
using Unity.Mathematics;

namespace Latios.Calci
{
    public static partial class BezierMath
    {
        /// <summary>
        /// Evaluates the curve at the specified time. The time passed in must be within the time range the curve is valid for,
        /// or else this method may produce unusable results including infinities and NaNs.
        /// </summary>
        /// <param name="curve">The curve to evaluate</param>
        /// <param name="time">The time value to evaluate</param>
        /// <returns>The evaluation of the curve</returns>
        public static float Evaluate(in KeyedCurve curve, float time)
        {
            if (curve.leftTangentWeight == Keyframe.kHermite && curve.rightTangentWeight == Keyframe.kHermite)
            {
                // Use hermite interpolation
                // The following implementation is taken from the old Unity.Animation package.
                // The hermite function is:
                // (2 * t^3 -3 * t^2 +1) * p0 + (t^3 - 2 * t^2 + t) * m0 + (-2 * t^3 + 3 * t^2) * p1 + (t^3 - t^2) * m1
                // The key observation here is that most of the terms are t^3 and t^2. We can factor out t^2 from these
                // terms and sum the terms first which leads to more floating point stability.
                // Additionally, this factored form happens to be more efficient. Ignoring loads, the optimal implementation
                // is fully scalar with FMAs. However, there might be an SLP optimization geared towards vector loads.
                var dx = curve.rightTime - curve.leftTime;
                var t  = (time - curve.leftTime) / dx;
                var p0 = curve.leftValue;
                var m0 = curve.leftTangentSlope * dx;
                var p1 = curve.rightValue;
                var m1 = curve.rightTangentSlope * dx;

                var a = 2.0f * p0 + m0 - 2.0f * p1 + m1;
                var b = -3.0f * p0 - 2.0f * m0 + 3.0f * p1 - m1;
                var c = m0;
                var d = p0;

                return t * (t * (a * t + b) + c) + d;
            }
            else
            {
                // Use cubic bezier interpolation

                var dx = curve.rightTime - curve.leftTime;
                var t  = FindT((time - curve.leftTime) / dx, curve.leftTangentWeight, 1f - curve.rightTangentWeight);
                var p0 = curve.leftValue;
                var p1 = curve.leftValue + dx * curve.leftTangentSlope * curve.leftTangentWeight;
                var p2 = curve.rightValue - dx * curve.rightTangentSlope * curve.rightTangentWeight;
                var p3 = curve.rightValue;

                float t2   = t * t;
                float t3   = t2 * t;
                float omt  = 1f - t;
                float omt2 = omt * omt;
                float omt3 = omt2 * omt;

                return omt3 * p0 + 3f * t * omt2 * p1 + 3f * t2 * omt * p2 + t3 * p3;
            }
        }
    }
}

