using Unity.Mathematics;

namespace Latios.Calci
{
    public static partial class BezierMath
    {
        /// <summary>
        /// Computes the integral from the start to end of the curve's time span with respect to time.
        /// </summary>
        /// <param name="curve">The curve to integrate. Its time span must not be zero.</param>
        /// <returns>The integral across the curve's time span.</returns>
        public static float Integrate(in KeyedCurve curve)
        {
            var dx = curve.rightTime - curve.leftTime;
            if (curve.leftTangentWeight == Keyframe.kHermite && curve.rightTangentWeight == Keyframe.kHermite)
                return dx * IntegrateHermiteNormalized(in curve, dx, 1f);
            return dx * IntegrateWeightedNormalized(in curve, dx, 1f);
        }

        /// <summary>
        /// Computes the integral from the start of the curve's time span up to the specified time.
        /// A time value outside the curve's timespan is clamped to the timespan.
        /// </summary>
        /// <param name="curve">The curve to integrate. Its time span must not be zero.</param>
        /// <param name="time">The time to integrate up to</param>
        /// <returns>The integral from the curve's left time to the specified time.</returns>
        public static float Integrate(in KeyedCurve curve, float time)
        {
            var dx       = curve.rightTime - curve.leftTime;
            var fraction = math.saturate((time - curve.leftTime) / dx);
            if (curve.leftTangentWeight == Keyframe.kHermite && curve.rightTangentWeight == Keyframe.kHermite)
                return dx * IntegrateHermiteNormalized(in curve, dx, fraction);

            var t = FindT(fraction, curve.leftTangentWeight, 1f - curve.rightTangentWeight);
            return dx * IntegrateWeightedNormalized(in curve, dx, t);
        }

        // The integral of a Hermite segment over the first fraction of its time span.
        // </summary>
        // <remarks>
        // Hermite is a cubic function with respect to the normalized time axis directly (the fraction).
        // Therefore, the integral is a quartic with respect to time.
        // </remarks>
        static float IntegrateHermiteNormalized(in KeyedCurve curve, float dx, float fraction)
        {
            var p0 = curve.leftValue;
            var m0 = curve.leftTangentSlope * dx;
            var p1 = curve.rightValue;
            var m1 = curve.rightTangentSlope * dx;

            var a = 2f * p0 + m0 - 2f * p1 + m1;
            var b = -3f * p0 - 2f * m0 + 3f * p1 - m1;
            var c = m0;
            var d = p0;

            // The integral of a*t^3 + b*t^2 + c*t + d, in Horner form.
            var t = fraction;
            return t * (d + t * (0.5f * c + t * ((1f / 3f) * b + t * (0.25f * a))));
        }

        // Both axes are cubics of the parameter t here, but we really want the integral of y with
        // respect to x. That is, _/" y * dx. As a parametric, y = g(t), and x = f(t). And therefore
        // dx = f'(t)dt. Thus, our integral is actually _/" g(t) * f'(t)dt which is an integral of a
        // quintic.
        static float IntegrateWeightedNormalized(in KeyedCurve curve, float dx, float t)
        {
            // The control points
            var p0 = curve.leftValue;
            var p1 = curve.leftValue + dx * curve.leftTangentSlope * curve.leftTangentWeight;
            var p2 = curve.rightValue - dx * curve.rightTangentSlope * curve.rightTangentWeight;
            var p3 = curve.rightValue;

            // Value as a power series: y = y3*t^3 + y2*t^2 + y1*t + y0.
            var y0 = p0;
            var y1 = 3f * (p1 - p0);
            var y2 = 3f * (p2 - 2f * p1 + p0);
            var y3 = p3 - 3f * p2 + 3f * p1 - p0;

            // The normalized time axis runs 0, leftWeight, 1 - rightWeight, 1, which collapses to the
            // identity at Hermite weights and is why that path can skip all of this.
            var x1c = curve.leftTangentWeight;
            var x2c = 1f - curve.rightTangentWeight;

            // Time as a power series, then differentiated: x' = 3*x3*t^2 + 2*x2*t + x1.
            var x1 = 3f * x1c;
            var x2 = 3f * (x2c - 2f * x1c);
            var x3 = 1f + 3f * x1c - 3f * x2c;

            var d0 = x1;
            var d1 = 2f * x2;
            var d2 = 3f * x3;

            // Product of the two, giving the quintic to integrate.
            var q0 = y0 * d0;
            var q1 = y0 * d1 + y1 * d0;
            var q2 = y0 * d2 + y1 * d1 + y2 * d0;
            var q3 = y1 * d2 + y2 * d1 + y3 * d0;
            var q4 = y2 * d2 + y3 * d1;
            var q5 = y3 * d2;

            return t * (q0 + t * (0.5f * q1 + t * ((1f / 3f) * q2 + t * (0.25f * q3 + t * (0.2f * q4 + t * ((1f / 6f) * q5))))));
        }
    }
}

