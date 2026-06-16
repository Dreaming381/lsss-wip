using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace UnitTests
{
    [BurstCompile]
    public class PhysicsTests
    {
        [Test]
        public void SegmentSegmentTests()
        {
            SegmentSegment(float3.zero, new float3(1f, 0f, 0f), new float3(2f, 0f, 0f), new float3(-1f, 0f, 0f), out var closestAOut, out var closestBOut, out var isStartEndAB);
            Assert.IsTrue(math.all(math.isfinite(closestAOut)));
            Assert.IsTrue(math.all(math.isfinite(closestBOut)));
            SegmentSegment(new float3(1f, 0f, 0f), new float3(-1f, 0f, 0f), new float3(2f, 0f, 0f), new float3(-1f, 0f, 0f), out closestAOut, out closestBOut, out isStartEndAB);
            Assert.IsTrue(math.all(math.isfinite(closestAOut)));
            Assert.IsTrue(math.all(math.isfinite(closestBOut)));
            var startA = new float3(238907f, 2378.34f, 23974.23f);
            var endA   = new float3(4523.2346f, 34678.3426f, 2347.34783f);
            var startB = endA;
            var endB   = new float3(39875.32f, 209348.2389f, 23897.3287f);
            SegmentSegment(startA, endA - startA, startB, endB - startB, out closestAOut, out closestBOut, out isStartEndAB);
            closestAOut = math.select(closestAOut, startA, isStartEndAB.x);
            closestAOut = math.select(closestAOut, endA, isStartEndAB.y);
            closestBOut = math.select(closestBOut, startB, isStartEndAB.z);
            closestBOut = math.select(closestBOut, endB, isStartEndAB.w);
            UnityEngine.Debug.Log($"endA: {endA}, closestA: {closestAOut}, closestB: {closestBOut}, isStartEndAB: {isStartEndAB}");
            Assert.IsTrue(math.all(math.isfinite(closestAOut)));
            Assert.IsTrue(math.all(math.isfinite(closestBOut)));
            Assert.IsTrue(closestAOut.Equals(endA));
            Assert.IsTrue(closestBOut.Equals(startB));
        }

        // Todo: Copied from Unity.Physics. I still don't fully understand this, but it is working correctly for degenerate segments somehow.
        // I tested with parallel segments, segments with 0-length edges and a few other weird things. It holds up with pretty good accuracy.
        // I'm not sure where the NaNs or infinities disappear. But they do.
        // Find the closest points on a pair of line segments
        [BurstCompile]
        internal static void SegmentSegment(in float3 pointA,
                                            in float3 edgeA,
                                            in float3 pointB,
                                            in float3 edgeB,
                                            out float3 closestAOut,
                                            out float3 closestBOut,
                                            out bool4 isStartEndAB)
        {
            // Find the closest point on edge A to the line containing edge B
            float3 diff = pointB - pointA;

            float r         = math.dot(edgeA, edgeB);
            float s1        = math.dot(edgeA, diff);
            float s2        = math.dot(edgeB, diff);
            float lengthASq = math.lengthsq(edgeA);
            float lengthBSq = math.lengthsq(edgeB);

            float invDenom, invLengthASq, invLengthBSq;
            {
                float  denom = lengthASq * lengthBSq - r * r;
                float3 inv   = 1.0f / new float3(denom, lengthASq, lengthBSq);
                invDenom     = inv.x;
                invLengthASq = inv.y;
                invLengthBSq = inv.z;
            }

            float fracA = (s1 * lengthBSq - s2 * r) * invDenom;
            fracA       = math.clamp(fracA, 0.0f, 1.0f);

            // Find the closest point on edge B to the point on A just found
            float fracB = fracA * (invLengthBSq * r) - invLengthBSq * s2;
            fracB       = math.clamp(fracB, 0.0f, 1.0f);

            // If the point on B was clamped then there may be a closer point on A to the edge
            fracA = fracB * (invLengthASq * r) + invLengthASq * s1;
            fracA = math.clamp(fracA, 0.0f, 1.0f);

            closestAOut = pointA + fracA * edgeA;
            closestBOut = pointB + fracB * edgeB;

            isStartEndAB = new float4(fracA, fracA, fracB, fracB) == new float4(0f, 1f, 0f, 1f);
        }
    }
}

