using Latios.Transforms;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Latios.Calci
{
    public static partial class BezierMath
    {
        /// <summary>
        /// Splits the curve at factor t into two parts which when concatenated represent the path of the original curve
        /// </summary>
        /// <param name="curve">The curve to split</param>
        /// <param name="t">The factor along the curve to split</param>
        /// <param name="outA">The first split curve which shares endpointA with the original</param>
        /// <param name="outB">The second split curve which shares endpointB with the original</param>
        public static void SplitCurve(in BezierCurve curve, float t, out BezierCurve outA, out BezierCurve outB)
        {
            // Lerp the 3 control segments
            var splitA = math.lerp(curve.endpointA, curve.controlA, t);
            var splitB = math.lerp(curve.controlA, curve.controlB, t);
            var splitC = math.lerp(curve.controlB, curve.endpointB, t);

            // Lerp the lerps
            var splitAB = math.lerp(splitA, splitB, t);
            var splitBC = math.lerp(splitB, splitC, t);

            // Lerp the lerped lerps
            var superSplit = math.lerp(splitAB, splitBC, t);

            outA = new BezierCurve(curve.endpointA, splitA, splitAB, superSplit);
            outB = new BezierCurve(superSplit, splitBC, splitC, curve.endpointB);
        }

        /// <summary>
        /// Applies translation, rotation, scale, and stretch to the knot.
        /// </summary>
        /// <param name="knot">The know to be transformed</param>
        /// <param name="transform">The transform to apply to the knot</param>
        /// <returns>The resulting transformed knot</returns>
        public static BezierKnot TransformKnot(in BezierKnot knot, in TransformQvvs transform)
        {
            var newPosition   = qvvs.TransformPoint(in transform, knot.position);
            var newTangentIn  = qvvs.TransformDirectionScaledAndStretched(in transform, knot.tangentIn);
            var newTangentOut = qvvs.TransformDirectionScaledAndStretched(in transform, knot.tangentOut);
            return new BezierKnot(newPosition, newTangentIn, newTangentOut);
        }

        // Todo: More transforming curves.
    }
}

