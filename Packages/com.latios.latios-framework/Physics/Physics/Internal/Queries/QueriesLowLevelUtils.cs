using Unity.Mathematics;

namespace Latios.PhysicsEngine
{
    internal static class QueriesLowLevelUtils
    {
        //Todo: Copied from Unity.Physics. I still don't fully understand this, but it is working correctly for degenerate segments somehow.
        //I tested with parallel segments, segments with 0-length edges and a few other weird things. It holds up with pretty good accuracy.
        //I'm not sure where the NaNs or infinities disappear. But they do.
        // Find the closest points on a pair of line segments
        internal static void SegmentSegment(float3 pointA, float3 edgeA, float3 pointB, float3 edgeB, out float3 closestAOut, out float3 closestBOut)
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
        }

        internal static void SegmentSegment(simdFloat3 pointA, simdFloat3 edgeA, simdFloat3 pointB, simdFloat3 edgeB, out simdFloat3 closestAOut, out simdFloat3 closestBOut)
        {
            simdFloat3 diff = pointB - pointA;

            float4 r         = simd.dot(edgeA, edgeB);
            float4 s1        = simd.dot(edgeA, diff);
            float4 s2        = simd.dot(edgeB, diff);
            float4 lengthASq = simd.lengthsq(edgeA);
            float4 lengthBSq = simd.lengthsq(edgeB);

            float4 invDenom, invLengthASq, invLengthBSq;
            {
                float4 denom = lengthASq * lengthBSq - r * r;
                invDenom     = 1.0f / denom;
                invLengthASq = 1.0f / lengthASq;
                invLengthBSq = 1.0f / lengthBSq;
            }

            float4 fracA = (s1 * lengthBSq - s2 * r) * invDenom;
            fracA        = math.clamp(fracA, 0.0f, 1.0f);

            float4 fracB = fracA * (invLengthBSq * r) - invLengthBSq * s2;
            fracB        = math.clamp(fracB, 0.0f, 1.0f);

            fracA = fracB * invLengthASq * r + invLengthASq * s1;
            fracA = math.clamp(fracA, 0.0f, 1.0f);

            closestAOut = pointA + fracA * edgeA;
            closestBOut = pointB + fracB * edgeB;
        }

        internal static void OriginAabb8Points(float3 aabb, simdFloat3 points03, simdFloat3 points47, out float3 closestAOut, out float3 closestBOut, out float axisDistanceOut)
        {
            bool4 minXMask0347 = points03.x <= points47.x;
            bool4 minYMask0347 = points03.y <= points47.y;
            bool4 minZMask0347 = points03.z <= points47.z;

            var minX0347 = simd.select(points47, points03, minXMask0347);
            var maxX0347 = simd.select(points03, points47, minXMask0347);
            var minY0347 = simd.select(points47, points03, minYMask0347);
            var maxY0347 = simd.select(points03, points47, minYMask0347);
            var minZ0347 = simd.select(points47, points03, minZMask0347);
            var maxZ0347 = simd.select(points03, points47, minZMask0347);

            float minXValue = math.cmin(minX0347.x);
            float maxXValue = math.cmax(maxX0347.x);
            float minYValue = math.cmin(minY0347.y);
            float maxYValue = math.cmax(maxY0347.y);
            float minZValue = math.cmin(minZ0347.z);
            float maxZValue = math.cmax(maxZ0347.z);

            int3 minIndicesFrom0347;
            int3 maxIndicesFrom0347;
            minIndicesFrom0347.x = math.tzcnt(math.bitmask(minXValue == minX0347.x));
            maxIndicesFrom0347.x = math.tzcnt(math.bitmask(maxXValue == maxX0347.x));
            minIndicesFrom0347.y = math.tzcnt(math.bitmask(minYValue == minY0347.y));
            maxIndicesFrom0347.y = math.tzcnt(math.bitmask(maxYValue == maxY0347.y));
            minIndicesFrom0347.z = math.tzcnt(math.bitmask(minZValue == minZ0347.z));
            maxIndicesFrom0347.z = math.tzcnt(math.bitmask(maxZValue == maxZ0347.z));

            var bestMins = simd.shuffle(minX0347,
                                        minX0347,
                                        (math.ShuffleComponent)minIndicesFrom0347.x,
                                        (math.ShuffleComponent)minIndicesFrom0347.y,
                                        (math.ShuffleComponent)minIndicesFrom0347.z,
                                        (math.ShuffleComponent)minIndicesFrom0347.x);
            var bestMaxs = simd.shuffle(maxX0347,
                                        maxX0347,
                                        (math.ShuffleComponent)maxIndicesFrom0347.x,
                                        (math.ShuffleComponent)maxIndicesFrom0347.y,
                                        (math.ShuffleComponent)maxIndicesFrom0347.z,
                                        (math.ShuffleComponent)maxIndicesFrom0347.x);

            float3 minValues = new float3(minXValue, minYValue, minZValue);
            float3 maxValues = new float3(maxXValue, maxYValue, maxZValue);

            float3 distancesToMin = maxValues + aabb;
            float3 distancesToMax = aabb - minValues;
            float3 minDistances   = math.min(distancesToMin, distancesToMax);
            float  bestDistance   = math.cmin(minDistances);
            bool3  bestAxisMask   = bestDistance == minDistances;
            //Prioritize y first, then z, then x if multiple distances perfectly match.
            //Todo: Should this be configurabe?
            bestAxisMask.xz  &= !bestAxisMask.y;
            bestAxisMask.x   &= !bestAxisMask.z;
            float3 zeroMask   = math.select(0f, 1f, bestAxisMask);
            bool   useMin     = (minDistances * zeroMask).Equals(distancesToMin * zeroMask);
            int    bestIndex  = math.tzcnt(math.bitmask(new bool4(bestAxisMask, true)));
            closestBOut       = math.select(bestMins[bestIndex], bestMaxs[bestIndex], useMin);
            closestAOut       = math.select(closestBOut, math.select(aabb, -aabb, useMin), bestAxisMask);
            closestAOut       = math.clamp(closestAOut, -aabb, aabb);
            axisDistanceOut   = -bestDistance;
        }

        public static bool4 ArePointsInsideObb(simdFloat3 points, simdFloat3 obbNormals, float3 distances, float3 halfWidths)
        {
            float3 positives  = distances + halfWidths;
            float3 negatives  = distances - halfWidths;
            var    dots       = simd.dot(points, obbNormals.aaaa);
            bool4  results    = dots <= positives.x;
            results          &= dots >= negatives.x;
            dots              = simd.dot(points, obbNormals.bbbb);
            results          &= dots <= positives.y;
            results          &= dots >= negatives.y;
            dots              = simd.dot(points, obbNormals.cccc);
            results          &= dots <= positives.z;
            results          &= dots >= negatives.z;
            return results;
        }
    }
}

