using Unity.Mathematics;

namespace Latios
{
    public struct BezierKnot
    {
        public float3 position;
        public float3 tangentIn;  // Always pointing away from position
        public float3 tangentOut;  // Always pointing away from position

        public BezierKnot(float3 position, float3 tangentIn, float3 tangentOut)
        {
            this.position   = position;
            this.tangentIn  = tangentIn;
            this.tangentOut = tangentOut;
        }
    }

    public struct BezierCurve
    {
        public float3 endpointA;
        public float3 controlA;
        public float3 controlB;
        public float3 endpointB;

        public BezierCurve(float3 endpointA, float3 controlA, float3 controlB, float3 endpointB)
        {
            this.endpointA = endpointA;
            this.controlA  = controlA;
            this.controlB  = controlB;
            this.endpointB = endpointB;
        }

        public static BezierCurve FromLineSegment(float3 a, float3 b)
        {
            return new BezierCurve(a, b, a, b);
        }

        public static BezierCurve FromQuadratic(float3 endpointA, float3 control, float3 endpointB)
        {
            float3 tangent = 2f / 3f * control;
            return new BezierCurve(endpointA, endpointA / 3f + tangent, endpointB / 3f + tangent, endpointB);
        }

        public static BezierCurve FromKnots(in BezierKnot knotA, in BezierKnot knotB)
        {
            return new BezierCurve(knotA.position, knotA.position + knotA.tangentOut, knotB.position + knotB.tangentIn, knotB.position);
        }

        public BezierCurve ToFlipped() => new BezierCurve(endpointB, controlB, controlA, endpointA);

        public unsafe struct SegmentLengths
        {
            public fixed float lengths[32];
        }
    }
}

