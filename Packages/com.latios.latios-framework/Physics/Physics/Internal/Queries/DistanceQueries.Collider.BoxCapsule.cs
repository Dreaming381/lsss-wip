using System;
using Unity.Mathematics;

namespace Latios.PhysicsEngine
{
    internal static partial class DistanceQueries
    {
        public static bool DistanceBetween(BoxCollider box, CapsuleCollider capsule, float maxDistance, out ColliderDistanceResultInternal result)
        {
            float3 osPointA = capsule.pointA - box.center;  //os = origin space
            float3 osPointB = capsule.pointB - box.center;
            float3 pointsPointOnBox;
            float3 pointsPointOnSegment;
            float  axisDistance;
            //Step 1: Points vs Planes
            {
                float3 distancesToMin = math.max(osPointA, osPointB) + box.halfSize;
                float3 distancesToMax = box.halfSize - math.min(osPointA, osPointB);
                float3 minDistances   = math.min(distancesToMin, distancesToMax);
                float  bestDistance   = math.cmin(minDistances);
                bool3  bestAxisMask   = bestDistance == minDistances;
                //Prioritize y first, then z, then x if multiple distances perfectly match.
                //Todo: Should this be configurabe?
                bestAxisMask.xz      &= !bestAxisMask.y;
                bestAxisMask.x       &= !bestAxisMask.z;
                float3 zeroMask       = math.select(0f, 1f, bestAxisMask);
                bool   useMin         = (minDistances * zeroMask).Equals(distancesToMin * zeroMask);
                float  aOnAxis        = math.dot(osPointA, zeroMask);
                float  bOnAxis        = math.dot(osPointB, zeroMask);
                bool   aIsGreater     = aOnAxis > bOnAxis;
                pointsPointOnSegment  = math.select(osPointA, osPointB, useMin ^ aIsGreater);
                pointsPointOnBox      = math.select(pointsPointOnSegment, math.select(box.halfSize, -box.halfSize, useMin), bestAxisMask);
                pointsPointOnBox      = math.clamp(pointsPointOnBox, -box.halfSize, box.halfSize);
                axisDistance          = -bestDistance;
            }
            float signedDistanceSq = math.distancesq(pointsPointOnSegment, pointsPointOnBox);
            signedDistanceSq       = math.select(signedDistanceSq, -signedDistanceSq, axisDistance <= 0f);
            //Step 2: Edge vs Edges
            //Todo: We could inline the SegmentSegment invocations to simplify the initial dot products.
            float3     capsuleEdge     = osPointB - osPointA;
            simdFloat3 simdCapsuleEdge = new simdFloat3(capsuleEdge);
            simdFloat3 simdOsPointA    = new simdFloat3(osPointA);
            //x-axes
            simdFloat3 boxPointAx = new simdFloat3(new float3(-box.halfSize.x, box.halfSize.y, box.halfSize.z),
                                                   new float3(-box.halfSize.x, box.halfSize.y, -box.halfSize.z),
                                                   new float3(-box.halfSize.x, -box.halfSize.y, box.halfSize.z),
                                                   new float3(-box.halfSize.x, -box.halfSize.y, -box.halfSize.z));
            simdFloat3 simdBoxEdgeX = new simdFloat3(new float3(2f * box.halfSize.x, 0f, 0f));
            QueriesLowLevelUtils.SegmentSegment(simdOsPointA, simdCapsuleEdge, boxPointAx, simdBoxEdgeX, out simdFloat3 closestAx, out simdFloat3 closestBx);
            simdFloat3 boxNormalsX = new simdFloat3(new float3(0f, math.SQRT2 / 2f, math.SQRT2 / 2f),
                                                    new float3(0f, math.SQRT2 / 2f, -math.SQRT2 / 2f),
                                                    new float3(0f, -math.SQRT2 / 2f, math.SQRT2 / 2f),
                                                    new float3(0f, -math.SQRT2 / 2f, -math.SQRT2 / 2f));
            //Imagine a line that goes perpendicular through a box's edge at the midpoint.
            //All orientations of that line which do not penetrate the box (tangency is not considered penetrating in this case) are validly resolved collisions.
            //Orientations of the line which do penetrate are not valid.
            //If we constrain the capsule edge to be perpendicular, normalize it, and then compute the dot product, we can compare that to the necessary 45 degree angle
            //where penetration occurs. Parallel lines are excluded because either we want to record a capsule point (step 1) or a perpendicular edge on the box.
            bool   notParallelX      = !capsuleEdge.yz.Equals(float2.zero);
            float4 alignmentsX       = simd.dot(math.normalize(new float3(0f, capsuleEdge.yz)), boxNormalsX);
            bool4  xValid            = (math.abs(alignmentsX) <= math.SQRT2 / 2f) & notParallelX;
            float4 signedDistanceSqX = simd.distancesq(closestAx, closestBx);
            bool4  insideX           = (math.abs(closestAx.x) <= box.halfSize.x) & (math.abs(closestAx.y) <= box.halfSize.y) & (math.abs(closestAx.z) <= box.halfSize.z);
            signedDistanceSqX        = math.select(signedDistanceSqX, -signedDistanceSqX, insideX);
            signedDistanceSqX        = math.select(float.MaxValue, signedDistanceSqX, xValid);
            //y-axis
            simdFloat3 boxPointAy = new simdFloat3(new float3(box.halfSize.x, -box.halfSize.y, box.halfSize.z),
                                                   new float3(box.halfSize.x, -box.halfSize.y, -box.halfSize.z),
                                                   new float3(-box.halfSize.x, -box.halfSize.y, box.halfSize.z),
                                                   new float3(-box.halfSize.x, -box.halfSize.y, -box.halfSize.z));
            simdFloat3 simdBoxEdgeY = new simdFloat3(new float3(0f, 2f * box.halfSize.y, 0f));
            QueriesLowLevelUtils.SegmentSegment(simdOsPointA, simdCapsuleEdge, boxPointAy, simdBoxEdgeY, out simdFloat3 closestAy, out simdFloat3 closestBy);
            simdFloat3 boxNormalsY = new simdFloat3(new float3(math.SQRT2 / 2f, 0f, math.SQRT2 / 2f),
                                                    new float3(math.SQRT2 / 2f, 0f, -math.SQRT2 / 2f),
                                                    new float3(-math.SQRT2 / 2f, 0f, math.SQRT2 / 2f),
                                                    new float3(-math.SQRT2 / 2f, 0f, -math.SQRT2 / 2f));
            bool   notParallelY      = !capsuleEdge.xz.Equals(float2.zero);
            float4 alignmentsY       = simd.dot(math.normalize(new float3(capsuleEdge.x, 0f, capsuleEdge.z)), boxNormalsY);
            bool4  yValid            = (math.abs(alignmentsY) <= math.SQRT2 / 2f) & notParallelY;
            float4 signedDistanceSqY = simd.distancesq(closestAy, closestBy);
            bool4  insideY           = (math.abs(closestAy.x) <= box.halfSize.x) & (math.abs(closestAy.y) <= box.halfSize.y) & (math.abs(closestAy.z) <= box.halfSize.z);
            signedDistanceSqY        = math.select(signedDistanceSqY, -signedDistanceSqY, insideY);
            signedDistanceSqY        = math.select(float.MaxValue, signedDistanceSqY, yValid);
            //z-axis
            simdFloat3 boxPointAz = new simdFloat3(new float3(box.halfSize.x, box.halfSize.y, -box.halfSize.z),
                                                   new float3(box.halfSize.x, -box.halfSize.y, -box.halfSize.z),
                                                   new float3(-box.halfSize.x, box.halfSize.y, -box.halfSize.z),
                                                   new float3(-box.halfSize.x, -box.halfSize.y, -box.halfSize.z));
            simdFloat3 simdBoxEdgeZ = new simdFloat3(new float3(0f, 0f, 2f * box.halfSize.z));
            QueriesLowLevelUtils.SegmentSegment(simdOsPointA, simdCapsuleEdge, boxPointAz, simdBoxEdgeZ, out simdFloat3 closestAz, out simdFloat3 closestBz);
            simdFloat3 boxNormalsZ = new simdFloat3(new float3(math.SQRT2 / 2f, math.SQRT2 / 2f, 0f),
                                                    new float3(math.SQRT2 / 2f, -math.SQRT2 / 2f, 0f),
                                                    new float3(-math.SQRT2 / 2f, math.SQRT2 / 2f, 0f),
                                                    new float3(-math.SQRT2 / 2f, -math.SQRT2 / 2f, 0f));
            bool   notParallelZ      = !capsuleEdge.xy.Equals(float2.zero);
            float4 alignmentsZ       = simd.dot(math.normalize(new float3(capsuleEdge.xy, 0f)), boxNormalsZ);
            bool4  zValid            = (math.abs(alignmentsZ) <= math.SQRT2 / 2f) & notParallelZ;
            float4 signedDistanceSqZ = simd.distancesq(closestAz, closestBz);
            bool4  insideZ           = (math.abs(closestAz.x) <= box.halfSize.x) & (math.abs(closestAz.y) <= box.halfSize.y) & (math.abs(closestAz.z) <= box.halfSize.z);
            signedDistanceSqZ        = math.select(signedDistanceSqZ, -signedDistanceSqZ, insideZ);
            signedDistanceSqZ        = math.select(float.MaxValue, signedDistanceSqZ, zValid);

            //Step 3: Find best result
            float4     bestAxisSignedDistanceSq = signedDistanceSqX;
            simdFloat3 bestAxisPointOnSegment   = closestAx;
            simdFloat3 bestAxisPointOnBox       = closestBx;
            bool4      yWins                    = (signedDistanceSqY < bestAxisSignedDistanceSq) ^ ((bestAxisSignedDistanceSq < 0f) & (signedDistanceSqY < 0f));
            bestAxisSignedDistanceSq            = math.select(bestAxisSignedDistanceSq, signedDistanceSqY, yWins);
            bestAxisPointOnSegment              = simd.select(bestAxisPointOnSegment, closestAy, yWins);
            bestAxisPointOnBox                  = simd.select(bestAxisPointOnBox, closestBy, yWins);
            bool4 zWins                         = (signedDistanceSqZ < bestAxisSignedDistanceSq) ^ ((bestAxisSignedDistanceSq < 0f) & (signedDistanceSqZ < 0f));
            bestAxisSignedDistanceSq            = math.select(bestAxisSignedDistanceSq, signedDistanceSqZ, zWins);
            bestAxisPointOnSegment              = simd.select(bestAxisPointOnSegment, closestAz, zWins);
            bestAxisPointOnBox                  = simd.select(bestAxisPointOnBox, closestBz, zWins);
            bool   bBeatsA                      = (bestAxisSignedDistanceSq.y < bestAxisSignedDistanceSq.x) ^ (math.all(bestAxisSignedDistanceSq.xy < 0f));
            bool   dBeatsC                      = (bestAxisSignedDistanceSq.w < bestAxisSignedDistanceSq.z) ^ (math.all(bestAxisSignedDistanceSq.zw < 0f));
            float  bestAbSignedDistanceSq       = math.select(bestAxisSignedDistanceSq.x, bestAxisSignedDistanceSq.y, bBeatsA);
            float  bestCdSignedDistanceSq       = math.select(bestAxisSignedDistanceSq.z, bestAxisSignedDistanceSq.w, dBeatsC);
            float3 bestAbPointOnSegment         = math.select(bestAxisPointOnSegment.a, bestAxisPointOnSegment.b, bBeatsA);
            float3 bestCdPointOnSegment         = math.select(bestAxisPointOnSegment.c, bestAxisPointOnSegment.d, dBeatsC);
            float3 bestAbPointOnBox             = math.select(bestAxisPointOnBox.a, bestAxisPointOnBox.b, bBeatsA);
            float3 bestCdPointOnBox             = math.select(bestAxisPointOnBox.c, bestAxisPointOnBox.d, dBeatsC);
            bool   cdBeatsAb                    = (bestCdSignedDistanceSq < bestAbSignedDistanceSq) ^ ((bestCdSignedDistanceSq < 0f) & (bestAbSignedDistanceSq < 0f));
            float  bestSignedDistanceSq         = math.select(bestAbSignedDistanceSq, bestCdSignedDistanceSq, cdBeatsAb);
            float3 bestPointOnSegment           = math.select(bestAbPointOnSegment, bestCdPointOnSegment, cdBeatsAb);
            float3 bestPointOnBox               = math.select(bestAbPointOnBox, bestCdPointOnBox, cdBeatsAb);
            bool   pointsBeatEdges              = (signedDistanceSq <= bestSignedDistanceSq) ^ ((signedDistanceSq < 0f) & (bestSignedDistanceSq < 0f));
            bestSignedDistanceSq                = math.select(bestSignedDistanceSq, signedDistanceSq, pointsBeatEdges);
            bestPointOnSegment                  = math.select(bestPointOnSegment, pointsPointOnSegment, pointsBeatEdges);
            bestPointOnBox                      = math.select(bestPointOnBox, pointsPointOnBox, pointsBeatEdges);

            //Step 4: Build result
            float3 boxNormal     = math.normalize(math.select(0f, 1f, bestPointOnBox == box.halfSize) + math.select(0f, -1f, bestPointOnBox == -box.halfSize));
            float3 capsuleNormal = math.normalizesafe(bestPointOnBox - bestPointOnSegment, -boxNormal);
            capsuleNormal        = math.select(capsuleNormal, -capsuleNormal, bestSignedDistanceSq < 0f);
            result               = new ColliderDistanceResultInternal
            {
                hitpointA = bestPointOnBox + box.center,
                hitpointB = bestPointOnSegment + box.center + capsuleNormal * capsule.radius,
                normalA   = boxNormal,
                normalB   = capsuleNormal,
                distance  = math.sign(bestSignedDistanceSq) * math.sqrt(math.abs(bestSignedDistanceSq)) - capsule.radius
            };

            return result.distance <= maxDistance;
        }
    }
}

