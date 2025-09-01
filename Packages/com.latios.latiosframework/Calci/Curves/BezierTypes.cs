using System.Diagnostics;
using Unity.Mathematics;

namespace Latios.Calci
{
    /// <summary>
    /// A vertex with tangents that describes one end of a curve within a spline
    /// </summary>
    public struct BezierKnot
    {
        public float3 position;
        public float3 tangentIn;  // Always pointing away from position
        public float3 tangentOut;  // Always pointing away from position

        /// <summary>
        /// Constructs a knot from a position and the two tangents. If one tangent is in the exact opposite direction of the other,
        /// the spline point is smooth.
        /// </summary>
        /// <param name="position">The position of the knot</param>
        /// <param name="tangentIn">The tangent pointing in the direction that nearby previous points along the spline belong to</param>
        /// <param name="tangentOut">The tangent pointing in the direction that nearby following points along the spline belong to</param>
        public BezierKnot(float3 position, float3 tangentIn, float3 tangentOut)
        {
            this.position   = position;
            this.tangentIn  = tangentIn;
            this.tangentOut = tangentOut;
        }

        /// <summary>
        /// Constructs a knot that defines a beginning of a spline, using the first curve of the spline
        /// </summary>
        /// <param name="curve">The first curve of a spline, in which endpointA is used as the knot point</param>
        /// <returns>A knot based on endpointA of the curve with smooth tangents</returns>
        public static BezierKnot FromCurveEndpointA(in BezierCurve curve)
        {
            var tangentOut = curve.controlA - curve.endpointA;
            return new BezierKnot(curve.endpointA, -tangentOut, tangentOut);
        }

        /// <summary>
        /// Constructs a knot that defines an end of a spline, using the last curve of the spline
        /// </summary>
        /// <param name="curve">The last curve of a spline, in which endpointB is used as the knot point</param>
        /// <returns>A knot based on endpointB of the curve with smooth tangents</returns>
        public static BezierKnot FromCurveEndpointB(in BezierCurve curve)
        {
            var tangentIn = curve.controlB - curve.endpointB;
            return new BezierKnot(curve.endpointA, tangentIn, -tangentIn);
        }

        /// <summary>
        /// Constructs a knot that defines a middle point of a spline between two curves
        /// </summary>
        /// <param name="curveA">The curve before the knot within the spline, where endpointB should be equal to the knot position</param>
        /// <param name="curveB">The curve after the knot within the spline, where endpointA should be equal to the knot position</param>
        /// <returns>A knot at the point that connects the two curves, with tangents compatible with the curves</returns>
        public static BezierKnot FromTwoCurves(in BezierCurve curveA, in BezierCurve curveB)
        {
            EndpointsMatch(curveA.endpointB, curveB.endpointA);
            return new BezierKnot(curveA.endpointB, curveA.controlB - curveA.endpointB, curveB.controlA - curveA.endpointB);
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        static void EndpointsMatch(float3 fromA, float3 fromB)
        {
            if (!fromA.Equals(fromB))
                throw new System.ArgumentException($"The two curves do not connect. curveA.endpointB {fromA} != curveB.endpointA {fromB}");
        }
    }

    /// <summary>
    /// A cubic bezier curve in 3D space
    /// </summary>
    public struct BezierCurve
    {
        public float3 endpointA;  // also known as control point 0
        public float3 controlA;  // also known as control point 1
        public float3 controlB;  // also known as control point 2
        public float3 endpointB;  // also known as control point 3

        /// <summary>
        /// Construct the cubic bezier curve from the 4 control points in sequence
        /// </summary>
        public BezierCurve(float3 endpointA, float3 controlA, float3 controlB, float3 endpointB)
        {
            this.endpointA = endpointA;
            this.controlA  = controlA;
            this.controlB  = controlB;
            this.endpointB = endpointB;
        }

        /// <summary>
        /// Constructs a cubic bezier curve that defines a straight line segment from the passed in point a to point b
        /// </summary>
        /// <param name="a">Segment point a which should become the curve's endpointA</param>
        /// <param name="b">Segment point b which should become the curve's endpointB</param>
        /// <returns>A bezier curve that is a straight line segment</returns>
        public static BezierCurve FromLineSegment(float3 a, float3 b)
        {
            return new BezierCurve(a, b, a, b);
        }

        /// <summary>
        /// Constructs a cubic bezier curve that matches the passed in quadratic bezier curve control points
        /// </summary>
        /// <param name="endpointA">The first endpoint of the quadratic bezier curve</param>
        /// <param name="control">The intermediate control point of the quadratic bezier curve</param>
        /// <param name="endpointB">The second endpoint of the quadratic bezier curve</param>
        /// <returns>A cubic bezier curve that is identical to the quadratic bezier curve</returns>
        public static BezierCurve FromQuadratic(float3 endpointA, float3 control, float3 endpointB)
        {
            float3 tangent = 2f / 3f * control;
            return new BezierCurve(endpointA, endpointA / 3f + tangent, endpointB / 3f + tangent, endpointB);
        }

        /// <summary>
        /// Constructs a cubic bezier curve given the two endpoint bezier knots
        /// </summary>
        /// <param name="knotA">The first knot that defines the start of the bezier curve</param>
        /// <param name="knotB">The second knot that defines the end of the bezier curve</param>
        /// <returns>A bezier curve that connects the two knots</returns>
        public static BezierCurve FromKnots(in BezierKnot knotA, in BezierKnot knotB)
        {
            return new BezierCurve(knotA.position, knotA.position + knotA.tangentOut, knotB.position + knotB.tangentIn, knotB.position);
        }

        /// <summary>
        /// Flips the endpoints and direction of the bezier curve, while preserving the overall shape.
        /// Any factor t evaluated for the original curve will be equal to 1 - t evaluated for the flipped curve.
        /// </summary>
        /// <returns>The bezier curve flipped around</returns>
        public BezierCurve ToFlipped() => new BezierCurve(endpointB, controlB, controlA, endpointA);

        /// <summary>
        /// 32 segment subdivision lengths of the bezier curve
        /// </summary>
        public unsafe struct SegmentLengths
        {
            public fixed float lengths[32];
        }
    }
}

