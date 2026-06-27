using Latios.Psyshock;
using Latios.Transforms;
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

