using Latios.Psyshock;
using Latios.Transforms;
using NUnit.Framework;
using Unity.Burst;
using Unity.Burst.CompilerServices;
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

        [BurstCompile]
        internal static unsafe float MaybeSimd(in float3 point, int length, float* aVals, float* bVals, float* cVals)
        {
            var dots = stackalloc float[length];
            for (int i = 0; i < length; i++)
            {
                dots[i] = point.x * aVals[i] + point.y * bVals[i] - point.z * cVals[i];
            }

            float best = float.MinValue;
            for (int i = 0; i < length; i++)
            {
                best = math.max(best, dots[i]);
            }
            return best;
        }

        [BurstCompile]
        internal static unsafe float MaybeSimdMath(in float3 point, int length, float* aVals, float* bVals, float* cVals)
        {
            var dots = stackalloc float[length];
            for (int i = 0; i < length; i++)
            {
                float3 other = new float3(aVals[i], bVals[i], cVals[i]);
                dots[i]      = math.dot(point, other);
            }

            float best = float.MinValue;
            for (int i = 0; i < length; i++)
            {
                best = math.max(best, dots[i]);
            }
            return best;
        }

        [BurstCompile]
        internal static unsafe float DistanceBetween(Collider* colliderA, TransformQvvs* transformA, Collider* colliderB, TransformQvvs* transformB, float maxDistance)
        {
            Physics.DistanceBetween(*colliderA, *transformA, *colliderB, *transformB, maxDistance, out var result);
            return result.distance;
        }

        [BurstCompile]
        internal static unsafe float BoxCapsuleDistanceBurst(BoxCollider* box, CapsuleCollider* capsule, float maxDistance)
        {
            BoxCapsuleDistance(*box, *capsule, maxDistance, out var result);
            return result.distance;
        }

        unsafe struct float12
        {
            public fixed float values[12];

            public float this[int index]
            {
                get => values[index];
                set => values[index] = value;
            }
        }

        static readonly float[] sBoxPointsAxSigns = { -1f, -1f, -1f, -1f, 1f, 1f, -1f, -1f, 1f, 1f, -1f, -1f };
        static readonly float[] sBoxPointsBxSigns = { 1f, 1f, 1f, 1f, 1f, 1f, -1f, -1f, 1f, 1f, -1f, -1f };
        static readonly float[] sBoxPointsAySigns = { 1f, 1f, -1f, -1f, -1f, -1f, -1f, -1f, 1f, -1f, 1f, -1f };
        static readonly float[] sBoxPointsBySigns = { 1f, 1f, -1f, -1f, 1f, 1f, 1f, 1f, 1f, -1f, 1f, -1f };
        static readonly float[] sBoxPointsAzSigns = { 1f, -1f, 1f, -1f, 1f, -1f, 1f, -1f, -1f, -1f, -1f, -1f };
        static readonly float[] sBoxPointsBzSigns = { 1f, -1f, 1f, -1f, 1f, -1f, 1f, -1f, 1f, 1f, 1f, 1f };

        private static bool BoxCapsuleDistance(in BoxCollider box, in CapsuleCollider capsule, float maxDistance, out ColliderDistanceResultInternal result)
        {
            float3 osPointA = capsule.pointA - box.center;  //os = origin space
            float3 osPointB = capsule.pointB - box.center;
            float3 pointsPointOnBox;
            float3 pointsPointOnSegment;
            float  pointsSignedDistanceSq;
            // Step 1: Points vs Planes
            {
                float3 distancesToMin = math.max(osPointA, osPointB) + box.halfSize;
                float3 distancesToMax = box.halfSize - math.min(osPointA, osPointB);
                float3 bestDistances  = math.min(distancesToMin, distancesToMax);
                float  bestDistance   = math.cmin(bestDistances);
                bool3  bestAxisMask   = bestDistance == bestDistances;
                // Prioritize y first, then z, then x if multiple distances perfectly match.
                // Todo: Should this be configurabe?
                bestAxisMask.xz        &= !bestAxisMask.y;
                bestAxisMask.x         &= !bestAxisMask.z;
                bool3 useMin            = bestDistances == distancesToMin;
                bool3 aIsGreater        = osPointA > osPointB;
                bool  useB              = math.any((useMin ^ aIsGreater) & bestAxisMask);
                pointsPointOnSegment    = math.select(osPointA, osPointB, useB);
                pointsPointOnBox        = math.select(pointsPointOnSegment, math.select(box.halfSize, -box.halfSize, useMin), bestAxisMask);
                pointsPointOnBox        = math.clamp(pointsPointOnBox, -box.halfSize, box.halfSize);
                float axisDistance      = -bestDistance;
                float signedDistanceSq  = math.distancesq(pointsPointOnSegment, pointsPointOnBox);
                pointsSignedDistanceSq  = math.select(signedDistanceSq, -signedDistanceSq, axisDistance <= 0f);
            }

            // Step 2: Edge vs Edges
            float3 edgesPointOnSegment;
            float3 edgesPointOnBox;
            float  edgesSignedDistanceSq;
            {
                // Todo: We could inline the SegmentSegment invocations to simplify the initial dot products.
                float3 edgeA        = osPointB - osPointA;
                float  lengthASq    = math.lengthsq(edgeA);
                float  invLengthASq = 1f / lengthASq;

                float12 signedDistanceSqs = default;
                float12 closestAxs        = default;
                float12 closestAys        = default;
                float12 closestAzs        = default;
                float12 closestBxs        = default;
                float12 closestBys        = default;
                float12 closestBzs        = default;
                for (int i = 0; i < 12; i++)
                {
                    // Inline CapsuleCapsule.SegmentSegment
                    // Get the box start segment
                    var startBx = math.chgsign(box.halfSize.x, sBoxPointsAxSigns[i]);
                    var startBy = math.chgsign(box.halfSize.y, sBoxPointsAySigns[i]);
                    var startBz = math.chgsign(box.halfSize.z, sBoxPointsAzSigns[i]);
                    var endBx   = math.chgsign(box.halfSize.x, sBoxPointsBxSigns[i]);
                    var endBy   = math.chgsign(box.halfSize.y, sBoxPointsBySigns[i]);
                    var endBz   = math.chgsign(box.halfSize.z, sBoxPointsBzSigns[i]);
                    // Get the box edge magnitude
                    var edgeB = 2f * math.select(math.select(box.halfSize.x, box.halfSize.y, i >= 4), box.halfSize.z, i >= 8);
                    var diffX = startBx - osPointA.x;
                    var diffY = startBy - osPointA.y;
                    var diffZ = startBz - osPointA.z;

                    var r            = edgeB * math.select(math.select(edgeA.x, edgeA.y, i >= 4), edgeA.z, i >= 8);
                    var s1           = edgeA.x * diffX + edgeA.y * diffY + edgeA.z * diffZ;
                    var s2           = edgeB * math.select(math.select(diffX, diffY, i >= 4), diffZ, i >= 8);
                    var lengthBSq    = edgeB * edgeB;
                    var invDenom     = 1f / (lengthASq * lengthBSq - r * r);
                    var invLengthBSq = 1f / lengthBSq;

                    // Find the closest point on edge A to the line containing edge B
                    float fracA = (s1 * lengthBSq - s2 * r) * invDenom;
                    fracA       = math.clamp(fracA, 0.0f, 1.0f);
                    // Find the closest point on edge B to the point on A just found
                    float fracB = fracA * (invLengthBSq * r) - invLengthBSq * s2;
                    fracB       = math.clamp(fracB, 0.0f, 1.0f);
                    // If the point on B was clamped then there may be a closer point on A to the edge
                    fracA          = fracB * (invLengthASq * r) + invLengthASq * s1;
                    fracA          = math.clamp(fracA, 0.0f, 1.0f);
                    bool fracAIs1  = fracA == 1f;
                    var  closestAx = math.select(osPointA.x + fracA * edgeA.x, osPointB.x, fracAIs1);
                    var  closestAy = math.select(osPointA.y + fracA * edgeA.y, osPointB.y, fracAIs1);
                    var  closestAz = math.select(osPointA.z + fracA * edgeA.z, osPointB.z, fracAIs1);
                    bool fracBIs1  = fracB == 1f;
                    var  closestBx = math.select(math.lerp(startBx, endBx, fracB), endBx, fracBIs1);
                    var  closestBy = math.select(math.lerp(startBy, endBy, fracB), endBy, fracBIs1);
                    var  closestBz = math.select(math.lerp(startBz, endBz, fracB), endBz, fracBIs1);

                    // Evaluate validity of the result.
                    // Imagine a line that goes perpendicular through a box's edge at the midpoint.
                    // All orientations of that line which do not penetrate the box (tangency is not considered penetrating in this case) are validly resolved collisions.
                    // Orientations of the line which do penetrate are not valid.
                    // If we constrain the capsule edge to be perpendicular, normalize it, and then compute the dot product, we can compare that to the necessary 45 degree angle
                    // where penetration occurs. Parallel lines are excluded because either we want to record a capsule point (step 1) or a perpendicular edge on the box.
                    var root2                         = math.SQRT2 / 2f;
                    var muteX                         = math.select(0f, 1f, sBoxPointsAxSigns[i] == sBoxPointsBxSigns[i]);
                    var muteY                         = math.select(0f, 1f, sBoxPointsAySigns[i] == sBoxPointsBySigns[i]);
                    var muteZ                         = math.select(0f, 1f, sBoxPointsAzSigns[i] == sBoxPointsBzSigns[i]);
                    var boxNormalX                    = math.chgsign(root2, sBoxPointsAxSigns[i]) * muteX;
                    var boxNormalY                    = math.chgsign(root2, sBoxPointsAySigns[i]) * muteY;
                    var boxNormalZ                    = math.chgsign(root2, sBoxPointsAzSigns[i]) * muteZ;
                    var mutedCapsuleEdgeX             = edgeA.x * muteX;
                    var mutedCapsuleEdgeY             = edgeA.y * muteY;
                    var mutedCapsuleEdgeZ             = edgeA.z * muteZ;
                    var notParallel                   = mutedCapsuleEdgeX != 0f || mutedCapsuleEdgeY != 0f || mutedCapsuleEdgeZ != 0f;
                    var mutedNormalizedCapsuleEdgeMag =
                        math.rsqrt(mutedCapsuleEdgeX * mutedCapsuleEdgeX + mutedCapsuleEdgeY * mutedCapsuleEdgeY + mutedCapsuleEdgeZ * mutedCapsuleEdgeZ);
                    var mutedNormalizedCapsuleEdgeX = mutedNormalizedCapsuleEdgeMag * mutedCapsuleEdgeX;
                    var mutedNormalizedCapsuleEdgeY = mutedNormalizedCapsuleEdgeMag * mutedCapsuleEdgeY;
                    var mutedNormalizedCapsuleEdgeZ = mutedNormalizedCapsuleEdgeMag * mutedCapsuleEdgeZ;
                    var alignment                   = mutedNormalizedCapsuleEdgeX * boxNormalX + mutedNormalizedCapsuleEdgeY * boxNormalY + mutedNormalizedCapsuleEdgeZ *
                                                      boxNormalZ;
                    var valid      = notParallel && math.abs(alignment) <= root2;
                    var distanceSq = math.square(closestBx - closestAx) + math.square(closestBy - closestAy) + math.square(closestBz - closestAz);
                    var inside     = math.abs(closestAx) <= box.halfSize.x && math.abs(closestAy) <= box.halfSize.y && math.abs(closestAz) <= box.halfSize.z;

                    // Finalize result
                    signedDistanceSqs[i] = math.select(float.MaxValue, math.select(distanceSq, -distanceSq, inside), valid);
                    closestAxs[i]        = closestAx;
                    closestAys[i]        = closestAy;
                    closestAzs[i]        = closestAz;
                    closestBxs[i]        = closestBx;
                    closestBys[i]        = closestBy;
                    closestBzs[i]        = closestBz;
                }

                // Todo: Might be more optimal to explicitly vectorize this reduction so that we don't have the weird integer shenanigans?
                ulong bestValue = ulong.MaxValue;
                for (int i = 0; i < 12; i++)
                {
                    // Integers are required for reduction, and we always favor values close to 0. But we prefer negative, so we flip the sign
                    // so that positive values convert into larger integers.
                    ulong val = math.asuint(-signedDistanceSqs[i]);
                    // Pack the index into the reduction variable, so that this can be autovectorized properly.
                    val <<= 16;
                    val  |= (uint)i;
                    if (val < bestValue)
                    {
                        bestValue = val;
                    }
                }

                var bestIndex         = (int)(bestValue & 0xf);
                edgesSignedDistanceSq = signedDistanceSqs[bestIndex];
                edgesPointOnSegment   = new float3(closestAxs[bestIndex], closestAys[bestIndex], closestAzs[bestIndex]);
                edgesPointOnBox       = new float3(closestBxs[bestIndex], closestBys[bestIndex], closestBzs[bestIndex]);
            }

            // Step 3: Pick the better between points and edges
            bool pointsBeatEdges      = (pointsSignedDistanceSq <= edgesSignedDistanceSq) ^ ((pointsSignedDistanceSq < 0f) | (edgesSignedDistanceSq < 0f));
            pointsBeatEdges          |= edgesSignedDistanceSq == float.MaxValue;
            var bestSignedDistanceSq  = math.select(edgesSignedDistanceSq, pointsSignedDistanceSq, pointsBeatEdges);
            var bestPointOnSegment    = math.select(edgesPointOnSegment, pointsPointOnSegment, pointsBeatEdges);
            var bestPointOnBox        = math.select(edgesPointOnBox, pointsPointOnBox, pointsBeatEdges);

            // Step 4: Create result
            float3 boxNormal         = math.normalize(math.select(0f, 1f, bestPointOnBox == box.halfSize) + math.select(0f, -1f, bestPointOnBox == -box.halfSize));
            float3 capsuleNormal     = math.normalizesafe(bestPointOnBox - bestPointOnSegment);
            bool   capsuleDegenerate = capsuleNormal.Equals(float3.zero);
            capsuleNormal            = math.select(capsuleNormal, -capsuleNormal, bestSignedDistanceSq < 0f);
            result                   = new ColliderDistanceResultInternal
            {
                hitpointA = bestPointOnBox + box.center,
                hitpointB = bestPointOnSegment + box.center + capsuleNormal * capsule.radius,
                normalA   = boxNormal,
                normalB   = capsuleNormal,
                distance  = math.sign(bestSignedDistanceSq) * math.sqrt(math.abs(bestSignedDistanceSq)) - capsule.radius,
                //featureCodeA = PointRayBox.FeatureCodeFromBoxNormal(boxNormal),
                //featureCodeB = PointRayCapsule.FeatureCodeFromSegmentHitpoint(bestPointOnSegment, osPointA, osPointB)
            };

            if (Hint.Likely(!capsuleDegenerate))
                return result.distance <= maxDistance;

            var capsuleEdge = osPointB - osPointA;
            if (capsuleEdge.Equals(float3.zero))
            {
                result.hitpointB -= boxNormal * capsule.radius;
                result.normalB    = -boxNormal;
                return result.distance <= maxDistance;
            }

            var edgeNormalized = math.normalize(capsuleEdge);
            //edgeNormalized = math.select(edgeNormalized, -edgeNormalized, result.featureCodeB == 1);
            //if (result.featureCodeB < 2 && math.dot(result.normalA, edgeNormalized) >= 0f)
            {
                result.hitpointB -= boxNormal * capsule.radius;
                result.normalB    = -boxNormal;
                return result.distance <= maxDistance;
            }

            result.normalB    = math.normalize(math.cross(math.cross(capsuleEdge, -boxNormal), capsuleEdge));
            result.hitpointB += result.normalB * capsule.radius;
            return result.distance <= maxDistance;
        }

        [Test]
        public void TerrainRaycast_OnVariedLandscape_NormalsMustPointUpward()
        {
            int quadsPerRow = 128;
            int vertsPerRow = quadsPerRow + 1;
            int quadCount   = quadsPerRow * quadsPerRow;

            var heights = new NativeArray<short>(vertsPerRow * vertsPerRow, Allocator.Temp);

            // Generating a varied landscape
            for (int y = 0; y < vertsPerRow; y++)
            {
                for (int x = 0; x < vertsPerRow; x++)
                {
                    short heightVal = 0;

                    if (x > 60 && x < 100 && y > 60 && y < 100)
                    {
                        heightVal = 328;
                    }
                    else if (x >= 55 && x <= 60 && y > 60 && y < 100)
                    {
                        float t   = (x - 55) / 5f;
                        heightVal = (short)math.lerp(0, 328, t);
                    }
                    else
                    {
                        float wave = math.sin(x * 0.1f) * math.cos(y * 0.1f);
                        heightVal  = (short)(wave * 150 + 150);
                    }

                    heights[y * vertsPerRow + x] = heightVal;
                }
            }

            var parities = GenerateParitiesFromHeights(heights, quadsPerRow);

            var validities = new NativeArray<BitField64>((quadCount + 31) / 32, Allocator.Temp);
            for (int i = 0; i < validities.Length; i++)
                validities[i] = new BitField64(~0ul);

            var builder = new BlobBuilder(Allocator.Temp);
            var blob    = TerrainColliderBlob.BuildBlob(
                ref builder,
                quadsPerRow,
                heights,
                parities,
                validities,
                "StressTestTerrain",
                Allocator.Temp);

            var      terrainCollider = new TerrainCollider(blob, new float3(1f, 600f / 32767f, 1f), 0);
            Collider targetCollider  = terrainCollider;

            TransformQvvs terrainTransform = TransformQvvs.identity;

            int raycastHits          = 0;
            int invertedNormalsFound = 0;

            for (int z = 5; z < quadsPerRow - 5; z += 2)
            {
                for (int x = 5; x < quadsPerRow - 5; x += 2)
                {
                    float3 rayStart = new float3(x + 0.5f, 20f, z + 0.5f);
                    float3 rayEnd   = new float3(x + 0.5f, -20f, z + 0.5f);

                    if (Physics.Raycast(rayStart, rayEnd, in targetCollider, in terrainTransform, out var rayHit))
                    {
                        raycastHits++;

                        if (rayHit.normal.y < 0f)
                        {
                            invertedNormalsFound++;
                        }
                    }
                }
            }

            UnityEngine.Debug.Log($"[Terrain Winding Test] Fired {raycastHits} successful rays. Found {invertedNormalsFound} inverted normals.");

            Assert.IsTrue(raycastHits > 0, "All raycasts missed. Terrain collision is completely broken.");

            Assert.AreEqual(0, invertedNormalsFound,
                            $"BUG REPRODUCED: Out of {raycastHits} terrain hits, {invertedNormalsFound} returned an upside-down geometric normal (Y < 0). " +
                            "Check the triangle winding order inside TerrainColliderBlob.DoFinal().");

            blob.Dispose();
            heights.Dispose();
            parities.Dispose();
            validities.Dispose();
        }

        private NativeArray<BitField32> GenerateParitiesFromHeights(NativeArray<short> heights, int quadsPerRow)
        {
            int vertsPerRow = quadsPerRow + 1;
            var parities    = new NativeArray<BitField32>((quadsPerRow * (quadsPerRow + 1) + 31) / 32, Allocator.Temp);

            for (var y = 0; y < quadsPerRow; y++)
            {
                for (var x = 0; x < quadsPerRow; x++)
                {
                    short hTL = heights[y * vertsPerRow + x];
                    short hTR = heights[y * vertsPerRow + (x + 1)];
                    short hBL = heights[(y + 1) * vertsPerRow + x];
                    short hBR = heights[(y + 1) * vertsPerRow + (x + 1)];

                    if (math.abs(hBL - hTR) < math.abs(hTL - hBR))
                    {
                        int        quadIndex      = x + y * quadsPerRow;
                        BitField32 current        = parities[quadIndex / 32];
                        current.Value            |= (1u << (quadIndex % 32));
                        parities[quadIndex / 32]  = current;
                    }
                }
            }
            return parities;
        }
    }
}

