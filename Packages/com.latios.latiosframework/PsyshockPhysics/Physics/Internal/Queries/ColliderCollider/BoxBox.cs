//#define LATIOS_PSYSHOCK_REFERENCE

using System;
using Latios.Calci;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;

namespace Latios.Psyshock
{
    internal static class BoxBox
    {
        public static bool AreOverlapping(in BoxCollider boxA,
                                          in RigidTransform aTransform,
                                          in BoxCollider boxB,
                                          in RigidTransform bTransform)
        {
            var aOffsetTransform  = aTransform;
            aOffsetTransform.pos += math.rotate(aTransform.rot, boxA.center);
            var bOffsetTransform  = bTransform;
            bOffsetTransform.pos += math.rotate(bTransform.rot, boxB.center);
            var bInATransform     = math.InverseTransformFast(in aOffsetTransform, in bOffsetTransform);
            return BoxBoxOverlapping(boxA.halfSize, boxB.halfSize, in bInATransform);
        }

        public static bool WithinDistance(in BoxCollider boxA,
                                          in RigidTransform aTransform,
                                          in BoxCollider boxB,
                                          in RigidTransform bTransform,
                                          float maxDistance)
        {
            var aOffsetTransform  = aTransform;
            aOffsetTransform.pos += math.rotate(aTransform.rot, boxA.center);
            var bOffsetTransform  = bTransform;
            bOffsetTransform.pos += math.rotate(bTransform.rot, boxB.center);
            var bInATransform     = math.InverseTransformFast(in aOffsetTransform, in bOffsetTransform);
            var aInBTransform     = math.InverseTransformFast(in bOffsetTransform, in aOffsetTransform);
            return BoxBoxWithin(boxA.halfSize, boxB.halfSize, in bInATransform, in aInBTransform, maxDistance);
        }

        public static bool DistanceBetween(in BoxCollider boxA,
                                           in RigidTransform aTransform,
                                           in BoxCollider boxB,
                                           in RigidTransform bTransform,
                                           float maxDistance,
                                           out ColliderDistanceResult result)
        {
            var aOffsetTransform  = aTransform;
            aOffsetTransform.pos += math.rotate(aTransform.rot, boxA.center);
            var bOffsetTransform  = bTransform;
            bOffsetTransform.pos += math.rotate(bTransform.rot, boxB.center);
            var bInATransform     = math.InverseTransformFast(in aOffsetTransform, in bOffsetTransform);
            var aInBTransform     = math.InverseTransformFast(in bOffsetTransform, in aOffsetTransform);

            var hit = BoxBoxDistance(boxA.halfSize, boxB.halfSize, in bInATransform, in aInBTransform, maxDistance, out var localResult);
            //var altHit = BoxBoxDistanceReference(boxA.halfSize, boxB.halfSize, in bInATransform, in aInBTransform, maxDistance, out var localResult2);
            //if ((altHit && !hit) || (hit && math.distance(localResult.hitpointA, localResult.hitpointB) > math.abs(localResult.distance) + 0.001f))
            //{
            //    UnityEngine.Debug.Log(
            //        $"Mismatched to reference.\nhit: {hit}, distance: {localResult.distance}, hitA: {localResult.hitpointA}, hitB: {localResult.hitpointB}, featureA: {localResult.featureCodeA}, featureB: {localResult.featureCodeB}\nhit: {altHit}, distance: {localResult2.distance}, hitA: {localResult2.hitpointA}, hitB: {localResult2.hitpointB}, featureA: {localResult2.featureCodeA}, featureB: {localResult2.featureCodeB}");
            //    var hit3 = BoxBoxDistance(boxA.halfSize,
            //                               boxB.halfSize,
            //                               in bInATransform,
            //                               in aInBTransform,
            //                               maxDistance,
            //                               out var localResult3);
            //    PhysicsDebug.DrawCollider(new BoxCollider { center = 0f, halfSize = boxA.halfSize }, RigidTransform.identity, UnityEngine.Color.blue);
            //    PhysicsDebug.DrawCollider(new BoxCollider { center                = 0f, halfSize = boxB.halfSize }, bInATransform,           UnityEngine.Color.red);
            //}
            localResult.hitpointA += boxA.center;
            localResult.hitpointB += boxA.center;
            result                 = InternalQueryTypeUtilities.BinAResultToWorld(in localResult, in aTransform);
            return hit;
        }

        public static bool ColliderCast(in BoxCollider boxToCast, in RigidTransform castStart, float3 castEnd, in BoxCollider targetBox, in RigidTransform targetBoxTransform,
                                        out ColliderCastResult result)
        {
            var  castStartInverse             = math.inverse(castStart);
            var  targetInCasterSpaceTransform = math.mul(castStartInverse, targetBoxTransform);
            var  castDirection                = math.rotate(castStartInverse, castEnd - castStart.pos);
            var  normalizedCastDirection      = math.normalize(castDirection);
            bool hit                          = Mpr.MprCastNoRoundness(boxToCast,
                                                                       targetBox,
                                                                       in targetInCasterSpaceTransform,
                                                                       normalizedCastDirection,
                                                                       math.length(castDirection),
                                                                       out float distanceOfImpact,
                                                                       out bool somethingWentWrong);
            InternalQueryTypeUtilities.CheckMprResolved(somethingWentWrong);
            if (!hit || distanceOfImpact <= 0f)
            {
                result = default;
                return false;
            }

            var castHitOffset       = math.rotate(castStart, normalizedCastDirection * distanceOfImpact);
            var casterHitTransform  = castStart;
            casterHitTransform.pos += castHitOffset;
            DistanceBetween(in boxToCast, in casterHitTransform, in targetBox, in targetBoxTransform, float.MaxValue, out var distanceResult);

            result = new ColliderCastResult
            {
                distance                 = distanceOfImpact,
                hitpoint                 = distanceResult.hitpointA,
                normalOnCaster           = distanceResult.normalA,
                normalOnTarget           = distanceResult.normalB,
                subColliderIndexOnCaster = 0,
                subColliderIndexOnTarget = 0
            };

            return true;
        }

        public static UnitySim.ContactsBetweenResult UnityContactsBetween(in BoxCollider boxA,
                                                                          in RigidTransform aTransform,
                                                                          in BoxCollider boxB,
                                                                          in RigidTransform bTransform,
                                                                          in ColliderDistanceResult distanceResult)
        {
            UnitySim.ContactsBetweenResult result        = default;
            var                            bInATransform = math.mul(math.inverse(aTransform), bTransform);

            // Unity Physics prefers to use the SAT axes for the contact normal if it can.
            // We attempt to recover the best SAT axis here.
            var featureTypes  = (distanceResult.featureCodeA >> 12) & 0x0c;
            featureTypes     |= distanceResult.featureCodeB >> 14;
            float3 aLocalContactNormal;
            bool   usesContactDir = false;
            switch (featureTypes)
            {
                case 0:  // A point and B point
                case 1:  // A point and B edge
                case 4:  // A edge and B point
                {
                    // In the case of a point and an edge, I believe only one of three things will happen:
                    // 1) The SAT axis is between two edges, and the planes won't project onto each other at all.
                    // 2) The SAT axis will be a face axis, in which the closest point will be clipped off.
                    // 3) The SAT axis is identical to the direction between the closest points.
                    //
                    // The first two options in Unity Physics will trigger the FaceFace() method to fail.
                    // The failure will result in Unity completely nuking its in-progress manifold and using
                    // the general-purpose algorithm that relies on GJK, which uses a contact normal based on
                    // the axis between the closest points. The last option is effectively the same result.
                    aLocalContactNormal =
                        math.normalizesafe((distanceResult.hitpointB - distanceResult.hitpointA) * math.select(1f, -1f, distanceResult.distance < 0f), float3.zero);
                    // Yes, normalizesafe has produced "valid" normals on floating point errors before. The result can get pretty noisy, so the tolerance is high.
                    aLocalContactNormal = math.select(aLocalContactNormal, float3.zero, math.abs(distanceResult.distance) < 1e-3f);
                    if (aLocalContactNormal.Equals(float3.zero))
                    {
                        aLocalContactNormal = math.normalize(distanceResult.normalA - distanceResult.normalB);
                    }
                    aLocalContactNormal = math.InverseRotateFast(aTransform.rot, aLocalContactNormal);
                    usesContactDir      = true;
                    break;
                }
                case 5:  // A edge and B edge
                {
                    var edgeDirectionIndexA = (distanceResult.featureCodeA >> 2) & 0xff;
                    var edgeDirectionA      = edgeDirectionIndexA switch
                    {
                        0 => new float3(1f, 0f, 0f),
                        1 => new float3(0f, 1f, 0f),
                        2 => new float3(0f, 0f, 1f),
                        _ => default
                    };
                    var edgeDirectionIndexB = (distanceResult.featureCodeB >> 2) & 0xff;
                    var edgeDirectionB      = edgeDirectionIndexB switch
                    {
                        0 => new float3(1f, 0f, 0f),
                        1 => new float3(0f, 1f, 0f),
                        2 => new float3(0f, 0f, 1f),
                        _ => default
                    };
                    edgeDirectionB      = math.rotate(bInATransform.rot, edgeDirectionB);
                    aLocalContactNormal = math.normalize(math.cross(edgeDirectionA, edgeDirectionB));
                    aLocalContactNormal = math.select(aLocalContactNormal,
                                                      -aLocalContactNormal,
                                                      math.dot(math.rotate(aTransform.rot, aLocalContactNormal), distanceResult.normalA) < 0f);
                    break;
                }
                case 2:  // A point and B face
                case 6:  // A edge and B face
                {
                    // For A edge, this can only happen due to some bizarre precision issues.
                    // But we'll handle it anyways by just using the face normal of B.
                    var faceIndex       = distanceResult.featureCodeB & 0xff;
                    aLocalContactNormal = faceIndex switch
                    {
                        0 => new float3(1f, 0f, 0f),
                        1 => new float3(0f, 1f, 0f),
                        2 => new float3(0f, 0f, 1f),
                        3 => new float3(-1f, 0f, 0f),
                        4 => new float3(0f, -1f, 0f),
                        5 => new float3(0f, 0f, -1f),
                        _ => default
                    };
                    aLocalContactNormal = -math.rotate(bInATransform.rot, aLocalContactNormal);
                    break;
                }
                case 8:  // A face and B point
                case 9:  // A face and B edge
                case 10:  // A face and B face
                {
                    // For B edge and face, this can only happen due to some bizarre precision issues.
                    // But we'll handle it anyways by just using the face normal of A.
                    var faceIndex       = distanceResult.featureCodeA & 0xff;
                    aLocalContactNormal = faceIndex switch
                    {
                        0 => new float3(1f, 0f, 0f),
                        1 => new float3(0f, 1f, 0f),
                        2 => new float3(0f, 0f, 1f),
                        3 => new float3(-1f, 0f, 0f),
                        4 => new float3(0f, -1f, 0f),
                        5 => new float3(0f, 0f, -1f),
                        _ => default
                    };
                    break;
                }
                default:
                    aLocalContactNormal = default;
                    break;
            }

            for (int iteration = math.select(0, 1, usesContactDir); iteration < 2; iteration++)
            {
                result.contactNormal = math.rotate(aTransform, -aLocalContactNormal);

                var bLocalContactNormal = math.InverseRotateFast(bInATransform.rot, -aLocalContactNormal);
                PointRayBox.BestFacePlanesAndVertices(in boxA, aLocalContactNormal, out var aEdgePlaneNormals, out var aEdgePlaneDistances, out var aPlane, out var aVertices);
                PointRayBox.BestFacePlanesAndVertices(in boxB, bLocalContactNormal, out var bEdgePlaneNormals, out _,                       out var bPlane, out var bVertices);
                bPlane                                 = mathex.TransformPlane(bInATransform, bPlane);
                bVertices                              = simd.transform(bInATransform, bVertices);
                bEdgePlaneNormals                      = simd.rotate(bInATransform.rot, bEdgePlaneNormals);
                var  bEdgePlaneDistances               = simd.dot(bEdgePlaneNormals, bVertices.bcda);
                bool needsClosestPoint                 = true;
                var  distanceScalarAlongContactNormalB = math.rcp(math.dot(-aLocalContactNormal, bPlane.normal));

                // Project and clip edges of A onto the face of B.
                for (int edgeIndex = 0; edgeIndex < 4; edgeIndex++)
                {
                    var rayStart          = aVertices.a;
                    var rayDisplacement   = aVertices.b - rayStart;
                    var rayRelativeStarts = simd.dot(rayStart, bEdgePlaneNormals) - bEdgePlaneDistances;
                    var relativeDiffs     = simd.dot(rayDisplacement, bEdgePlaneNormals);
                    var rayRelativeEnds   = rayRelativeStarts + relativeDiffs;
                    var rayFractions      = math.select(-rayRelativeStarts / relativeDiffs, float4.zero, relativeDiffs == float4.zero);
                    var startsInside      = rayRelativeStarts <= 0f;
                    var endsInside        = rayRelativeEnds <= 0f;
                    var projectsOnFace    = startsInside | endsInside;
                    var enterFractions    = math.select(float4.zero, rayFractions, !startsInside & rayFractions > float4.zero);
                    var exitFractions     = math.select(1f, rayFractions, !endsInside & rayFractions < 1f);
                    var fractionA         = math.cmax(enterFractions);
                    var fractionB         = math.cmin(exitFractions);

                    if (math.all(projectsOnFace) && fractionA < fractionB)
                    {
                        // Add the two contacts from the possibly clipped segment
                        var clippedSegmentA = rayStart + fractionA * rayDisplacement;
                        var aDistance       = mathex.SignedDistance(bPlane, clippedSegmentA) * distanceScalarAlongContactNormalB;
                        result.Add(math.transform(aTransform, clippedSegmentA + aLocalContactNormal * aDistance), aDistance);
                        needsClosestPoint &= aDistance > distanceResult.distance + 1e-4f;
                        if (fractionB < 1f)  // Avoid duplication when vertex is not clipped
                        {
                            var clippedSegmentB = rayStart + fractionB * rayDisplacement;
                            var bDistance       = mathex.SignedDistance(bPlane, clippedSegmentB) * distanceScalarAlongContactNormalB;
                            var bContact        = math.transform(aTransform, clippedSegmentB + aLocalContactNormal * bDistance);
                            result.Add(bContact, bDistance);
                            needsClosestPoint &= bDistance > distanceResult.distance + 1e-4f;
                        }
                    }
                    aVertices = aVertices.bcda;
                }

                // Project vertices of B onto the face of A
                var distanceScalarAlongContactNormalA = math.rcp(math.dot(aLocalContactNormal, aPlane.normal));
                for (int i = 0; i < 4; i++)
                {
                    var vertex = bVertices[i];
                    if (math.all(simd.dot(aEdgePlaneNormals, vertex) < aEdgePlaneDistances))
                    {
                        var distance = mathex.SignedDistance(aPlane, vertex) * distanceScalarAlongContactNormalA;
                        var bContact = math.transform(aTransform, vertex);
                        result.Add(bContact, distance);
                        needsClosestPoint &= distance > distanceResult.distance + 1e-4f;
                    }
                }

                if (!needsClosestPoint)
                {
                    return result;
                }
                else if (iteration == 0)
                {
                    // We missed using the SAT axis. Unity falls back to a more generalized algorithm,
                    // but it effectively simplifies to the same loop except using a contact normal based
                    // on the axis of the closest points.
                    aLocalContactNormal =
                        math.normalizesafe((distanceResult.hitpointB - distanceResult.hitpointA) * math.select(1f, -1f, distanceResult.distance < 0f), float3.zero);
                    if (aLocalContactNormal.Equals(float3.zero))
                    {
                        aLocalContactNormal = math.normalize(distanceResult.normalA - distanceResult.normalB);
                    }
                    aLocalContactNormal = math.InverseRotateFast(aTransform.rot, aLocalContactNormal);
                    result              = default;
                }
            }

            result.Add(distanceResult.hitpointB, distanceResult.distance);
            return result;
        }

        static bool BoxBoxOverlapping(float3 halfSizeA, float3 halfSizeB, in RigidTransform bInASpace)
        {
            var bInARotMat   = new float3x3(bInASpace.rot);
            var faceAxesBinA = new simdFloat3(bInARotMat.c0, bInARotMat.c1, bInARotMat.c2, bInARotMat.c0);
            {
                // Inline IsSeparatingAxis4 for A's face axes.
                float4     boxARadiusOnAxes = halfSizeA.xyzx;
                simdFloat3 axesInBSpace     = simd.inverseRotateRightUpForwardRight(bInARotMat);
                float4     boxBRadiusOnAxes = simd.dot(simd.abs(axesInBSpace), halfSizeB);
                // When the distance B's center is from A's along the axis is greater than the combined box extents along the axis, there is separation.
                if (math.any(math.abs(bInASpace.pos.xyzx) > boxARadiusOnAxes + boxBRadiusOnAxes))
                    return false;
            }
            if (math.any(IsSeparatingAxis4(faceAxesBinA, halfSizeA, halfSizeB, in bInARotMat, bInASpace.pos)))
                return false;
            if (math.any(IsSeparatingAxis4(simd.rightCross(faceAxesBinA), halfSizeA, halfSizeB, in bInARotMat, bInASpace.pos)))
                return false;
            if (math.any(IsSeparatingAxis4(simd.upCross(faceAxesBinA), halfSizeA, halfSizeB, in bInARotMat, bInASpace.pos)))
                return false;
            return !math.any(IsSeparatingAxis4(simd.forwardCross(faceAxesBinA), halfSizeA, halfSizeB, in bInARotMat, bInASpace.pos));
        }

        static bool4 IsSeparatingAxis4(simdFloat3 axes, float3 halfSizeA, float3 halfSizeB, in float3x3 bInARotMat, float3 centerB)
        {
            float4     boxARadiusOnAxes = simd.dot(simd.abs(axes), halfSizeA);
            simdFloat3 axesInBSpace     = simd.inverseRotate(bInARotMat, axes);
            float4     boxBRadiusOnAxes = simd.dot(simd.abs(axesInBSpace), halfSizeB);
            // When the distance B's center is from A's along the axis is greater than the combined box extents along the axis, there is separation.
            return math.abs(simd.dot(axes, centerB)) > boxARadiusOnAxes + boxBRadiusOnAxes;
        }

        // This implementation is a simplified mix of BoxBoxOverlapping() and BoxBoxDistance().
        private static bool BoxBoxWithin(float3 halfSizeA,
                                         float3 halfSizeB,
                                         in RigidTransform bInASpace,
                                         in RigidTransform aInBSpace,
                                         float maxDistance)
        {
            var         bInARotMat = new float3x3(bInASpace.rot);
            Span<float> axesX      =
                stackalloc float[] { 1f, 0f, 0f, bInARotMat.c0.x, bInARotMat.c1.x, bInARotMat.c2.x, 1f, 0f, 0f, 0f, bInARotMat.c0.z, bInARotMat.c1.z, bInARotMat.c2.z,
                                     -bInARotMat.c0.y, -bInARotMat.c1.y, -bInARotMat.c2.y };
            Span<float> axesY =
                stackalloc float[] { 0f, 1f, 0f, bInARotMat.c0.y, bInARotMat.c1.y, bInARotMat.c2.y, 0f, -bInARotMat.c0.z, -bInARotMat.c1.z, -bInARotMat.c2.z, 0f, 0f, 0f,
                                     bInARotMat.c0.x, bInARotMat.c1.x, bInARotMat.c2.x };
            Span<float> axesZ =
                stackalloc float[] { 0f, 0f, 1f, bInARotMat.c0.z, bInARotMat.c1.z, bInARotMat.c2.z, 0f, bInARotMat.c0.y, bInARotMat.c1.y, bInARotMat.c2.y, -bInARotMat.c0.x,
                                     -bInARotMat.c1.x, -bInARotMat.c2.x, 0f, 0f, 0f };
            Span<float> separations = stackalloc float[16];
            for (int i = 0; i < 16; i++)
            {
                var axisX         = axesX[i];
                var axisY         = axesY[i];
                var axisZ         = axesZ[i];
                var axisLenSq     = axisX * axisX + axisY * axisY + axisZ * axisZ;
                var valid         = axisLenSq > math.FLT_MIN_NORMAL;
                var axisLenRsqrt  = math.rsqrt(axisLenSq);
                axisX            *= axisLenRsqrt;
                axisY            *= axisLenRsqrt;
                axisZ            *= axisLenRsqrt;

                var aSupportX = math.chgsign(halfSizeA.x, axisX);
                var aSupportY = math.chgsign(halfSizeA.y, axisY);
                var aSupportZ = math.chgsign(halfSizeA.z, axisZ);

                // Scalar implementation of mul(aInBSpace.rot, axis)
                var tx       = 2f * (aInBSpace.rot.value.y * axisZ - aInBSpace.rot.value.z * axisY);
                var ty       = 2f * (aInBSpace.rot.value.z * axisX - aInBSpace.rot.value.x * axisZ);
                var tz       = 2f * (aInBSpace.rot.value.x * axisY - aInBSpace.rot.value.y * axisX);
                var axisInBX = axisX + aInBSpace.rot.value.w * tx + (aInBSpace.rot.value.y * tz - aInBSpace.rot.value.z * ty);
                var axisInBY = axisY + aInBSpace.rot.value.w * ty + (aInBSpace.rot.value.z * tx - aInBSpace.rot.value.x * tz);
                var axisInBZ = axisZ + aInBSpace.rot.value.w * tz + (aInBSpace.rot.value.x * ty - aInBSpace.rot.value.y * tx);

                var bSupportInBX  = math.chgsign(halfSizeB.x, -axisInBX);
                var bSupportInBY  = math.chgsign(halfSizeB.y, -axisInBY);
                var bSupportInBZ  = math.chgsign(halfSizeB.z, -axisInBZ);
                tx                = 2f * (bInASpace.rot.value.y * bSupportInBZ - bInASpace.rot.value.z * bSupportInBY);
                ty                = 2f * (bInASpace.rot.value.z * bSupportInBX - bInASpace.rot.value.x * bSupportInBZ);
                tz                = 2f * (bInASpace.rot.value.x * bSupportInBY - bInASpace.rot.value.y * bSupportInBX);
                var bSupportRelBX = bSupportInBX + bInASpace.rot.value.w * tx + (bInASpace.rot.value.y * tz - bInASpace.rot.value.z * ty);
                var bSupportRelBY = bSupportInBY + bInASpace.rot.value.w * ty + (bInASpace.rot.value.z * tx - bInASpace.rot.value.x * tz);
                var bSupportRelBZ = bSupportInBZ + bInASpace.rot.value.w * tz + (bInASpace.rot.value.x * ty - bInASpace.rot.value.y * tx);
                var bSupportX     = bInASpace.pos.x + bSupportRelBX;
                var bSupportY     = bInASpace.pos.y + bSupportRelBY;
                var bSupportZ     = bInASpace.pos.z + bSupportRelBZ;

                var aSupportDot      = aSupportX * axisX + aSupportY * axisY + aSupportZ * axisZ;
                var bSupportDot      = bSupportX * axisX + bSupportY * axisY + bSupportZ * axisZ;
                var separationBFromA = bSupportDot - aSupportDot;

                var negBSupportX     = bInASpace.pos.x - bSupportRelBX;
                var negBSupportY     = bInASpace.pos.y - bSupportRelBY;
                var negBSupportZ     = bInASpace.pos.z - bSupportRelBZ;
                var negASupportDot   = -aSupportDot;
                var negBSupportDot   = negBSupportX * axisX + negBSupportY * axisY + negBSupportZ * axisZ;
                var separationAFromB = negASupportDot - negBSupportDot;

                separations[i] = math.select(float.MinValue, math.max(separationAFromB, separationBFromA), valid);
            }

            // Find maximum separation
            float bestSeparation;
            {
                Span<float> maxes = stackalloc float[8];
                for (int i = 0; i < 8; i++)
                {
                    maxes[i] = math.max(separations[i], separations[i + 8]);
                }
                for (int i = 0; i < 4; i++)
                {
                    maxes[i] = math.max(maxes[i], maxes[i + 4]);
                }
                bestSeparation = math.cmax(new float4(maxes[0], maxes[1], maxes[2], maxes[3]));
            }
            // If the max separation is beyond our max distance, exit early.
            if (bestSeparation > maxDistance)
                return false;

            // If there was no separating axis, then we already have the overlap distance. And because we know
            // we are within the maxDistance, then we can simply return true. We check maxDistance as it might
            // provide Burst the opportunity to perform compile-time constant code elimination.
            if (maxDistance <= 0f)
                return true;

            // The boxes are not penetrating, but we don't know what the distance is.
            // If edges have best separating axes, test those.
            // Otherwise, it can be guaranteed we do not have an edge-edge pair.

            // Find all edge axes that match our best separation, and make a mask out of them.
            // We fudge this a little so that we avoid cases where floating-point error steers us down the wrong path.
            uint  bestAxesMaskRaw = 0;
            float epsilon         = math.EPSILON * math.max(math.abs(bestSeparation), 128f);
            for (int i = 7; i < 16; i++)
            {
                uint toAdd       = 1u << i;
                bestAxesMaskRaw += math.select(0, toAdd, math.distance(separations[i], bestSeparation) <= epsilon);
            }
            var   bestAxesMask       = new BitField32(bestAxesMaskRaw);
            bool  foundClosestEdge   = false;
            float bestEdgeDistanceSq = float.MinValue;

            //if (edgeMaxDistance + 1e-4f >= alignedMaxDistance)
            while (bestAxesMask.Value != 0)
            {
                var bestEdgeIndex = bestAxesMask.CountTrailingZeros();
                var axis          = math.normalize(new float3(axesX[bestEdgeIndex], axesY[bestEdgeIndex], axesZ[bestEdgeIndex]));
                {
                    var supportA            = math.chgsign(halfSizeA, axis);
                    var maxA                = math.dot(supportA, axis);
                    var minA                = -maxA;
                    var axisInB             = math.rotate(aInBSpace.rot, axis);
                    var supportBinB         = math.chgsign(halfSizeB, axisInB);
                    var supportB            = math.rotate(bInASpace.rot, supportBinB);
                    var offsetB             = math.dot(supportB, axis);
                    var centerB             = math.dot(bInASpace.pos, axis);
                    var maxB                = centerB + offsetB;
                    var minB                = centerB - offsetB;
                    var azPositiveDistances = minB - maxA;
                    var azNegativeDistances = minA - maxB;
                    axis                    = math.select(axis, -axis, azPositiveDistances < azNegativeDistances);
                }
                bool3 maskA                    = false;
                bool3 maskB                    = false;
                maskA[(bestEdgeIndex - 7) / 3] = true;
                maskB[(bestEdgeIndex - 7) % 3] = true;
                var aSigns                     = math.select(axis, 1f, maskA);
                var aSupportP                  = math.chgsign(halfSizeA, aSigns);
                var aSupportE                  = math.select(0f, -2f * halfSizeA, maskA);
                var edgeAxisInB                = math.rotate(aInBSpace.rot, axis);
                var bSigns                     = math.select(-edgeAxisInB, 1f, maskB);
                var bSupportPinB               = math.chgsign(halfSizeB, bSigns);
                var bSupportEinB               = math.select(0f, -2f * halfSizeB, maskB);
                var bSupportP                  = math.transform(bInASpace, bSupportPinB);
                var bSupportE                  = math.rotate(bInASpace, bSupportEinB);

                // Look for the ordinate of the axis closest to zero. Flipping that should give us the next best support.
                var absAxis            = math.abs(axis);
                var aFlipMask          = absAxis == math.cmin(math.select(absAxis, float.MaxValue, maskA));
                var aAlternateSupportP = math.select(aSupportP, -aSupportP, aFlipMask);

                var absAxisInB         = math.abs(edgeAxisInB);
                var bFlipMask          = absAxisInB == math.cmin(math.select(absAxisInB, float.MaxValue, maskB));
                var bAlternateSupportP = math.transform(bInASpace, math.select(bSupportPinB, -bSupportPinB, bFlipMask));

                var aStarts = new simdFloat3(aSupportP, aSupportP, aAlternateSupportP, aAlternateSupportP);
                var bStarts = new simdFloat3(bSupportP, bAlternateSupportP, bSupportP, bAlternateSupportP);
                var valid   = CapsuleCapsule.SegmentSegmentInvalidateEndpointsPointEdge(aStarts,
                                                                                        new simdFloat3(aSupportE),
                                                                                        bStarts,
                                                                                        new simdFloat3(bSupportE),
                                                                                        out var closestAs,
                                                                                        out var closestBs);
                if (math.any(valid))
                {
                    var distSq = math.cmin(math.select(float.MaxValue, simd.distancesq(closestAs, closestBs), valid));
                    if (distSq > bestEdgeDistanceSq)
                    {
                        foundClosestEdge   = true;
                        bestEdgeDistanceSq = distSq;
                    }
                }
                bestAxesMask.SetBits(bestEdgeIndex, false);
            }
            if (foundClosestEdge)
            {
                // If our edge-edge pair is closer than any face axis, then we know this is our distance and can skip the point-face tests.
                var faceSeparations03 = new float4(separations[0], separations[1], separations[2], separations[3]);
                var faceSeparations47 = new float4(separations[4], separations[5], separations[6], separations[7]);
                var maxFaceSeparation = math.cmax(math.max(faceSeparations03, faceSeparations47));
                if (maxFaceSeparation < 0f || bestEdgeDistanceSq <= maxFaceSeparation * maxFaceSeparation)
                {
                    return bestEdgeDistanceSq <= maxDistance * maxDistance;
                }
            }

            // Transform each box's vertices into the other's space, and then compare to the clamped.
            for (int i = 0; i < 16; i++)
            {
                var rotX   = math.select(aInBSpace.rot.value.x, bInASpace.rot.value.x, i >= 8);
                var rotY   = math.select(aInBSpace.rot.value.y, bInASpace.rot.value.y, i >= 8);
                var rotZ   = math.select(aInBSpace.rot.value.z, bInASpace.rot.value.z, i >= 8);
                var rotW   = math.select(aInBSpace.rot.value.w, bInASpace.rot.value.w, i >= 8);
                var posX   = math.select(aInBSpace.pos.x, bInASpace.pos.x, i >= 8);
                var posY   = math.select(aInBSpace.pos.y, bInASpace.pos.z, i >= 8);
                var posZ   = math.select(aInBSpace.pos.z, bInASpace.pos.y, i >= 8);
                var pointX = math.select(halfSizeA.x, halfSizeB.x, i >= 8);
                var pointY = math.select(halfSizeA.y, halfSizeB.y, i >= 8);
                var pointZ = math.select(halfSizeA.z, halfSizeB.z, i >= 8);
                var halfX  = math.select(halfSizeB.x, halfSizeA.x, i >= 8);
                var halfY  = math.select(halfSizeB.y, halfSizeA.y, i >= 8);
                var halfZ  = math.select(halfSizeB.z, halfSizeA.z, i >= 8);

                pointX = math.select(pointX, -pointX, (i & 1) != 0);
                pointY = math.select(pointY, -pointY, (i & 2) != 0);
                pointZ = math.select(pointZ, -pointZ, (i & 4) != 0);

                // Transform point
                var tx                = 2f * (rotY * pointZ - rotZ * pointY);
                var ty                = 2f * (rotZ * pointX - rotX * pointZ);
                var tz                = 2f * (rotX * pointY - rotY * pointX);
                var transformedPointX = posX + pointX + rotW * tx + (rotY * tz - rotZ * ty);
                var transformedPointY = posY + pointY + rotW * ty + (rotZ * tx - rotX * tz);
                var transformedPointZ = posZ + pointZ + rotW * tz + (rotX * ty - rotY * tx);

                var diffX = transformedPointX - math.clamp(transformedPointX, -halfX, halfX);
                var diffY = transformedPointY - math.clamp(transformedPointY, -halfY, halfY);
                var diffZ = transformedPointZ - math.clamp(transformedPointZ, -halfZ, halfZ);

                separations[i] = diffX * diffX + diffY * diffY + diffZ * diffZ;
            }

            // Find the best vertex distance.
            uint bestValue = uint.MaxValue;
            for (int i = 0; i < 16; i++)
            {
                var candidate = math.asuint(separations[i]);
                bestValue     = math.min(candidate, bestValue);
            }
            var bestDistSq = math.min(math.asfloat(bestValue), bestEdgeDistanceSq);
            return bestDistSq <= maxDistance * maxDistance;
        }

        // This custom algorithm is a bit weird, but is more reliable than GJK+EPA. It is a mix of SAT and Lin-Canny.
        // The first step is to perform SAT to determine if the boxes are intersecting or not. If they are, and
        // the minimum axis is along the face normal, then we try to find a penetrating vertex that projects onto
        // the face. Otherwise, we find an edge-edge penetration.
        // If SAT determines an outside hit, we use a Lin-Canny feature pair test with a couple massive shortcuts.
        // For any vertex on one box, we can find the closest point on the other box (and thus the closest feature)
        // simply by transforming the vertex into the other box's local space, and then clamping it to the box volume.
        // For edge-edge tests, we know the closest points lie on the closest edges along the separating axis.
        private static bool BoxBoxDistance(float3 halfSizeA,
                                           float3 halfSizeB,
                                           in RigidTransform bInASpace,
                                           in RigidTransform aInBSpace,
                                           float maxDistance,
                                           out ColliderDistanceResultInternal result)
        {
            // Do initial SAT test
            var         bInARotMat = new float3x3(bInASpace.rot);
            Span<float> axesX      =
                stackalloc float[] { 1f, 0f, 0f, bInARotMat.c0.x, bInARotMat.c1.x, bInARotMat.c2.x, 1f, 0f, 0f, 0f, bInARotMat.c0.z, bInARotMat.c1.z, bInARotMat.c2.z,
                                     -bInARotMat.c0.y, -bInARotMat.c1.y, -bInARotMat.c2.y };
            Span<float> axesY =
                stackalloc float[] { 0f, 1f, 0f, bInARotMat.c0.y, bInARotMat.c1.y, bInARotMat.c2.y, 0f, -bInARotMat.c0.z, -bInARotMat.c1.z, -bInARotMat.c2.z, 0f, 0f, 0f,
                                     bInARotMat.c0.x, bInARotMat.c1.x, bInARotMat.c2.x };
            Span<float> axesZ =
                stackalloc float[] { 0f, 0f, 1f, bInARotMat.c0.z, bInARotMat.c1.z, bInARotMat.c2.z, 0f, bInARotMat.c0.y, bInARotMat.c1.y, bInARotMat.c2.y, -bInARotMat.c0.x,
                                     -bInARotMat.c1.x, -bInARotMat.c2.x, 0f, 0f, 0f };
            Span<float> separations = stackalloc float[16];
            for (int i = 0; i < 16; i++)
            {
                var axisX         = axesX[i];
                var axisY         = axesY[i];
                var axisZ         = axesZ[i];
                var axisLenSq     = axisX * axisX + axisY * axisY + axisZ * axisZ;
                var valid         = axisLenSq > math.FLT_MIN_NORMAL;
                var axisLenRsqrt  = math.rsqrt(axisLenSq);
                axisX            *= axisLenRsqrt;
                axisY            *= axisLenRsqrt;
                axisZ            *= axisLenRsqrt;

                var aSupportX = math.chgsign(halfSizeA.x, axisX);
                var aSupportY = math.chgsign(halfSizeA.y, axisY);
                var aSupportZ = math.chgsign(halfSizeA.z, axisZ);

                // Scalar implementation of mul(aInBSpace.rot, axis)
                var tx       = 2f * (aInBSpace.rot.value.y * axisZ - aInBSpace.rot.value.z * axisY);
                var ty       = 2f * (aInBSpace.rot.value.z * axisX - aInBSpace.rot.value.x * axisZ);
                var tz       = 2f * (aInBSpace.rot.value.x * axisY - aInBSpace.rot.value.y * axisX);
                var axisInBX = axisX + aInBSpace.rot.value.w * tx + (aInBSpace.rot.value.y * tz - aInBSpace.rot.value.z * ty);
                var axisInBY = axisY + aInBSpace.rot.value.w * ty + (aInBSpace.rot.value.z * tx - aInBSpace.rot.value.x * tz);
                var axisInBZ = axisZ + aInBSpace.rot.value.w * tz + (aInBSpace.rot.value.x * ty - aInBSpace.rot.value.y * tx);

                var bSupportInBX  = math.chgsign(halfSizeB.x, -axisInBX);
                var bSupportInBY  = math.chgsign(halfSizeB.y, -axisInBY);
                var bSupportInBZ  = math.chgsign(halfSizeB.z, -axisInBZ);
                tx                = 2f * (bInASpace.rot.value.y * bSupportInBZ - bInASpace.rot.value.z * bSupportInBY);
                ty                = 2f * (bInASpace.rot.value.z * bSupportInBX - bInASpace.rot.value.x * bSupportInBZ);
                tz                = 2f * (bInASpace.rot.value.x * bSupportInBY - bInASpace.rot.value.y * bSupportInBX);
                var bSupportRelBX = bSupportInBX + bInASpace.rot.value.w * tx + (bInASpace.rot.value.y * tz - bInASpace.rot.value.z * ty);
                var bSupportRelBY = bSupportInBY + bInASpace.rot.value.w * ty + (bInASpace.rot.value.z * tx - bInASpace.rot.value.x * tz);
                var bSupportRelBZ = bSupportInBZ + bInASpace.rot.value.w * tz + (bInASpace.rot.value.x * ty - bInASpace.rot.value.y * tx);
                var bSupportX     = bInASpace.pos.x + bSupportRelBX;
                var bSupportY     = bInASpace.pos.y + bSupportRelBY;
                var bSupportZ     = bInASpace.pos.z + bSupportRelBZ;

                var aSupportDot      = aSupportX * axisX + aSupportY * axisY + aSupportZ * axisZ;
                var bSupportDot      = bSupportX * axisX + bSupportY * axisY + bSupportZ * axisZ;
                var separationBFromA = bSupportDot - aSupportDot;

                var negBSupportX     = bInASpace.pos.x - bSupportRelBX;
                var negBSupportY     = bInASpace.pos.y - bSupportRelBY;
                var negBSupportZ     = bInASpace.pos.z - bSupportRelBZ;
                var negASupportDot   = -aSupportDot;
                var negBSupportDot   = negBSupportX * axisX + negBSupportY * axisY + negBSupportZ * axisZ;
                var separationAFromB = negASupportDot - negBSupportDot;

                separations[i] = math.select(float.MinValue, math.max(separationAFromB, separationBFromA), valid);
            }

            // Find maximum separation
            float bestSeparation;
            {
                Span<float> maxes = stackalloc float[8];
                for (int i = 0; i < 8; i++)
                {
                    maxes[i] = math.max(separations[i], separations[i + 8]);
                }
                for (int i = 0; i < 4; i++)
                {
                    maxes[i] = math.max(maxes[i], maxes[i + 4]);
                }
                bestSeparation = math.cmax(new float4(maxes[0], maxes[1], maxes[2], maxes[3]));
            }
            // If the max separation is beyond our max distance, exit early.
            if (bestSeparation > maxDistance)
            {
                result = default;
                return false;
            }

            // Find all axes that match our best separation, and make a mask out of them.
            // We fudge this a little so that we avoid cases where floating-point error steers us down the wrong path.
            uint  bestAxesMaskRaw = 0;
            float epsilon         = math.EPSILON * math.max(math.abs(bestSeparation), 128f);
            for (int i = 0; i < 16; i++)
            {
                uint toAdd       = 1u << i;
                bestAxesMaskRaw += math.select(0, toAdd, math.distance(separations[i], bestSeparation) <= epsilon);
            }
            var bestAxesMask = new BitField32(bestAxesMaskRaw);
            bestAxesMask.SetBits(6, false);

            if (bestSeparation <= 0f)
            {
                // We have penetration. Run through the gauntlet of equal separations and try to find the first valid hit.
                // We start with the point-face pairs, prioritizing y, then z, then x.
                static simdFloat3 GetMultipleSupports(float3 direction, float3 halfSize, float epsilon, out int4 featureCodes)
                {
                    simdFloat3 result = default;
                    bool4      tfMask = new bool4(true, true, false, false);
                    bool4      xPositives;
                    bool4      yPositives;
                    bool4      zPositives;
                    var        directionZero = math.abs(direction) <= epsilon;
                    if (directionZero.x)
                    {
                        xPositives = tfMask;
                        tfMask     = new bool4(true, false, true, false);
                    }
                    else
                        xPositives = direction.x > 0f;
                    if (directionZero.y)
                    {
                        yPositives = tfMask;
                        tfMask     = new bool4(true, false, true, false);
                    }
                    else
                        yPositives = direction.y > 0f;
                    if (directionZero.z)
                        zPositives = tfMask;
                    else
                        zPositives  = direction.z > 0f;
                    featureCodes    = 0;
                    featureCodes   += math.select(1, int4.zero, xPositives);
                    result.x        = math.select(-halfSize.x, halfSize.x, xPositives);
                    featureCodes   += math.select(2, int4.zero, yPositives);
                    result.y        = math.select(-halfSize.y, halfSize.y, yPositives);
                    featureCodes   += math.select(4, int4.zero, zPositives);
                    result.z        = math.select(-halfSize.z, halfSize.z, zPositives);
                    return result;
                }

                if (bestAxesMask.IsSet(1))
                {
                    // There's a vertex on B closest to a face A along A's y axis.
                    var aFaceNormal    = new float3(0f, math.chgsign(1f, bInASpace.pos.y), 0f);
                    var aFaceNormalInB = math.rotate(aInBSpace.rot, aFaceNormal);
                    if (math.all(math.abs(aFaceNormalInB) > epsilon))
                    {
                        // There is a singular closest vertex in B
                        var faceSupportB = math.transform(bInASpace, math.chgsign(halfSizeB, -aFaceNormalInB));
                        // Check that the vertex is inside A (or fully penetrated through). If this fails, we'll look for a different axis.
                        var clampedSupport = math.clamp(faceSupportB.xz, -halfSizeA.xz - epsilon, halfSizeA.xz + epsilon);
                        if (clampedSupport.Equals(faceSupportB.xz))
                        {
                            clampedSupport   = math.clamp(faceSupportB.xz, -halfSizeA.xz, halfSizeA.xz);
                            var featureCodeB = (ushort)math.bitmask(new bool4(-aFaceNormalInB < 0f, false));
                            result           = new ColliderDistanceResultInternal
                            {
                                distance     = bestSeparation,
                                hitpointA    = new float3(clampedSupport.x, aFaceNormal.y * halfSizeA.y, clampedSupport.y),
                                hitpointB    = faceSupportB,
                                normalA      = aFaceNormal,
                                normalB      = math.rotate(bInASpace.rot, PointRayBox.BoxNormalFromFeatureCode(featureCodeB)),
                                featureCodeA = (ushort)(0x8000 + math.select(1, 4, aFaceNormal.y < 0f)),
                                featureCodeB = featureCodeB
                            };
                            return true;
                        }
                    }
                    else
                    {
                        // There are multiple vertices in B equally close to the A plane. We need to try all of them.
                        var faceSupportsBinB = GetMultipleSupports(-aFaceNormalInB, halfSizeB, epsilon, out var featureCodesB);
                        var faceSupportsB    = simd.transform(bInASpace, faceSupportsBinB);
                        var clampedSupportsX = math.clamp(faceSupportsB.x, -halfSizeA.x, halfSizeA.x);
                        var clampedSupportsZ = math.clamp(faceSupportsB.z, -halfSizeA.z, halfSizeA.z);
                        var candidates       = (math.abs(faceSupportsB.x - clampedSupportsX) < epsilon) & (math.abs(faceSupportsB.z - clampedSupportsZ) < epsilon);
                        if (math.any(candidates))
                        {
                            var supportIndex = math.tzcnt(math.bitmask(candidates));
                            var faceSupportB = faceSupportsB[supportIndex];
                            var featureCodeB = (ushort)featureCodesB[supportIndex];
                            result           = new ColliderDistanceResultInternal
                            {
                                distance     = bestSeparation,
                                hitpointA    = new float3(clampedSupportsX[supportIndex], aFaceNormal.y * halfSizeA.y, clampedSupportsZ[supportIndex]),
                                hitpointB    = faceSupportB,
                                normalA      = aFaceNormal,
                                normalB      = math.rotate(bInASpace.rot, PointRayBox.BoxNormalFromFeatureCode(featureCodeB)),
                                featureCodeA = (ushort)(0x8000 + math.select(1, 4, aFaceNormal.y < 0f)),
                                featureCodeB = featureCodeB
                            };
                            return true;
                        }
                    }
                }
                if (bestAxesMask.IsSet(4))
                {
                    // There's a vertex on A closest to a face B along B's y axis
                    var bFaceNormal    = new float3(0f, math.chgsign(1f, aInBSpace.pos.y), 0f);
                    var bFaceNormalInA = math.rotate(bInASpace.rot, bFaceNormal);
                    if (math.all(math.abs(bFaceNormalInA) > epsilon))
                    {
                        // There is a singular closest vertex in A
                        var faceSupportAinA = math.chgsign(halfSizeA, -bFaceNormalInA);
                        var faceSupportA    = math.transform(aInBSpace, faceSupportAinA);
                        // Check that the vertex is inside A (or fully penetrated through). If this fails, we'll look for a different axis.
                        var clampedSupport = math.clamp(faceSupportA.xz, -halfSizeB.xz - epsilon, halfSizeB.xz + epsilon);
                        if (clampedSupport.Equals(faceSupportA.xz))
                        {
                            clampedSupport   = math.clamp(faceSupportA.xz, -halfSizeB.xz, halfSizeB.xz);
                            var featureCodeA = (ushort)math.bitmask(new bool4(-bFaceNormalInA < 0f, false));
                            var hitpointB    = new float3(clampedSupport.x, bFaceNormal.y * halfSizeB.y, clampedSupport.y);
                            result           = new ColliderDistanceResultInternal
                            {
                                distance     = bestSeparation,
                                hitpointA    = faceSupportAinA,
                                hitpointB    = math.transform(bInASpace, hitpointB),
                                normalA      = PointRayBox.BoxNormalFromFeatureCode(featureCodeA),
                                normalB      = bFaceNormalInA,
                                featureCodeA = featureCodeA,
                                featureCodeB = (ushort)(0x8000 + math.select(1, 4, bFaceNormal.y < 0f)),
                            };
                            return true;
                        }
                    }
                    else
                    {
                        // There are multiple verices in A equally close to the B plane. We need to try all of them.
                        var faceSupportsAinA = GetMultipleSupports(-bFaceNormalInA, halfSizeA, epsilon, out var featureCodesA);
                        var faceSupportsA    = simd.transform(aInBSpace, faceSupportsAinA);
                        var clampedSupportsX = math.clamp(faceSupportsA.x, -halfSizeB.x, halfSizeB.x);
                        var clampedSupportsZ = math.clamp(faceSupportsA.z, -halfSizeB.z, halfSizeB.z);
                        var candidates       = (math.abs(faceSupportsA.x - clampedSupportsX) < epsilon) & (math.abs(faceSupportsA.z - clampedSupportsZ) < epsilon);
                        if (math.any(candidates))
                        {
                            var supportIndex    = math.tzcnt(math.bitmask(candidates));
                            var faceSupportAinA = faceSupportsAinA[supportIndex];
                            var featureCodeA    = (ushort)featureCodesA[supportIndex];
                            var hitpointB       = new float3(clampedSupportsX[supportIndex], bFaceNormal.y * halfSizeB.y, clampedSupportsZ[supportIndex]);
                            result              = new ColliderDistanceResultInternal
                            {
                                distance     = bestSeparation,
                                hitpointA    = faceSupportAinA,
                                hitpointB    = math.transform(bInASpace, hitpointB),
                                normalA      = PointRayBox.BoxNormalFromFeatureCode(featureCodeA),
                                normalB      = bFaceNormalInA,
                                featureCodeA = featureCodeA,
                                featureCodeB = (ushort)(0x8000 + math.select(1, 4, bFaceNormal.y < 0f)),
                            };
                            return true;
                        }
                    }
                }
                if (bestAxesMask.IsSet(2))
                {
                    // There's a vertex on B closest to a face A along A's z axis.
                    var aFaceNormal    = new float3(0f, 0f, math.chgsign(1f, bInASpace.pos.z));
                    var aFaceNormalInB = math.rotate(aInBSpace.rot, aFaceNormal);
                    if (math.all(math.abs(aFaceNormalInB) > epsilon))
                    {
                        // There is a singular closest vertex in B
                        var faceSupportB = math.transform(bInASpace, math.chgsign(halfSizeB, -aFaceNormalInB));
                        // Check that the vertex is inside A (or fully penetrated through). If this fails, we'll look for a different axis.
                        var clampedSupport = math.clamp(faceSupportB.xy, -halfSizeA.xy - epsilon, halfSizeA.xy + epsilon);
                        if (clampedSupport.Equals(faceSupportB.xy))
                        {
                            clampedSupport   = math.clamp(faceSupportB.xy, -halfSizeA.xy, halfSizeA.xy);
                            var featureCodeB = (ushort)math.bitmask(new bool4(-aFaceNormalInB < 0f, false));
                            result           = new ColliderDistanceResultInternal
                            {
                                distance     = bestSeparation,
                                hitpointA    = new float3(clampedSupport.xy, aFaceNormal.z * halfSizeA.z),
                                hitpointB    = faceSupportB,
                                normalA      = aFaceNormal,
                                normalB      = math.rotate(bInASpace.rot, PointRayBox.BoxNormalFromFeatureCode(featureCodeB)),
                                featureCodeA = (ushort)(0x8000 + math.select(2, 5, aFaceNormal.z < 0f)),
                                featureCodeB = featureCodeB
                            };
                            return true;
                        }
                    }
                    else
                    {
                        // There are multiple vertices in B equally close to the A plane. We need to try all of them.
                        var faceSupportsBinB = GetMultipleSupports(-aFaceNormalInB, halfSizeB, epsilon, out var featureCodesB);
                        var faceSupportsB    = simd.transform(bInASpace, faceSupportsBinB);
                        var clampedSupportsX = math.clamp(faceSupportsB.x, -halfSizeA.x, halfSizeA.x);
                        var clampedSupportsY = math.clamp(faceSupportsB.y, -halfSizeA.y, halfSizeA.y);
                        var candidates       = (math.abs(faceSupportsB.x - clampedSupportsX) < epsilon) & (math.abs(faceSupportsB.y - clampedSupportsY) < epsilon);
                        if (math.any(candidates))
                        {
                            var supportIndex = math.tzcnt(math.bitmask(candidates));
                            var faceSupportB = faceSupportsB[supportIndex];
                            var featureCodeB = (ushort)featureCodesB[supportIndex];
                            result           = new ColliderDistanceResultInternal
                            {
                                distance     = bestSeparation,
                                hitpointA    = new float3(clampedSupportsX[supportIndex], clampedSupportsY[supportIndex], aFaceNormal.z * halfSizeA.z),
                                hitpointB    = faceSupportB,
                                normalA      = aFaceNormal,
                                normalB      = math.rotate(bInASpace.rot, PointRayBox.BoxNormalFromFeatureCode(featureCodeB)),
                                featureCodeA = (ushort)(0x8000 + math.select(2, 5, aFaceNormal.z < 0f)),
                                featureCodeB = featureCodeB
                            };
                            return true;
                        }
                    }
                }
                if (bestAxesMask.IsSet(5))
                {
                    // There's a vertex on A closest to a face B along B's z axis
                    var bFaceNormal    = new float3(0f, 0f, math.chgsign(1f, aInBSpace.pos.z));
                    var bFaceNormalInA = math.rotate(bInASpace.rot, bFaceNormal);
                    if (math.all(math.abs(bFaceNormalInA) > epsilon))
                    {
                        // There is a singular closest vertex in A
                        var faceSupportAinA = math.chgsign(halfSizeA, -bFaceNormalInA);
                        var faceSupportA    = math.transform(aInBSpace, faceSupportAinA);
                        // Check that the vertex is inside A (or fully penetrated through). If this fails, we'll look for a different axis.
                        var clampedSupport = math.clamp(faceSupportA.xy, -halfSizeB.xy - epsilon, halfSizeB.xy + epsilon);
                        if (clampedSupport.Equals(faceSupportA.xy))
                        {
                            clampedSupport   = math.clamp(faceSupportA.xy, -halfSizeB.xy, halfSizeB.xy);
                            var featureCodeA = (ushort)math.bitmask(new bool4(-bFaceNormalInA < 0f, false));
                            var hitpointB    = new float3(clampedSupport.xy, bFaceNormal.z * halfSizeB.z);
                            result           = new ColliderDistanceResultInternal
                            {
                                distance     = bestSeparation,
                                hitpointA    = faceSupportAinA,
                                hitpointB    = math.transform(bInASpace, hitpointB),
                                normalA      = PointRayBox.BoxNormalFromFeatureCode(featureCodeA),
                                normalB      = bFaceNormalInA,
                                featureCodeA = featureCodeA,
                                featureCodeB = (ushort)(0x8000 + math.select(2, 5, bFaceNormal.z < 0f)),
                            };
                            return true;
                        }
                    }
                    else
                    {
                        // There are multiple verices in A equally close to the B plane. We need to try all of them.
                        var faceSupportsAinA = GetMultipleSupports(-bFaceNormalInA, halfSizeA, epsilon, out var featureCodesA);
                        var faceSupportsA    = simd.transform(aInBSpace, faceSupportsAinA);
                        var clampedSupportsX = math.clamp(faceSupportsA.x, -halfSizeB.x, halfSizeB.x);
                        var clampedSupportsY = math.clamp(faceSupportsA.y, -halfSizeB.y, halfSizeB.y);
                        var candidates       = (math.abs(faceSupportsA.x - clampedSupportsX) < epsilon) & (math.abs(faceSupportsA.y - clampedSupportsY) < epsilon);
                        if (math.any(candidates))
                        {
                            var supportIndex    = math.tzcnt(math.bitmask(candidates));
                            var faceSupportAinA = faceSupportsAinA[supportIndex];
                            var featureCodeA    = (ushort)featureCodesA[supportIndex];
                            var hitpointB       = new float3(clampedSupportsX[supportIndex], clampedSupportsY[supportIndex], bFaceNormal.z * halfSizeB.z);
                            result              = new ColliderDistanceResultInternal
                            {
                                distance     = bestSeparation,
                                hitpointA    = faceSupportAinA,
                                hitpointB    = math.transform(bInASpace, hitpointB),
                                normalA      = PointRayBox.BoxNormalFromFeatureCode(featureCodeA),
                                normalB      = bFaceNormalInA,
                                featureCodeA = featureCodeA,
                                featureCodeB = (ushort)(0x8000 + math.select(2, 5, bFaceNormal.z < 0f)),
                            };
                            return true;
                        }
                    }
                }
                if (bestAxesMask.IsSet(0))
                {
                    // There's a vertex on B closest to a face A along A's x axis.
                    var aFaceNormal    = new float3(math.chgsign(1f, bInASpace.pos.x), 0f, 0f);
                    var aFaceNormalInB = math.rotate(aInBSpace.rot, aFaceNormal);
                    if (math.all(math.abs(aFaceNormalInB) > epsilon))
                    {
                        // There is a singular closest vertex in B
                        var faceSupportB = math.transform(bInASpace, math.chgsign(halfSizeB, -aFaceNormalInB));
                        // Check that the vertex is inside A (or fully penetrated through). If this fails, we'll look for a different axis.
                        var clampedSupport = math.clamp(faceSupportB.yz, -halfSizeA.yz - epsilon, halfSizeA.yz + epsilon);
                        if (clampedSupport.Equals(faceSupportB.yz))
                        {
                            clampedSupport   = math.clamp(faceSupportB.yz, -halfSizeA.yz, halfSizeA.yz);
                            var featureCodeB = (ushort)math.bitmask(new bool4(-aFaceNormalInB < 0f, false));
                            result           = new ColliderDistanceResultInternal
                            {
                                distance     = bestSeparation,
                                hitpointA    = new float3(aFaceNormal.x * halfSizeA.x, clampedSupport.xy),
                                hitpointB    = faceSupportB,
                                normalA      = aFaceNormal,
                                normalB      = math.rotate(bInASpace.rot, PointRayBox.BoxNormalFromFeatureCode(featureCodeB)),
                                featureCodeA = (ushort)(0x8000 + math.select(0, 3, aFaceNormal.x < 0f)),
                                featureCodeB = featureCodeB
                            };
                            return true;
                        }
                    }
                    else
                    {
                        // There are multiple vertices in B equally close to the A plane. We need to try all of them.
                        var faceSupportsBinB = GetMultipleSupports(-aFaceNormalInB, halfSizeB, epsilon, out var featureCodesB);
                        var faceSupportsB    = simd.transform(bInASpace, faceSupportsBinB);
                        var clampedSupportsY = math.clamp(faceSupportsB.y, -halfSizeA.y, halfSizeA.y);
                        var clampedSupportsZ = math.clamp(faceSupportsB.z, -halfSizeA.z, halfSizeA.z);
                        var candidates       = (math.abs(faceSupportsB.y - clampedSupportsY) < epsilon) & (math.abs(faceSupportsB.z - clampedSupportsZ) < epsilon);
                        if (math.any(candidates))
                        {
                            var supportIndex = math.tzcnt(math.bitmask(candidates));
                            var faceSupportB = faceSupportsB[supportIndex];
                            var featureCodeB = (ushort)featureCodesB[supportIndex];
                            result           = new ColliderDistanceResultInternal
                            {
                                distance     = bestSeparation,
                                hitpointA    = new float3(aFaceNormal.x * halfSizeA.x, clampedSupportsY[supportIndex], clampedSupportsZ[supportIndex]),
                                hitpointB    = faceSupportB,
                                normalA      = aFaceNormal,
                                normalB      = math.rotate(bInASpace.rot, PointRayBox.BoxNormalFromFeatureCode(featureCodeB)),
                                featureCodeA = (ushort)(0x8000 + math.select(0, 3, aFaceNormal.x < 0f)),
                                featureCodeB = featureCodeB
                            };
                            return true;
                        }
                    }
                }
                if (bestAxesMask.IsSet(3))
                {
                    // There's a vertex on A closest to a face B along B's x axis
                    var bFaceNormal    = new float3(math.chgsign(1f, aInBSpace.pos.x), 0f, 0f);
                    var bFaceNormalInA = math.rotate(bInASpace.rot, bFaceNormal);
                    if (math.all(math.abs(bFaceNormalInA) > epsilon))
                    {
                        // There is a singular closest vertex in A
                        var faceSupportAinA = math.chgsign(halfSizeA, -bFaceNormalInA);
                        var faceSupportA    = math.transform(aInBSpace, faceSupportAinA);
                        // Check that the vertex is inside A (or fully penetrated through). If this fails, we'll look for a different axis.
                        var clampedSupport = math.clamp(faceSupportA.yz, -halfSizeB.yz - epsilon, halfSizeB.yz + epsilon);
                        if (clampedSupport.Equals(faceSupportA.yz))
                        {
                            clampedSupport   = math.clamp(faceSupportA.yz, -halfSizeB.yz, halfSizeB.yz);
                            var featureCodeA = (ushort)math.bitmask(new bool4(-bFaceNormalInA < 0f, false));
                            var hitpointB    = new float3(bFaceNormal.x * halfSizeB.x, clampedSupport.xy);
                            result           = new ColliderDistanceResultInternal
                            {
                                distance     = bestSeparation,
                                hitpointA    = faceSupportAinA,
                                hitpointB    = math.transform(bInASpace, hitpointB),
                                normalA      = PointRayBox.BoxNormalFromFeatureCode(featureCodeA),
                                normalB      = bFaceNormalInA,
                                featureCodeA = featureCodeA,
                                featureCodeB = (ushort)(0x8000 + math.select(0, 3, bFaceNormal.x < 0f)),
                            };
                            return true;
                        }
                    }
                    else
                    {
                        // There are multiple verices in A equally close to the B plane. We need to try all of them.
                        var faceSupportsAinA = GetMultipleSupports(-bFaceNormalInA, halfSizeA, epsilon, out var featureCodesA);
                        var faceSupportsA    = simd.transform(aInBSpace, faceSupportsAinA);
                        var clampedSupportsY = math.clamp(faceSupportsA.y, -halfSizeB.y, halfSizeB.y);
                        var clampedSupportsZ = math.clamp(faceSupportsA.z, -halfSizeB.z, halfSizeB.z);
                        var candidates       = (math.abs(faceSupportsA.y - clampedSupportsY) < epsilon) & (math.abs(faceSupportsA.z - clampedSupportsZ) < epsilon);
                        if (math.any(candidates))
                        {
                            var supportIndex    = math.tzcnt(math.bitmask(candidates));
                            var faceSupportAinA = faceSupportsAinA[supportIndex];
                            var featureCodeA    = (ushort)featureCodesA[supportIndex];
                            var hitpointB       = new float3(bFaceNormal.x * halfSizeB.x, clampedSupportsY[supportIndex], clampedSupportsZ[supportIndex]);
                            result              = new ColliderDistanceResultInternal
                            {
                                distance     = bestSeparation,
                                hitpointA    = faceSupportAinA,
                                hitpointB    = math.transform(bInASpace, hitpointB),
                                normalA      = PointRayBox.BoxNormalFromFeatureCode(featureCodeA),
                                normalB      = bFaceNormalInA,
                                featureCodeA = featureCodeA,
                                featureCodeB = (ushort)(0x8000 + math.select(0, 3, bFaceNormal.x < 0f)),
                            };
                            return true;
                        }
                    }
                }

                // Since we haven't returned already, we have edge-edge penetration.
                bestAxesMask.SetBits(0, false, 6);
                if (bestAxesMask.Value == 0)
                {
                    // Our point-face axes failed, and due to floating-point precision, our edge-edge axes aren't quite as good.
                    // So we need to find the next best separation value.
                    bestSeparation =
                        math.max(separations[7],
                                 math.cmax(math.max(new float4(separations[8], separations[9], separations[10], separations[11]),
                                                    new float4(separations[12], separations[13], separations[14], separations[15]))));
                    bestAxesMaskRaw = 0;
                    for (int i = 7; i < 16; i++)
                    {
                        uint toAdd       = 1u << i;
                        bestAxesMaskRaw += math.select(0, toAdd, separations[i] == bestSeparation);
                    }
                    bestAxesMask.Value = bestAxesMaskRaw;
                }
                // There may be multiple edge axes we can test, so we loop through until we find one that produces a good result,
                // or we are on the last candidate.
                while (bestAxesMask.Value != 0)
                {
                    var bestIndex = bestAxesMask.CountTrailingZeros();
                    var axis      = math.normalize(new float3(axesX[bestIndex], axesY[bestIndex], axesZ[bestIndex]));
                    {
                        var supportA            = math.chgsign(halfSizeA, axis);
                        var maxA                = math.dot(supportA, axis);
                        var minA                = -maxA;
                        var axisInB             = math.rotate(aInBSpace.rot, axis);
                        var supportBinB         = math.chgsign(halfSizeB, axisInB);
                        var supportB            = math.rotate(bInASpace.rot, supportBinB);
                        var offsetB             = math.dot(supportB, axis);
                        var centerB             = math.dot(bInASpace.pos, axis);
                        var maxB                = centerB + offsetB;
                        var minB                = centerB - offsetB;
                        var azPositiveDistances = minB - maxA;
                        var azNegativeDistances = minA - maxB;
                        axis                    = math.select(axis, -axis, azPositiveDistances < azNegativeDistances);
                    }
                    bool3 maskA                = false;
                    bool3 maskB                = false;
                    maskA[(bestIndex - 7) / 3] = true;
                    maskB[(bestIndex - 7) % 3] = true;

                    // first letter = box, second letter = p is point, o is other point
                    var aSigns       = math.select(axis, 1f, maskA);
                    var aSupportP    = math.chgsign(halfSizeA, aSigns);
                    var aSupportO    = math.select(aSupportP, -aSupportP, maskA);
                    var edgeAxisInB  = math.rotate(aInBSpace.rot, axis);
                    var bSigns       = math.select(-edgeAxisInB, 1f, maskB);
                    var bSupportPinB = math.chgsign(halfSizeB, bSigns);
                    var bSupportOinB = math.select(bSupportPinB, -bSupportPinB, maskB);
                    var bSupportP    = math.transform(bInASpace, bSupportPinB);
                    var bSupportO    = math.transform(bInASpace, bSupportOinB);
                    CapsuleCapsule.SegmentSegment(aSupportP, aSupportO, bSupportP, bSupportO, out var closestA, out var closestB, out _, out _);
                    var closestAinB = math.transform(aInBSpace, closestA);
                    // The two points should be on or inside each other's boxes, or else we picked up the wrong edges
                    // (edges of parallel faces could have multiple valid support points)
                    bool valid  = math.clamp(closestAinB, -halfSizeB - epsilon, halfSizeB + epsilon).Equals(closestAinB);
                    valid      &= math.clamp(closestB, -halfSizeA - epsilon, halfSizeA + epsilon).Equals(closestB);
                    valid      &= math.distance(math.distance(closestA, closestB), math.abs(bestSeparation)) <= epsilon;
                    if (!valid)
                    {
                        // Look for the ordinate of the axis closest to zero. Flipping that should give us the next best support.
                        var absAxis            = math.abs(axis);
                        var aFlipMask          = absAxis == math.cmin(absAxis);
                        var aAlternateSupportP = math.select(aSupportP, -aSupportP, aFlipMask);

                        var absAxisInB            = math.abs(edgeAxisInB);
                        var bFlipMask             = absAxisInB == math.cmin(absAxisInB);
                        var bAlternateSupportPinB = math.select(bSupportPinB, -bSupportPinB, bFlipMask);
                        var bAlternateSupportP    = math.transform(bInASpace, bAlternateSupportPinB);
                        var bAlternateSupportO    = math.transform(bInASpace, math.select(bAlternateSupportPinB, -bAlternateSupportPinB, maskB));

                        var aStarts = new simdFloat3(aSupportP, aSupportP, aAlternateSupportP, aAlternateSupportP);
                        var aEnds   = simd.select(aStarts, -aStarts, maskA);
                        var bStarts = new simdFloat3(bSupportP, bAlternateSupportP, bSupportP, bAlternateSupportP);
                        var bEnds   = new simdFloat3(bSupportO, bAlternateSupportO, bSupportO, bAlternateSupportO);
                        CapsuleCapsule.SegmentSegment(in aStarts, in aEnds, in bStarts, in bEnds, out var closestAs, out var closestBs);
                        var closestAsInB       = simd.transform(aInBSpace, closestAs);
                        var clampedAs          = simd.clamp(closestAsInB, -halfSizeB, halfSizeB);
                        var clampedBs          = simd.clamp(closestBs, -halfSizeA, halfSizeA);
                        var clampedDistortions = simd.distance(closestAsInB, clampedAs) + simd.distance(closestBs, clampedBs);
                        var bestPairIndex      = math.tzcnt(math.bitmask(clampedDistortions == math.cmin(clampedDistortions)));
                        if ((bestPairIndex & 2) == 2)
                            aSigns = math.select(aSigns, -aSigns, aFlipMask);
                        if ((bestPairIndex & 1) == 1)
                            bSigns  = math.select(bSigns, -bSigns, bFlipMask);
                        closestA    = closestAs[bestPairIndex];
                        closestB    = closestBs[bestPairIndex];
                        closestAinB = closestAsInB[bestPairIndex];
                    }
                    valid  = math.clamp(closestAinB, -halfSizeB - epsilon, halfSizeB + epsilon).Equals(closestAinB);
                    valid &= math.clamp(closestB, -halfSizeA - epsilon, halfSizeA + epsilon).Equals(closestB);
                    valid &= math.distance(math.distance(closestA, closestB), math.abs(bestSeparation)) <= epsilon;
                    if (valid || bestAxesMask.CountBits() == 1)
                    {
                        var normalA    = math.select(math.chgsign(1f / math.sqrt(2f), aSigns), 0f, maskA);
                        var normalBinB = math.select(math.chgsign(1f / math.sqrt(2f), bSigns), 0f, maskB);
                        result         = new ColliderDistanceResultInternal
                        {
                            distance     = bestSeparation,
                            hitpointA    = closestA,
                            hitpointB    = closestB,
                            normalA      = normalA,
                            normalB      = math.rotate(bInASpace.rot, normalBinB),
                            featureCodeA = PointRayBox.FeatureCodeFromBoxNormal(normalA),
                            featureCodeB = PointRayBox.FeatureCodeFromBoxNormal(normalBinB)
                        };
                        return result.distance <= maxDistance;
                    }
                    bestAxesMask.SetBits(bestIndex, false);
                }
            }

            // The boxes are not penetrating. If edges have best separating axes, test those.
            // Otherwise, it can be guaranteed we do not have an edge-edge pair.
            bool foundClosestEdge = false;
            result                = default;
            bestAxesMask.SetBits(0, false, 6);
            float bestEdgeDistance = float.MinValue;

            //if (edgeMaxDistance + 1e-4f >= alignedMaxDistance)
            while (bestAxesMask.Value != 0)
            {
                var bestEdgeIndex = bestAxesMask.CountTrailingZeros();
                var axis          = math.normalize(new float3(axesX[bestEdgeIndex], axesY[bestEdgeIndex], axesZ[bestEdgeIndex]));
                {
                    var supportA            = math.chgsign(halfSizeA, axis);
                    var maxA                = math.dot(supportA, axis);
                    var minA                = -maxA;
                    var axisInB             = math.rotate(aInBSpace.rot, axis);
                    var supportBinB         = math.chgsign(halfSizeB, axisInB);
                    var supportB            = math.rotate(bInASpace.rot, supportBinB);
                    var offsetB             = math.dot(supportB, axis);
                    var centerB             = math.dot(bInASpace.pos, axis);
                    var maxB                = centerB + offsetB;
                    var minB                = centerB - offsetB;
                    var azPositiveDistances = minB - maxA;
                    var azNegativeDistances = minA - maxB;
                    axis                    = math.select(axis, -axis, azPositiveDistances < azNegativeDistances);
                }
                bool3 maskA                    = false;
                bool3 maskB                    = false;
                maskA[(bestEdgeIndex - 7) / 3] = true;
                maskB[(bestEdgeIndex - 7) % 3] = true;
                var aSigns                     = math.select(axis, 1f, maskA);
                var aSupportP                  = math.chgsign(halfSizeA, aSigns);
                var aSupportE                  = math.select(0f, -2f * halfSizeA, maskA);
                var edgeAxisInB                = math.rotate(aInBSpace.rot, axis);
                var bSigns                     = math.select(-edgeAxisInB, 1f, maskB);
                var bSupportPinB               = math.chgsign(halfSizeB, bSigns);
                var bSupportEinB               = math.select(0f, -2f * halfSizeB, maskB);
                var bSupportP                  = math.transform(bInASpace, bSupportPinB);
                var bSupportE                  = math.rotate(bInASpace, bSupportEinB);

                // Look for the ordinate of the axis closest to zero. Flipping that should give us the next best support.
                var absAxis            = math.abs(axis);
                var aFlipMask          = absAxis == math.cmin(math.select(absAxis, float.MaxValue, maskA));
                var aAlternateSupportP = math.select(aSupportP, -aSupportP, aFlipMask);

                var absAxisInB         = math.abs(edgeAxisInB);
                var bFlipMask          = absAxisInB == math.cmin(math.select(absAxisInB, float.MaxValue, maskB));
                var bAlternateSupportP = math.transform(bInASpace, math.select(bSupportPinB, -bSupportPinB, bFlipMask));

                var aStarts = new simdFloat3(aSupportP, aSupportP, aAlternateSupportP, aAlternateSupportP);
                var bStarts = new simdFloat3(bSupportP, bAlternateSupportP, bSupportP, bAlternateSupportP);
                var valid   = CapsuleCapsule.SegmentSegmentInvalidateEndpointsPointEdge(aStarts,
                                                                                        new simdFloat3(aSupportE),
                                                                                        bStarts,
                                                                                        new simdFloat3(bSupportE),
                                                                                        out var closestAs,
                                                                                        out var closestBs);
                if (math.any(valid))
                {
                    var distSqs       = math.select(float.MaxValue, simd.distancesq(closestAs, closestBs), valid);
                    var bestPairIndex = math.tzcnt(math.bitmask(distSqs == math.cmin(distSqs)));
                    var distance      = math.sqrt(distSqs[bestPairIndex]);
                    if (distance > bestEdgeDistance)
                    {
                        if ((bestPairIndex & 2) == 2)
                            aSigns = math.select(aSigns, -aSigns, aFlipMask);
                        if ((bestPairIndex & 1) == 1)
                            bSigns   = math.select(bSigns, -bSigns, bFlipMask);
                        var closestA = closestAs[bestPairIndex];
                        var closestB = closestBs[bestPairIndex];

                        var normalA    = math.select(math.chgsign(1f / math.sqrt(2f), aSigns), 0f, maskA);
                        var normalBinB = math.select(math.chgsign(1f / math.sqrt(2f), bSigns), 0f, maskB);
                        result         = new ColliderDistanceResultInternal
                        {
                            distance     = distance,
                            hitpointA    = closestA,
                            hitpointB    = closestB,
                            normalA      = normalA,
                            normalB      = math.rotate(bInASpace.rot, normalBinB),
                            featureCodeA = PointRayBox.FeatureCodeFromBoxNormal(normalA),
                            featureCodeB = PointRayBox.FeatureCodeFromBoxNormal(normalBinB)
                        };
                        foundClosestEdge = true;
                        bestEdgeDistance = distance;
                    }
                }
                bestAxesMask.SetBits(bestEdgeIndex, false);
            }
            if (foundClosestEdge)
            {
                // Our primary could fail due to being an endpoint. And our alternate might not be as good a point-face
                // due to floating point error. So check that there isn't a face separation that is better than our distance.
                var faceSeparations03 = new float4(separations[0], separations[1], separations[2], separations[3]);
                var faceSeparations47 = new float4(separations[4], separations[5], separations[6], separations[7]);
                var maxFaceSeparation = math.cmax(math.max(faceSeparations03, faceSeparations47));
                if (maxFaceSeparation < 0f || result.distance < maxFaceSeparation)
                {
                    return result.distance <= maxDistance;
                }
            }

            // Transform each box's vertices into the other's space, and then compare to the clamped.
            for (int i = 0; i < 16; i++)
            {
                var rotX   = math.select(aInBSpace.rot.value.x, bInASpace.rot.value.x, i >= 8);
                var rotY   = math.select(aInBSpace.rot.value.y, bInASpace.rot.value.y, i >= 8);
                var rotZ   = math.select(aInBSpace.rot.value.z, bInASpace.rot.value.z, i >= 8);
                var rotW   = math.select(aInBSpace.rot.value.w, bInASpace.rot.value.w, i >= 8);
                var posX   = math.select(aInBSpace.pos.x, bInASpace.pos.x, i >= 8);
                var posY   = math.select(aInBSpace.pos.y, bInASpace.pos.y, i >= 8);
                var posZ   = math.select(aInBSpace.pos.z, bInASpace.pos.z, i >= 8);
                var pointX = math.select(halfSizeA.x, halfSizeB.x, i >= 8);
                var pointY = math.select(halfSizeA.y, halfSizeB.y, i >= 8);
                var pointZ = math.select(halfSizeA.z, halfSizeB.z, i >= 8);
                var halfX  = math.select(halfSizeB.x, halfSizeA.x, i >= 8);
                var halfY  = math.select(halfSizeB.y, halfSizeA.y, i >= 8);
                var halfZ  = math.select(halfSizeB.z, halfSizeA.z, i >= 8);

                pointX = math.select(pointX, -pointX, (i & 1) != 0);
                pointY = math.select(pointY, -pointY, (i & 2) != 0);
                pointZ = math.select(pointZ, -pointZ, (i & 4) != 0);

                // Transform point
                var tx                = 2f * (rotY * pointZ - rotZ * pointY);
                var ty                = 2f * (rotZ * pointX - rotX * pointZ);
                var tz                = 2f * (rotX * pointY - rotY * pointX);
                var transformedPointX = posX + pointX + rotW * tx + (rotY * tz - rotZ * ty);
                var transformedPointY = posY + pointY + rotW * ty + (rotZ * tx - rotX * tz);
                var transformedPointZ = posZ + pointZ + rotW * tz + (rotX * ty - rotY * tx);

                var diffX = transformedPointX - math.clamp(transformedPointX, -halfX, halfX);
                var diffY = transformedPointY - math.clamp(transformedPointY, -halfY, halfY);
                var diffZ = transformedPointZ - math.clamp(transformedPointZ, -halfZ, halfZ);

                var separation = diffX * diffX + diffY * diffY + diffZ * diffZ;
                separations[i] = separation;
            }
            int bestVertexIndex = 0;
            {
                Span<int> indices = stackalloc int[8];
                for (int i = 0; i < 8; i++)
                {
                    if (separations[i] <= separations[i + 8])
                    {
                        indices[i] = i;
                    }
                    else
                    {
                        indices[i]     = i + 8;
                        separations[i] = separations[i + 8];
                    }
                }
                for (int i = 0; i < 4; i++)
                {
                    if (separations[i] <= separations[i + 4])
                    {
                        indices[i] = indices[i];
                    }
                    else
                    {
                        indices[i]     = indices[i + 4];
                        separations[i] = separations[i + 4];
                    }
                }
                for (int i = 0; i < 2; i++)
                {
                    if (separations[i] <= separations[i + 2])
                    {
                        indices[i] = indices[i];
                    }
                    else
                    {
                        indices[i]     = indices[i + 2];
                        separations[i] = separations[i + 2];
                    }
                }
                if (separations[0] <= separations[1])
                {
                    bestSeparation  = separations[0];
                    bestVertexIndex = indices[0];
                }
                else
                {
                    bestSeparation  = separations[1];
                    bestVertexIndex = indices[1];
                }
            }

            if (foundClosestEdge && bestSeparation > result.distance * result.distance)
            {
                // Edges were better.
                return result.distance <= maxDistance;
            }
            if (bestSeparation > maxDistance * maxDistance)
            {
                return false;
            }
            // At this point, we know we have a good point-box pair. Now we can generate the result.
            result.distance = math.sqrt(bestSeparation);
            if (bestVertexIndex < 8)
            {
                var hitpointA     = math.select(halfSizeA, -halfSizeA, (new int3(1, 2, 4) & bestVertexIndex) != 0);
                var hitpointAinB  = math.transform(aInBSpace, hitpointA);
                var hitpointBinB  = math.clamp(hitpointAinB, -halfSizeB, halfSizeB);
                var hitBInsideBox = halfSizeB - math.abs(hitpointBinB);
                // Sometimes due to floating point error, we actually have overlap here. So we need to push the hitpoint out to the surface.
                // First, try to correct very tiny floating point errors, so that we catch edges.
                var isVeryTinyError = hitBInsideBox < 1e-5f;
                hitpointBinB        = math.select(hitpointBinB, math.chgsign(halfSizeB, hitpointBinB), isVeryTinyError);
                hitBInsideBox       = halfSizeB - math.abs(hitpointBinB);
                if (math.all(hitBInsideBox > 0f))
                {
                    // The error is a little bigger. Pick the closest axis and push out to the face.
                    var boostAxis            = math.tzcnt(math.bitmask(new bool4(hitBInsideBox == math.cmin(hitBInsideBox), false)));
                    var boostAmount          = hitBInsideBox[boostAxis];
                    result.distance         -= boostAmount;
                    hitpointBinB[boostAxis]  = math.chgsign(halfSizeB[boostAxis], hitpointBinB[boostAxis]);
                }
                result.hitpointA    = hitpointA;
                result.hitpointB    = math.transform(bInASpace, hitpointBinB);
                result.featureCodeA = (ushort)bestVertexIndex;
                result.normalA      = math.normalize(math.select(1f, -1f, (bestVertexIndex & new int3(1, 2, 4)) != 0));
                result.normalB      = math.normalize(math.select(0f, math.chgsign(1f, hitpointBinB), hitpointBinB == math.chgsign(halfSizeB, hitpointBinB)));
                result.featureCodeB = PointRayBox.FeatureCodeFromBoxNormal(result.normalB);
                result.normalB      = math.rotate(bInASpace.rot, result.normalB);
            }
            else
            {
                var hitpointBinB  = math.select(halfSizeB, -halfSizeB, (new int3(1, 2, 4) & bestVertexIndex) != 0);
                var hitpointB     = math.transform(bInASpace, hitpointBinB);
                var hitpointA     = math.clamp(hitpointB, -halfSizeA, halfSizeA);
                var hitAInsideBox = halfSizeA - math.abs(hitpointB);
                // Sometimes due to floating point error, we actually have overlap here. So we need to push the hitpoint out to the surface.
                // First, try to correct very tiny floating point errors, so that we catch edges.
                var isVeryTinyError = hitAInsideBox < 1e-5f;
                hitpointA           = math.select(hitpointA, math.chgsign(halfSizeA, hitpointA), isVeryTinyError);
                hitAInsideBox       = halfSizeA - math.abs(hitpointA);
                if (math.all(hitAInsideBox > 0f))
                {
                    // The error is a little bigger. Pick the closest axis and push out to the face.
                    var boostAxis         = math.tzcnt(math.bitmask(new bool4(hitAInsideBox == math.cmin(hitAInsideBox), false)));
                    var boostAmount       = hitAInsideBox[boostAxis];
                    result.distance      -= boostAmount;
                    hitpointA[boostAxis]  = math.chgsign(halfSizeA[boostAxis], hitpointA[boostAxis]);
                }
                result.hitpointA    = hitpointA;
                result.hitpointB    = hitpointB;
                result.featureCodeB = (ushort)bestVertexIndex;
                result.normalB      = math.rotate(bInASpace.rot, math.normalize(math.select(1f, -1f, (bestVertexIndex & new int3(1, 2, 4)) != 0)));
                result.normalA      = math.normalize(math.select(0f, math.chgsign(1f, hitpointA), hitpointA == math.chgsign(halfSizeA, hitpointA)));
                result.featureCodeA = PointRayBox.FeatureCodeFromBoxNormal(result.normalA);
            }
            return result.distance <= maxDistance;
        }

        private static bool BoxBoxDistanceSeparated(float3 halfSizeA,
                                                    float3 halfSizeB,
                                                    in AffineTransform bInASpace,
                                                    float maxDistance,
                                                    out ColliderDistanceResultInternal result)
        {
            // Edge pairs.
            simdFloat3 bAxes = new simdFloat3(bInASpace.rs.c0, bInASpace.rs.c1, bInASpace.rs.c2, bInASpace.rs.c0);

            // Ax vs Bs
            simdFloat3 aXCrossBs = simd.rightCross(bAxes);
            // Ensure each axis points such that B's center is ahead of A's center
            aXCrossBs                   = simd.chgsign0Positive(aXCrossBs, simd.dot(aXCrossBs, bInASpace.t));
            simdFloat3 aXCrossAXCrossBs = simd.rightCross(aXCrossBs);
            simdFloat3 bCrossAXCrossBs  = simd.cross(bAxes, aXCrossBs);
            SelectEdgeEdgeSupports(aXCrossBs,
                                   new bool3(true, false, false),
                                   halfSizeA,
                                   halfSizeB,
                                   in bInASpace,
                                   out simdFloat3 aEdgeSupportCentersX,
                                   out simdFloat3 aEdgeAltSupportCentersX,
                                   out simdFloat3 bEdgeSupportCentersX,
                                   out simdFloat3 bEdgeAltSupportCentersX);
            float4 edgeEdgeXXDistSq = EdgeEdgeDistanceSqsOnAxis(new EdgeEdgeAxisConfiguration
            {
                axisHalfSizeA      = halfSizeA.x,
                axisHalfSizeB      = halfSizeB.x,
                bAxisIndex         = 0,
                crossAxis          = aXCrossBs.a,
                crossAxisCrossADir = aXCrossAXCrossBs.a,
                crossAxisCrossBDir = bCrossAXCrossBs.a
            },
                                                                in bInASpace,
                                                                new simdFloat3(aEdgeSupportCentersX.a, aEdgeAltSupportCentersX.a, bEdgeSupportCentersX.a,
                                                                               bEdgeAltSupportCentersX.a),
                                                                out bool absoluteClosestFoundXX);
            float4 edgeEdgeXYDistSq = EdgeEdgeDistanceSqsOnAxis(new EdgeEdgeAxisConfiguration
            {
                axisHalfSizeA      = halfSizeA.x,
                axisHalfSizeB      = halfSizeB.y,
                bAxisIndex         = 1,
                crossAxis          = aXCrossBs.b,
                crossAxisCrossADir = aXCrossAXCrossBs.b,
                crossAxisCrossBDir = bCrossAXCrossBs.b
            },
                                                                in bInASpace,
                                                                new simdFloat3(aEdgeSupportCentersX.b, aEdgeAltSupportCentersX.b, bEdgeSupportCentersX.b,
                                                                               bEdgeAltSupportCentersX.b),
                                                                out bool absoluteClosestFoundXY);
            float4 edgeEdgeXZDistSq = EdgeEdgeDistanceSqsOnAxis(new EdgeEdgeAxisConfiguration
            {
                axisHalfSizeA      = halfSizeA.x,
                axisHalfSizeB      = halfSizeB.z,
                bAxisIndex         = 2,
                crossAxis          = aXCrossBs.c,
                crossAxisCrossADir = aXCrossAXCrossBs.c,
                crossAxisCrossBDir = bCrossAXCrossBs.c
            },
                                                                in bInASpace,
                                                                new simdFloat3(aEdgeSupportCentersX.c, aEdgeAltSupportCentersX.c, bEdgeSupportCentersX.c,
                                                                               bEdgeAltSupportCentersX.c),
                                                                out bool absoluteClosestFoundXZ);

            // Ay vs Bs
            simdFloat3 aYCrossBs        = simd.upCross(bAxes);
            aYCrossBs                   = simd.chgsign0Positive(aYCrossBs, simd.dot(aYCrossBs, bInASpace.t));
            simdFloat3 aYCrossAYCrossBs = simd.upCross(aYCrossBs);
            simdFloat3 bCrossAYCrossBs  = simd.cross(bAxes, aYCrossBs);
            SelectEdgeEdgeSupports(aYCrossBs,
                                   new bool3(false, true, false),
                                   halfSizeA,
                                   halfSizeB,
                                   in bInASpace,
                                   out simdFloat3 aEdgeSupportCentersY,
                                   out simdFloat3 aEdgeAltSupportCentersY,
                                   out simdFloat3 bEdgeSupportCentersY,
                                   out simdFloat3 bEdgeAltSupportCentersY);
            float4 edgeEdgeYXDistSq = EdgeEdgeDistanceSqsOnAxis(new EdgeEdgeAxisConfiguration
            {
                axisHalfSizeA      = halfSizeA.y,
                axisHalfSizeB      = halfSizeB.x,
                bAxisIndex         = 0,
                crossAxis          = aYCrossBs.a,
                crossAxisCrossADir = aYCrossAYCrossBs.a,
                crossAxisCrossBDir = bCrossAYCrossBs.a
            },
                                                                in bInASpace,
                                                                new simdFloat3(aEdgeSupportCentersY.a, aEdgeAltSupportCentersY.a, bEdgeSupportCentersY.a,
                                                                               bEdgeAltSupportCentersY.a),
                                                                out bool absoluteClosestFoundYX);
            float4 edgeEdgeYYDistSq = EdgeEdgeDistanceSqsOnAxis(new EdgeEdgeAxisConfiguration
            {
                axisHalfSizeA      = halfSizeA.y,
                axisHalfSizeB      = halfSizeB.y,
                bAxisIndex         = 1,
                crossAxis          = aYCrossBs.b,
                crossAxisCrossADir = aYCrossAYCrossBs.b,
                crossAxisCrossBDir = bCrossAYCrossBs.b
            },
                                                                in bInASpace,
                                                                new simdFloat3(aEdgeSupportCentersY.b, aEdgeAltSupportCentersY.b, bEdgeSupportCentersY.b,
                                                                               bEdgeAltSupportCentersY.b),
                                                                out bool absoluteClosestFoundYY);
            float4 edgeEdgeYZDistSq = EdgeEdgeDistanceSqsOnAxis(new EdgeEdgeAxisConfiguration
            {
                axisHalfSizeA      = halfSizeA.y,
                axisHalfSizeB      = halfSizeB.z,
                bAxisIndex         = 2,
                crossAxis          = aYCrossBs.c,
                crossAxisCrossADir = aYCrossAYCrossBs.c,
                crossAxisCrossBDir = bCrossAYCrossBs.c
            },
                                                                in bInASpace,
                                                                new simdFloat3(aEdgeSupportCentersY.c, aEdgeAltSupportCentersY.c, bEdgeSupportCentersY.c,
                                                                               bEdgeAltSupportCentersY.c),
                                                                out bool absoluteClosestFoundYZ);

            // Az vs Bs
            simdFloat3 aZCrossBs        = simd.forwardCross(bAxes);
            aZCrossBs                   = simd.chgsign0Positive(aZCrossBs, simd.dot(aZCrossBs, bInASpace.t));
            simdFloat3 aZCrossAZCrossBs = simd.forwardCross(aZCrossBs);
            simdFloat3 bCrossAZCrossBs  = simd.cross(bAxes, aZCrossBs);
            SelectEdgeEdgeSupports(aZCrossBs,
                                   new bool3(false, false, true),
                                   halfSizeA,
                                   halfSizeB,
                                   in bInASpace,
                                   out simdFloat3 aEdgeSupportCentersZ,
                                   out simdFloat3 aEdgeAltSupportCentersZ,
                                   out simdFloat3 bEdgeSupportCentersZ,
                                   out simdFloat3 bEdgeAltSupportCentersZ);
            float4 edgeEdgeZXDistSq = EdgeEdgeDistanceSqsOnAxis(new EdgeEdgeAxisConfiguration
            {
                axisHalfSizeA      = halfSizeA.z,
                axisHalfSizeB      = halfSizeB.x,
                bAxisIndex         = 0,
                crossAxis          = aZCrossBs.a,
                crossAxisCrossADir = aZCrossAZCrossBs.a,
                crossAxisCrossBDir = bCrossAZCrossBs.a
            },
                                                                in bInASpace,
                                                                new simdFloat3(aEdgeSupportCentersZ.a, aEdgeAltSupportCentersZ.a, bEdgeSupportCentersZ.a,
                                                                               bEdgeAltSupportCentersZ.a),
                                                                out bool absoluteClosestFoundZX);
            float4 edgeEdgeZYDistSq = EdgeEdgeDistanceSqsOnAxis(new EdgeEdgeAxisConfiguration
            {
                axisHalfSizeA      = halfSizeA.z,
                axisHalfSizeB      = halfSizeB.y,
                bAxisIndex         = 1,
                crossAxis          = aZCrossBs.b,
                crossAxisCrossADir = aZCrossAZCrossBs.b,
                crossAxisCrossBDir = bCrossAZCrossBs.b
            },
                                                                in bInASpace,
                                                                new simdFloat3(aEdgeSupportCentersZ.b, aEdgeAltSupportCentersZ.b, bEdgeSupportCentersZ.b,
                                                                               bEdgeAltSupportCentersZ.b),
                                                                out bool absoluteClosestFoundZY);
            float4 edgeEdgeZZDistSq = EdgeEdgeDistanceSqsOnAxis(new EdgeEdgeAxisConfiguration
            {
                axisHalfSizeA      = halfSizeA.z,
                axisHalfSizeB      = halfSizeB.z,
                bAxisIndex         = 2,
                crossAxis          = aZCrossBs.c,
                crossAxisCrossADir = aZCrossAZCrossBs.c,
                crossAxisCrossBDir = bCrossAZCrossBs.c
            },
                                                                in bInASpace,
                                                                new simdFloat3(aEdgeSupportCentersZ.c, aEdgeAltSupportCentersZ.c, bEdgeSupportCentersZ.c,
                                                                               bEdgeAltSupportCentersZ.c),
                                                                out bool absoluteClosestFoundZZ);

            // Reduce edges
            bool4  axbyBeatsX   = edgeEdgeXYDistSq < edgeEdgeXXDistSq;
            float4 bestAxDistSq = math.select(edgeEdgeXXDistSq, edgeEdgeXYDistSq, axbyBeatsX);
            int4   bestAxCode   = math.select(int4.zero, 1, axbyBeatsX);
            bool4  axbzWins     = edgeEdgeXZDistSq < bestAxDistSq;
            bestAxDistSq        = math.select(bestAxDistSq, edgeEdgeXZDistSq, axbzWins);
            bestAxCode          = math.select(bestAxCode, 2, axbzWins);

            bool4  aybyBeatsX    = edgeEdgeYYDistSq < edgeEdgeYXDistSq;
            float4 bestAyDistSq  = math.select(edgeEdgeYXDistSq, edgeEdgeYYDistSq, aybyBeatsX);
            int4   bestAyCode    = math.select(int4.zero, 1, aybyBeatsX);
            bool4  aybzWins      = edgeEdgeYZDistSq < bestAyDistSq;
            bestAyDistSq         = math.select(bestAyDistSq, edgeEdgeYZDistSq, aybzWins);
            bestAyCode           = math.select(bestAyCode, 2, aybzWins);
            bestAyCode          += 4;

            bool4  azbyBeatsX    = edgeEdgeZYDistSq < edgeEdgeZXDistSq;
            float4 bestAzDistSq  = math.select(edgeEdgeZXDistSq, edgeEdgeZYDistSq, azbyBeatsX);
            int4   bestAzCode    = math.select(int4.zero, 1, azbyBeatsX);
            bool4  azbzWins      = edgeEdgeZZDistSq < bestAzDistSq;
            bestAzDistSq         = math.select(bestAzDistSq, edgeEdgeZZDistSq, azbzWins);
            bestAzCode           = math.select(bestAzCode, 2, azbzWins);
            bestAzCode          += 8;

            bool4  ayBeatsX        = bestAyDistSq < bestAxDistSq;
            float4 edgesBestDistSq = math.select(bestAxDistSq, bestAyDistSq, ayBeatsX);
            int4   edgesBestCodes  = math.select(bestAxCode, bestAyCode, ayBeatsX);
            bool4  azWins          = bestAzDistSq < edgesBestDistSq;
            edgesBestDistSq        = math.select(edgesBestDistSq, bestAzDistSq, azWins);
            edgesBestCodes         = math.select(edgesBestCodes, bestAzCode, azWins);

            float bestEdgeDistSq             = math.cmin(edgesBestDistSq);
            bool  absoluteClosestFoundIsEdge = absoluteClosestFoundXX | absoluteClosestFoundXY | absoluteClosestFoundXZ |
                                              absoluteClosestFoundYX | absoluteClosestFoundYY | absoluteClosestFoundYZ |
                                              absoluteClosestFoundZX | absoluteClosestFoundZY | absoluteClosestFoundZZ;

            // Vertices vs boxes unless we know we found the edge already
            float      bestVertexDistSq    = float.MaxValue;
            float4     verticesBestDistSq  = float.MaxValue;
            simdFloat3 verticesClosestA    = default;
            simdFloat3 verticesClosestBInB = default;
            if (!absoluteClosestFoundIsEdge)
            {
                simdFloat3 verticesSigns03 = new simdFloat3(new float3(1f, 1f, 1f), new float3(-1f, 1f, 1f), new float3(1f, -1f, 1f), new float3(-1f, -1f, 1f));
                simdFloat3 verticesSigns47 = verticesSigns03;
                verticesSigns47.z          = -verticesSigns47.z;

                // Test B's vertices against box A
                simdFloat3 bVertices03InB     = simd.chgsign(halfSizeB, verticesSigns03);
                simdFloat3 bVertices47InB     = simd.chgsign(halfSizeB, verticesSigns47);
                simdFloat3 bVertices03        = simd.transform(bInASpace, bVertices03InB);
                simdFloat3 bVertices47        = simd.transform(bInASpace, bVertices47InB);
                simdFloat3 bVertices03Clamped = simd.clamp(bVertices03, -halfSizeA, halfSizeA);
                simdFloat3 bVertices47Clamped = simd.clamp(bVertices47, -halfSizeA, halfSizeA);
                float4     bVerticesDistSq03  = simd.distancesq(bVertices03, bVertices03Clamped);
                float4     bVerticesDistSq47  = simd.distancesq(bVertices47, bVertices47Clamped);
                bool4      bVertices47Wins    = bVerticesDistSq47 < bVerticesDistSq03;
                verticesBestDistSq            = math.select(bVerticesDistSq03, bVerticesDistSq47, bVertices47Wins);
                // Todo: Would it be better to remember vertex indices rather than simd select the actual vertices?
                verticesClosestA    = simd.select(bVertices03Clamped, bVertices47Clamped, bVertices47Wins);
                verticesClosestBInB = simd.select(bVertices03InB, bVertices47InB, bVertices47Wins);

                // Test A's vertices against box B
                simdFloat3 aVertices03           = simd.chgsign(halfSizeA, verticesSigns03);
                simdFloat3 aVertices47           = simd.chgsign(halfSizeA, verticesSigns47);
                simdFloat3 aVertices03InB        = simd.inverseTransformIdentityScale(bInASpace, aVertices03);
                simdFloat3 aVertices47InB        = simd.inverseTransformIdentityScale(bInASpace, aVertices47);
                simdFloat3 aVertices03InBClamped = simd.clamp(aVertices03InB, -halfSizeB, halfSizeB);
                simdFloat3 aVertices47InBClamped = simd.clamp(aVertices47InB, -halfSizeB, halfSizeB);
                float4     aVerticesDistSq03     = simd.distancesq(aVertices03InB, aVertices03InBClamped);
                float4     aVerticesDistSq47     = simd.distancesq(aVertices47InB, aVertices47InBClamped);
                bool4      aVertices03Wins       = aVerticesDistSq03 < verticesBestDistSq;
                verticesBestDistSq               = math.select(verticesBestDistSq, aVerticesDistSq03, aVertices03Wins);
                verticesClosestA                 = simd.select(verticesClosestA, aVertices03, aVertices03Wins);
                verticesClosestBInB              = simd.select(verticesClosestBInB, aVertices03InBClamped, aVertices03Wins);
                bool4 aVertices47Wins            = aVerticesDistSq47 < verticesBestDistSq;
                verticesBestDistSq               = math.select(verticesBestDistSq, aVerticesDistSq47, aVertices47Wins);
                verticesClosestA                 = simd.select(verticesClosestA, aVertices47, aVertices47Wins);
                verticesClosestBInB              = simd.select(verticesClosestBInB, aVertices47InBClamped, aVertices47Wins);
                bestVertexDistSq                 = math.cmin(verticesBestDistSq);
            }

            // Finalize the winner.
            float  distSq;
            float3 closestA, closestBInB;
            float3 normalA, normalBInB;
            ushort featureCodeA, featureCodeB;
            if (bestVertexDistSq <= bestEdgeDistSq)
            {
                int bestLane = math.tzcnt(math.bitmask(bestVertexDistSq == verticesBestDistSq));
                distSq       = bestVertexDistSq;
                closestA     = verticesClosestA[bestLane];
                closestBInB  = verticesClosestBInB[bestLane];
                if (Hint.Unlikely(distSq == 0f))
                {
                    // If distSq is 0, then neither point was moved by clamping, which means one vertex must be inside the other box.
                    float3 depthA = halfSizeA - math.abs(closestA);
                    float3 depthB = halfSizeB - math.abs(closestBInB);
                    if (math.all(depthA > 0f))
                    {
                        int axis       = math.tzcnt(math.bitmask(new bool4(depthA == math.cmin(depthA), false)));
                        closestA[axis] = math.chgsign(halfSizeA[axis], closestA[axis]);
                        float depth    = depthA[axis];
                        distSq         = -depth * depth;
                    }
                    else if (math.all(depthB > 0f))
                    {
                        int axis          = math.tzcnt(math.bitmask(new bool4(depthB == math.cmin(depthB), false)));
                        closestBInB[axis] = math.chgsign(halfSizeB[axis], closestBInB[axis]);
                        float depth       = depthB[axis];
                        distSq            = -depth * depth;
                    }
                }
                featureCodeA = PointRayBox.FeatureCodeAndNormalFromCenterRelativeSurfacePoint(closestA, halfSizeA, out normalA);
                featureCodeB = PointRayBox.FeatureCodeAndNormalFromCenterRelativeSurfacePoint(closestBInB, halfSizeB, out normalBInB);
            }
            else
            {
                int    bestLane            = math.tzcnt(math.bitmask(bestEdgeDistSq == edgesBestDistSq));
                int    crossAxisIndex      = edgesBestCodes[bestLane];
                int    aAxisIndex          = crossAxisIndex / 4;
                int    bAxisIndex          = crossAxisIndex % 4;
                bool3  aAxisMask           = aAxisIndex == new int3(0, 1, 2);
                bool3  bAxisMask           = bAxisIndex == new int3(0, 1, 2);
                float3 aAxis               = math.select(0f, 1f, aAxisMask);
                float3 bAxis               = bInASpace.rs[bAxisIndex];
                float3 crossAxis           = math.cross(aAxis, bAxis);
                float3 crossAxisSupportDir = math.chgsign0Positive(crossAxis, math.dot(crossAxis, bInASpace.t));

                float3 crossAxisInB          = mathex.InverseRotate(bAxes, crossAxisSupportDir);
                float3 aEdgeSupportCenter    = math.select(math.chgsign0Positive(halfSizeA, crossAxisSupportDir), default, aAxisMask);
                float3 bEdgeSupportCenterInB = math.select(math.chgsign0Positive(halfSizeB, -crossAxisInB), default, bAxisMask);
                if ((bestLane & 1) != 0)
                {
                    float3 aAlternateSupportMagnitudes = math.select(math.abs(crossAxisSupportDir), float.MaxValue, aAxisMask);
                    bool   aFlipX                      = aAlternateSupportMagnitudes.x <= aAlternateSupportMagnitudes.y & aAlternateSupportMagnitudes.x <=
                                                       aAlternateSupportMagnitudes.z;
                    bool aFlipY        = !aFlipX & aAlternateSupportMagnitudes.y <= aAlternateSupportMagnitudes.z;
                    bool aFlipZ        = !aFlipX & !aFlipY;
                    aEdgeSupportCenter = math.select(aEdgeSupportCenter, -aEdgeSupportCenter, new bool3(aFlipX, aFlipY, aFlipZ));
                }
                if ((bestLane & 2) != 0)
                {
                    float3 bAlternateSupportMagnitudes = math.select(math.abs(crossAxisInB), float.MaxValue, bAxisMask);
                    bool   bFlipX                      = bAlternateSupportMagnitudes.x <= bAlternateSupportMagnitudes.y & bAlternateSupportMagnitudes.x <=
                                                       bAlternateSupportMagnitudes.z;
                    bool bFlipY           = !bFlipX & bAlternateSupportMagnitudes.y <= bAlternateSupportMagnitudes.z;
                    bool bFlipZ           = !bFlipX & !bFlipY;
                    bEdgeSupportCenterInB = math.select(bEdgeSupportCenterInB, -bEdgeSupportCenterInB, new bool3(bFlipX, bFlipY, bFlipZ));
                }
                float  crossAxisLengthSq = math.lengthsq(crossAxis);
                float3 centerAToCenterB  = math.transform(bInASpace, bEdgeSupportCenterInB) - aEdgeSupportCenter;
                closestA                 = aEdgeSupportCenter;
                closestA[aAxisIndex]     = math.dot(centerAToCenterB, math.cross(bAxis, crossAxis)) / crossAxisLengthSq;
                closestBInB              = bEdgeSupportCenterInB;
                closestBInB[bAxisIndex]  = math.dot(centerAToCenterB, math.cross(aAxis, crossAxis)) / crossAxisLengthSq;
                distSq                   = bestEdgeDistSq;
                float root2over2         = math.SQRT2 / 2f;
                normalA                  = math.select(math.chgsign(root2over2, aEdgeSupportCenter), 0f, aAxisMask);
                normalBInB               = math.select(math.chgsign(root2over2, bEdgeSupportCenterInB), 0f, bAxisMask);
                featureCodeA             = PointRayBox.FeatureCodeFromBoxNormal(normalA);
                featureCodeB             = PointRayBox.FeatureCodeFromBoxNormal(normalBInB);
            }
            result = new ColliderDistanceResultInternal
            {
                hitpointA    = closestA,
                hitpointB    = math.transform(bInASpace, closestBInB),
                normalA      = normalA,
                normalB      = math.mul(bInASpace.rs, normalBInB),
                distance     = math.chgsign(math.sqrt(math.abs(distSq)), distSq),
                featureCodeA = featureCodeA,
                featureCodeB = featureCodeB,
            };
            return result.distance <= maxDistance;
        }

        static void SelectEdgeEdgeSupports(simdFloat3 crossAxes,
                                           bool3 aAxisMask,
                                           float3 halfSizeA,
                                           float3 halfSizeB,
                                           in AffineTransform bInASpace,
                                           out simdFloat3 aEdgeSupportCenters,
                                           out simdFloat3 aAlternateSupportCenters,
                                           out simdFloat3 bEdgeSupportCentersInB,
                                           out simdFloat3 bAlternateSupportCentersInB)
        {
            simdFloat3 crossAxesInB = simd.inverseRotate(bInASpace.rs, crossAxes);
            aEdgeSupportCenters     = simd.select(simd.chgsign0Positive(halfSizeA, crossAxes), default, aAxisMask);
            bEdgeSupportCentersInB  = simd.chgsign0Positive(halfSizeB, -crossAxesInB);
            bEdgeSupportCentersInB  = simd.select(bEdgeSupportCentersInB,
                                                  default,
                                                  new bool4(true, false, false, true),
                                                  new bool4(false, true, false, false),
                                                  new bool4(false, false, true, false));

            simdFloat3 aAlternateSupportMagnitudes = simd.select(simd.abs(crossAxes), new simdFloat3(float.MaxValue), aAxisMask);
            bool4      aFlipX                      = aAlternateSupportMagnitudes.x <= aAlternateSupportMagnitudes.y & aAlternateSupportMagnitudes.x <=
                                                aAlternateSupportMagnitudes.z;
            bool4 aFlipY             = !aFlipX & aAlternateSupportMagnitudes.y <= aAlternateSupportMagnitudes.z;
            bool4 aFlipZ             = !aFlipX & !aFlipY;
            aAlternateSupportCenters = simd.select(aEdgeSupportCenters, -aEdgeSupportCenters, aFlipX, aFlipY, aFlipZ);

            simdFloat3 bAlternateSupportMagnitudes =
                simd.select(simd.abs(crossAxesInB), new simdFloat3(float.MaxValue), new bool4(true, false, false, true), new bool4(false, true, false, false),
                            new bool4(false, false, true, false));
            bool4 bFlipX = bAlternateSupportMagnitudes.x <= bAlternateSupportMagnitudes.y & bAlternateSupportMagnitudes.x <=
                           bAlternateSupportMagnitudes.z;
            bool4 bFlipY                = !bFlipX & bAlternateSupportMagnitudes.y <= bAlternateSupportMagnitudes.z;
            bool4 bFlipZ                = !bFlipX & !bFlipY;
            bAlternateSupportCentersInB = simd.select(bEdgeSupportCentersInB, -bEdgeSupportCentersInB, bFlipX, bFlipY, bFlipZ);
        }

        struct EdgeEdgeAxisConfiguration
        {
            public float3 crossAxis;
            public float3 crossAxisCrossBDir;  // Cross operand order does not matter
            public float3 crossAxisCrossADir;  // Cross operand order does not matter
            public float  axisHalfSizeA;
            public float  axisHalfSizeB;
            public int    bAxisIndex;
        }

        static float4 EdgeEdgeDistanceSqsOnAxis(in EdgeEdgeAxisConfiguration config, in AffineTransform bInASpace, simdFloat3 edgeCenters, out bool absoluteClosestFound)
        {
            simdFloat3 aEdgeCenters     = edgeCenters.abab;
            simdFloat3 bEdgeCentersInB  = edgeCenters.ccdd;
            simdFloat3 bEdgeCenters     = simd.transform(bInASpace, bEdgeCentersInB);
            simdFloat3 centerAToCenterB = bEdgeCenters - aEdgeCenters;
            float3     bEdgeDirection   = bInASpace.rs[config.bAxisIndex];

            // closestA = centerA + distanceAlongA * edgeDirectionA
            // closestB = centerB + distanceAlongB * edgeDirectionB
            // Traveling from centerA to centerB can be imagined in three segments: centerA to closestA, closestA to closestB, and closestB to centerB.
            // Therefore centerAToCenterB = distanceAlongA * edgeDirectionA + closestAToClosestB - distanceAlongB * edgeDirectionB
            // closestAToClosestB occurs on the crossAxis direction. It's squared magnitude is what we are after.
            // But we also want to know if distanceAlongA and distanceAlongB stay within their respective edges.
            // edgeDirectionB and crossAxis lie in the same plane as the latter two travel segments. The first travel segment must also reach this plane.
            // We find the distance from the center to this plane from centerA. Same for B.
            float4 distanceAlongAScaled = simd.dot(centerAToCenterB, config.crossAxisCrossBDir);
            float4 distanceAlongBScaled = simd.dot(centerAToCenterB, config.crossAxisCrossADir);
            float4 separation           = simd.dot(centerAToCenterB, config.crossAxis);
            // Because B's edge direction is unit length and perpendicular to crossAxis, the magnitude of crossAxisCrossBDir matches the magnitude of crossAxis.
            // Because both planeNormal and A's edge direction are perpendicular to crossAxis, A's edge direction is composed by some factor along the B's edge direction,
            // and some factor along the planeNormal. The angle along the planeNormal is sin(angleBetweenEdgeDirections).
            // The magnitude of crossAxis is also sin(angleBetweenEdgeDirections). Therefore the magnitude of the projection of A's edge along the planeNormal is
            // the magnitude of crossAxis.
            // The scale of the dot product is the magnitude of A's edge projected onto the scaled planeNormal, where the plane normal is scaled by the magnitude of
            // crossAxisCrossBDir. That is, scale = dot(aEdgeDir, planeNormal) * length(crossAxisCrossBDir) = math.length(crossAxis) * math.length(crossAxis).
            // This derivation also works with A's and B's edge roles swapped.
            // Another way to think about it is that the scale is dot(aEdgeDir, crossAxisCrossBDir) = dot(aEdgeDir, cross(crossAxis, bEdgeDir)) which via triple
            // product rule equals dot(crossAxis, cross(aEdgeDir, bEdgeDir)) where the cross product has a known magnitude of being the dot product.
            float4 scale = math.lengthsq(config.crossAxis);
            // With the scale, we can compare against the actual edge lengths
            bool4 isNotEndpoint  = math.abs(distanceAlongAScaled) < config.axisHalfSizeA * scale;
            isNotEndpoint       &= math.abs(distanceAlongBScaled) < config.axisHalfSizeB * scale;
            // If the closest points are not endpoints, and there's a positive separation axis, then the edge pair is guaranteed the closest, and closer than any vertex-box pair.
            // This only applies to the primary supports.
            absoluteClosestFound = isNotEndpoint.x & (separation.x > 0f);
            float4 distanceSq    = separation * separation / scale;
            return math.select(float.MaxValue, distanceSq, isNotEndpoint);
        }

#if LATIOS_PSYSHOCK_REFERENCE
        // This custom algorithm is really weird, but is faster than GJK+EPA. It is a mix of SAT and Lin-Canny.
        // The first step is to perform SAT to determine if the boxes are intersecting or not. If they are, and
        // the minimum axis is along the face normal, then we try to find a penetrating vertex that projects onto
        // the face. Otherwise, we find an edge-edge penetration.
        // If SAT determines an outside hit, we use a Lin-Canny feature pair test with a couple massive shortcuts.
        // For any vertex on one box, we can find the closest point on the other box (and thus the closest feature)
        // simply by transforming the vertex into the other box's local space, and then clamping it to the box volume.
        // For edge-edge tests, we know the closest points lie on the closest edges along the separating axis.
        private static bool BoxBoxDistanceReference(float3 halfSizeA,
                                                    float3 halfSizeB,
                                                    in RigidTransform bInASpace,
                                                    in RigidTransform aInBSpace,
                                                    float maxDistance,
                                                    out ColliderDistanceResultInternal result)
        {
            var aFaceBPointDistances = FacePointAxesDistances(in halfSizeA, in halfSizeB, in bInASpace);
            var aPointBFaceDistances = FacePointAxesDistances(in halfSizeB, in halfSizeA, in aInBSpace);
            var alignedMaxDistance   = math.cmax(math.max(aFaceBPointDistances, aPointBFaceDistances));
            if (alignedMaxDistance > maxDistance)
            {
                result = default;
                return false;
            }

            var bInARotMat = new float3x3(bInASpace.rot);
            var bAxes      = new simdFloat3(bInARotMat.c0, bInARotMat.c1, bInARotMat.c2, bInARotMat.c2);
            var axCrossB   = new simdFloat3(0f, -bAxes.z, bAxes.y);
            var ayCrossB   = new simdFloat3(bAxes.z, 0f, -bAxes.x);
            var azCrossB   = new simdFloat3(-bAxes.y, bAxes.x, 0f);

            var normalizedAxCrossB = simd.normalizesafe(axCrossB, default);
            var normalizedAyCrossB = simd.normalizesafe(ayCrossB, default);
            var normalizedAzCrossB = simd.normalizesafe(azCrossB, default);
            // If the edges are parallel, we cannot have an edge-edge feature pair. Only a point-edge or edge-face.
            var axMasks = (normalizedAxCrossB.x != 0f) | (normalizedAxCrossB.y != 0f) | (normalizedAxCrossB.z != 0f);
            var ayMasks = (normalizedAyCrossB.x != 0f) | (normalizedAyCrossB.y != 0f) | (normalizedAyCrossB.z != 0f);
            var azMasks = (normalizedAzCrossB.x != 0f) | (normalizedAzCrossB.y != 0f) | (normalizedAzCrossB.z != 0f);

            // This SAT algorithm is borrowed from Unity Physics BoxBox Manifold algorithm, except full simdFloat3-ified.
            var supportA = new simdFloat3(math.chgsign(halfSizeA.x, normalizedAxCrossB.x),
                                          math.chgsign(halfSizeA.y, normalizedAxCrossB.y),
                                          math.chgsign(halfSizeA.z, normalizedAxCrossB.z));
            var maxA                = math.abs(simd.dot(supportA, normalizedAxCrossB));
            var minA                = -maxA;
            var axisInB             = simd.mul(aInBSpace.rot, normalizedAxCrossB);
            var supportBinB         = new simdFloat3(math.chgsign(halfSizeB.x, axisInB.x), math.chgsign(halfSizeB.y, axisInB.y), math.chgsign(halfSizeB.z, axisInB.z));
            var supportB            = simd.mul(bInASpace.rot, supportBinB);
            var offsetB             = math.abs(simd.dot(supportB, normalizedAxCrossB));
            var centerB             = simd.dot(bInASpace.pos, normalizedAxCrossB);
            var maxB                = centerB + offsetB;
            var minB                = centerB - offsetB;
            var axPositiveDistances = minB - maxA;
            var axNegativeDistances = minA - maxB;
            var axDistances         = math.select(float.MinValue, math.max(axPositiveDistances, axNegativeDistances), axMasks);
            normalizedAxCrossB = simd.select(normalizedAxCrossB, -normalizedAxCrossB, axPositiveDistances < axNegativeDistances);

            supportA = new simdFloat3(math.chgsign(halfSizeA.x, normalizedAyCrossB.x),
                                      math.chgsign(halfSizeA.y, normalizedAyCrossB.y),
                                      math.chgsign(halfSizeA.z, normalizedAyCrossB.z));
            maxA        = math.abs(simd.dot(supportA, normalizedAyCrossB));
            minA        = -maxA;
            axisInB     = simd.mul(aInBSpace.rot, normalizedAyCrossB);
            supportBinB = new simdFloat3(math.chgsign(halfSizeB.x, axisInB.x), math.chgsign(halfSizeB.y, axisInB.y), math.chgsign(halfSizeB.z, axisInB.z));
            supportB    = simd.mul(bInASpace.rot, supportBinB);
            offsetB     = math.abs(simd.dot(supportB, normalizedAyCrossB));
            centerB     = simd.dot(bInASpace.pos, normalizedAyCrossB);
            maxB        = centerB + offsetB;
            minB        = centerB - offsetB;
            var ayPositiveDistances = minB - maxA;
            var ayNegativeDistances = minA - maxB;
            var ayDistances         = math.select(float.MinValue, math.max(ayPositiveDistances, ayNegativeDistances), ayMasks);
            normalizedAyCrossB = simd.select(normalizedAyCrossB, -normalizedAyCrossB, ayPositiveDistances < ayNegativeDistances);

            supportA = new simdFloat3(math.chgsign(halfSizeA.x, normalizedAzCrossB.x),
                                      math.chgsign(halfSizeA.y, normalizedAzCrossB.y),
                                      math.chgsign(halfSizeA.z, normalizedAzCrossB.z));
            maxA        = math.abs(simd.dot(supportA, normalizedAzCrossB));
            minA        = -maxA;
            axisInB     = simd.mul(aInBSpace.rot, normalizedAzCrossB);
            supportBinB = new simdFloat3(math.chgsign(halfSizeB.x, axisInB.x), math.chgsign(halfSizeB.y, axisInB.y), math.chgsign(halfSizeB.z, axisInB.z));
            supportB    = simd.mul(bInASpace.rot, supportBinB);
            offsetB     = math.abs(simd.dot(supportB, normalizedAzCrossB));
            centerB     = simd.dot(bInASpace.pos, normalizedAzCrossB);
            maxB        = centerB + offsetB;
            minB        = centerB - offsetB;
            var azPositiveDistances = minB - maxA;
            var azNegativeDistances = minA - maxB;
            var azDistances         = math.select(float.MinValue, math.max(azPositiveDistances, azNegativeDistances), azMasks);
            normalizedAzCrossB = simd.select(normalizedAzCrossB, -normalizedAzCrossB, azPositiveDistances < azNegativeDistances);

            var edgeMaxDistance    = math.cmax(math.max(math.max(axDistances, ayDistances), azDistances));
            var overallMaxDistance = math.max(alignedMaxDistance, edgeMaxDistance);
            if (overallMaxDistance > maxDistance)
            {
                result = default;
                return false;
            }

            var edgeAxisIndexBatch = math.select(int.MaxValue, new int4(0, 1, 2, 2), axDistances == edgeMaxDistance);
            edgeAxisIndexBatch = math.min(edgeAxisIndexBatch, math.select(int.MaxValue, new int4(3, 4, 5, 5), ayDistances == edgeMaxDistance));
            edgeAxisIndexBatch = math.min(edgeAxisIndexBatch, math.select(int.MaxValue, new int4(6, 7, 8, 8), azDistances == edgeMaxDistance));
            var edgeAxisIndex = math.cmin(edgeAxisIndexBatch);

            if (overallMaxDistance <= 0f)
            {
                if (alignedMaxDistance >= edgeMaxDistance)
                {
                    // We have penetration, and it is between a point and a face.
                    var aFaceMask = alignedMaxDistance == aFaceBPointDistances;
                    if (math.any(aFaceMask))
                    {
                        // There's a vertex on B closest to face A.
                        // If multiple axes match, prioritize y, then z over x.
                        aFaceMask.xz &= !aFaceMask.y;
                        aFaceMask.x  &= !aFaceMask.z;
                        var aFaceNormal    = math.select(0f, math.chgsign(1f, bInASpace.pos), aFaceMask);
                        var aFaceNormalInB = math.rotate(aInBSpace.rot, aFaceNormal);
                        // Oppose the normal on each axis, or if zero, pick the sign towards A's center
                        var signs        = math.select(-aFaceNormalInB, aInBSpace.pos, aFaceNormalInB == 0f);
                        var faceSupportB = math.transform(bInASpace, math.chgsign(halfSizeB, signs));

                        // It is possible that A is fully inside B. If so, we want the bFace result instead. In such a case,
                        // the following clamp check will fail. Due to floating point precision though, we need a little bit
                        // of margin, so we use the difference of the max distance between aFaceBPoint and aPointBFace as a
                        // margin.
                        var clampedSupport = math.clamp(faceSupportB, -halfSizeA, halfSizeA);
                        var margin         = math.distance(math.cmax(aFaceBPointDistances), math.cmax(aPointBFaceDistances));
                        if (math.all(aFaceMask | (math.abs(faceSupportB - clampedSupport) <= margin)))
                        {
                            ushort featureCodeA = (ushort)(0x8000 | (math.tzcnt(math.bitmask(new bool4(aFaceMask, false))) + math.select(0, 3, math.any(aFaceNormal < -0.5f))));
                            ushort featureCodeB = (ushort)math.bitmask(new bool4(signs < 0f, false));
                            result = new ColliderDistanceResultInternal
                            {
                                distance     = alignedMaxDistance,
                                hitpointA    = math.select(faceSupportB, halfSizeA * aFaceNormal, aFaceMask),
                                hitpointB    = faceSupportB,
                                normalA      = aFaceNormal,
                                normalB      = math.rotate(bInASpace.rot, PointRayBox.BoxNormalFromFeatureCode(featureCodeB)),
                                featureCodeA = featureCodeA,
                                featureCodeB = featureCodeB
                            };
                            return true;
                        }
                    }
                    var bFaceMask = alignedMaxDistance == aPointBFaceDistances;
                    // Catch the edge case where A was inside B but floating point imprecision caused the aFace axis to be larger.
                    bool bFaceValid = math.any(bFaceMask);
                    if (!bFaceValid)
                    {
                        bFaceMask  = math.abs(alignedMaxDistance - aPointBFaceDistances) <= 1e-5f;
                        bFaceValid = math.any(bFaceMask);
                    }
                    if (!bFaceValid)
                    {
                        // If edges are better, we should fall back to that.
                        bool bFaceBetterThanEdges = math.cmax(aPointBFaceDistances) <= edgeMaxDistance;
                        if (bFaceBetterThanEdges)
                        {
                            // Precision is bad. Just commit to this.
                            bFaceMask  = aPointBFaceDistances == math.cmax(aPointBFaceDistances);
                            bFaceValid = true;
                        }
                    }
                    if (bFaceValid)
                    {
                        // There's a vertex on A closet to face B.
                        // If multiple axes match, prioritize y, then z over x.
                        bFaceMask.xz &= !bFaceMask.y;
                        bFaceMask.x  &= !bFaceMask.z;
                        var bFaceNormal    = math.select(0f, math.chgsign(1f, aInBSpace.pos), bFaceMask);
                        var bFaceNormalInA = math.rotate(bInASpace.rot, bFaceNormal);
                        // Oppose the normal on each axis or if zero, pick the sign towards B's center
                        var signs        = math.select(-bFaceNormalInA, bInASpace.pos, bFaceNormalInA == 0f);
                        var faceSupportA = math.chgsign(halfSizeA, signs);

                        ushort featureCodeA = (ushort)math.bitmask(new bool4(signs < 0f, false));
                        ushort featureCodeB = (ushort)(0x8000 | (math.tzcnt(math.bitmask(new bool4(bFaceMask, false))) + math.select(0, 3, math.any(bFaceNormal < -0.5f))));
                        result = new ColliderDistanceResultInternal
                        {
                            distance     = alignedMaxDistance,
                            hitpointA    = faceSupportA,
                            hitpointB    = math.transform(bInASpace, math.select(math.transform(aInBSpace, faceSupportA), halfSizeB * bFaceNormal, bFaceMask)),
                            normalA      = PointRayBox.BoxNormalFromFeatureCode(featureCodeA),
                            normalB      = bFaceNormalInA,
                            featureCodeA = featureCodeA,
                            featureCodeB = featureCodeB
                        };
                        return result.distance <= maxDistance;
                    }
                }

                // We have penetration, and it is between two edges.
                float3 axis  = default;
                bool3 maskA = default;
                bool3 maskB = default;
                switch (edgeAxisIndex)
                {
                    case 0:
                        axis  = normalizedAxCrossB.a;
                        maskA = new bool3(true, false, false);
                        maskB = new bool3(true, false, false);
                        break;
                    case 1:
                        axis  = normalizedAxCrossB.b;
                        maskA = new bool3(true, false, false);
                        maskB = new bool3(false, true, false);
                        break;
                    case 2:
                        axis  = normalizedAxCrossB.c;
                        maskA = new bool3(true, false, false);
                        maskB = new bool3(false, false, true);
                        break;
                    case 3:
                        axis  = normalizedAyCrossB.a;
                        maskA = new bool3(false, true, false);
                        maskB = new bool3(true, false, false);
                        break;
                    case 4:
                        axis  = normalizedAyCrossB.b;
                        maskA = new bool3(false, true, false);
                        maskB = new bool3(false, true, false);
                        break;
                    case 5:
                        axis  = normalizedAyCrossB.c;
                        maskA = new bool3(false, true, false);
                        maskB = new bool3(false, false, true);
                        break;
                    case 6:
                        axis  = normalizedAzCrossB.a;
                        maskA = new bool3(false, false, true);
                        maskB = new bool3(true, false, false);
                        break;
                    case 7:
                        axis  = normalizedAzCrossB.b;
                        maskA = new bool3(false, false, true);
                        maskB = new bool3(false, true, false);
                        break;
                    case 8:
                        axis  = normalizedAzCrossB.c;
                        maskA = new bool3(false, false, true);
                        maskB = new bool3(false, false, true);
                        break;
                }

                // first letter = box, second letter = p is point, e is edge
                var aSigns       = math.select(axis, 1f, maskA);
                var aSupportP    = math.chgsign(halfSizeA, aSigns);
                var aSupportE    = math.select(0f, -2f, maskA);
                var edgeAxisInB  = math.rotate(aInBSpace.rot, axis);
                var bSigns       = math.select(-edgeAxisInB, 1f, maskB);
                var bSupportPinB = math.chgsign(halfSizeB, bSigns);
                var bSupportEinB = math.select(0f, -2f, maskB);
                var bSupportP    = math.transform(bInASpace, bSupportPinB);
                var bSupportE    = math.rotate(bInASpace, bSupportEinB);
                CapsuleCapsule.SegmentSegmentOld(aSupportP, aSupportE, bSupportP, bSupportE, out var closestA, out var closestB, out _);
                var closestAinB = math.transform(aInBSpace, closestA);
                // The two points should be on or inside each other's boxes, or else we picked up the wrong edges
                // (edges of parallel faces could have multiple valid support points)
                bool valid = math.clamp(closestAinB, -halfSizeB, halfSizeB).Equals(closestAinB);
                valid &= math.clamp(closestB, -halfSizeA, halfSizeA).Equals(closestB);
                if (!valid)
                {
                    // Look for the ordinate of the axis closest to zero. Flipping that should give us the next best support.
                    var absAxis            = math.abs(axis);
                    var aFlipMask          = absAxis == math.cmin(absAxis);
                    var aAlternateSupportP = math.select(aSupportP, -aSupportP, aFlipMask);

                    var absAxisInB         = math.abs(edgeAxisInB);
                    var bFlipMask          = absAxisInB == math.cmin(absAxisInB);
                    var bAlternateSupportP = math.transform(bInASpace, math.select(bSupportPinB, -bSupportPinB, bFlipMask));

                    var aStarts = new simdFloat3(aSupportP, aSupportP, aAlternateSupportP, aAlternateSupportP);
                    var bStarts = new simdFloat3(bSupportP, bAlternateSupportP, bSupportP, bAlternateSupportP);
                    CapsuleCapsule.SegmentSegmentOld(in aStarts, new simdFloat3(aSupportE), in bStarts, new simdFloat3(bSupportE), out var closestAs, out var closestBs);
                    var closestAsInB       = simd.transform(aInBSpace, closestAs);
                    var clampedAs          = simd.clamp(closestAsInB, -halfSizeB, halfSizeB);
                    var clampedBs          = simd.clamp(closestBs, -halfSizeA, halfSizeA);
                    var clampedDistortions = simd.distance(closestAsInB, clampedAs) + simd.distance(closestBs, clampedBs);
                    var bestPairIndex      = math.tzcnt(math.bitmask(clampedDistortions == math.cmin(clampedDistortions)));
                    if ((bestPairIndex & 2) == 2)
                        aSigns = math.select(aSigns, -aSigns, aFlipMask);
                    if ((bestPairIndex & 1) == 1)
                        bSigns = math.select(bSigns, -bSigns, bFlipMask);
                    closestA = closestAs[bestPairIndex];
                    closestB = closestBs[bestPairIndex];
                }

                var normalA    = math.select(math.chgsign(1f / math.sqrt(2f), aSigns), 0f, maskA);
                var normalBinB = math.select(math.chgsign(1f / math.sqrt(2f), bSigns), 0f, maskB);
                result = new ColliderDistanceResultInternal
                {
                    distance     = edgeMaxDistance,
                    hitpointA    = closestA,
                    hitpointB    = closestB,
                    normalA      = normalA,
                    normalB      = math.rotate(bInASpace.rot, normalBinB),
                    featureCodeA = PointRayBox.FeatureCodeFromBoxNormal(normalA),
                    featureCodeB = PointRayBox.FeatureCodeFromBoxNormal(normalBinB)
                };
                return result.distance <= maxDistance;
            }

            // The boxes are not penetrating. If edges have the greater separating axis, test those.
            // Otherwise, it can be guaranteed we do not have an edge-edge pair.
            bool foundClosestEdge = false;
            result = default;
            //if (edgeMaxDistance + 1e-4f >= alignedMaxDistance)
            if (edgeMaxDistance > alignedMaxDistance)
            {
                float3 axis  = default;
                bool3 maskA = default;
                bool3 maskB = default;
                switch (edgeAxisIndex)
                {
                    case 0:
                        axis  = normalizedAxCrossB.a;
                        maskA = new bool3(true, false, false);
                        maskB = new bool3(true, false, false);
                        break;
                    case 1:
                        axis  = normalizedAxCrossB.b;
                        maskA = new bool3(true, false, false);
                        maskB = new bool3(false, true, false);
                        break;
                    case 2:
                        axis  = normalizedAxCrossB.c;
                        maskA = new bool3(true, false, false);
                        maskB = new bool3(false, false, true);
                        break;
                    case 3:
                        axis  = normalizedAyCrossB.a;
                        maskA = new bool3(false, true, false);
                        maskB = new bool3(true, false, false);
                        break;
                    case 4:
                        axis  = normalizedAyCrossB.b;
                        maskA = new bool3(false, true, false);
                        maskB = new bool3(false, true, false);
                        break;
                    case 5:
                        axis  = normalizedAyCrossB.c;
                        maskA = new bool3(false, true, false);
                        maskB = new bool3(false, false, true);
                        break;
                    case 6:
                        axis  = normalizedAzCrossB.a;
                        maskA = new bool3(false, false, true);
                        maskB = new bool3(true, false, false);
                        break;
                    case 7:
                        axis  = normalizedAzCrossB.b;
                        maskA = new bool3(false, false, true);
                        maskB = new bool3(false, true, false);
                        break;
                    case 8:
                        axis  = normalizedAzCrossB.c;
                        maskA = new bool3(false, false, true);
                        maskB = new bool3(false, false, true);
                        break;
                }
                var aSigns       = math.select(axis, 1f, maskA);
                var aSupportP    = math.chgsign(halfSizeA, aSigns);
                var aSupportE    = math.select(0f, -2f * halfSizeA, maskA);
                var edgeAxisInB  = math.rotate(aInBSpace.rot, axis);
                var bSigns       = math.select(-edgeAxisInB, 1f, maskB);
                var bSupportPinB = math.chgsign(halfSizeB, bSigns);
                var bSupportEinB = math.select(0f, -2f * halfSizeB, maskB);
                var bSupportP    = math.transform(bInASpace, bSupportPinB);
                var bSupportE    = math.rotate(bInASpace, bSupportEinB);

                // Look for the ordinate of the axis closest to zero. Flipping that should give us the next best support.
                var absAxis            = math.abs(axis);
                var aFlipMask          = absAxis == math.cmin(math.select(absAxis, float.MaxValue, maskA));
                var aAlternateSupportP = math.select(aSupportP, -aSupportP, aFlipMask);

                var absAxisInB         = math.abs(edgeAxisInB);
                var bFlipMask          = absAxisInB == math.cmin(math.select(absAxisInB, float.MaxValue, maskB));
                var bAlternateSupportP = math.transform(bInASpace, math.select(bSupportPinB, -bSupportPinB, bFlipMask));

                var aStarts = new simdFloat3(aSupportP, aSupportP, aAlternateSupportP, aAlternateSupportP);
                var bStarts = new simdFloat3(bSupportP, bAlternateSupportP, bSupportP, bAlternateSupportP);
                var valid   = CapsuleCapsule.SegmentSegmentInvalidateEndpointsPointEdge(aStarts,
                                                                                        new simdFloat3(aSupportE),
                                                                                        bStarts,
                                                                                        new simdFloat3(bSupportE),
                                                                                        out var closestAs,
                                                                                        out var closestBs);
                if (math.any(valid))
                {
                    var distSqs       = math.select(float.MaxValue, simd.distancesq(closestAs, closestBs), valid);
                    var bestPairIndex = math.tzcnt(math.bitmask(distSqs == math.cmin(distSqs)));
                    if ((bestPairIndex & 2) == 2)
                        aSigns = math.select(aSigns, -aSigns, aFlipMask);
                    if ((bestPairIndex & 1) == 1)
                        bSigns = math.select(bSigns, -bSigns, bFlipMask);
                    var closestA   = closestAs[bestPairIndex];
                    var closestB   = closestBs[bestPairIndex];
                    var normalA    = math.select(math.chgsign(1f / math.sqrt(2f), aSigns), 0f, maskA);
                    var normalBinB = math.select(math.chgsign(1f / math.sqrt(2f), bSigns), 0f, maskB);
                    result = new ColliderDistanceResultInternal
                    {
                        distance     = math.sqrt(distSqs[bestPairIndex]),
                        hitpointA    = closestA,
                        hitpointB    = closestB,
                        normalA      = normalA,
                        normalB      = math.rotate(bInASpace.rot, normalBinB),
                        featureCodeA = PointRayBox.FeatureCodeFromBoxNormal(normalA),
                        featureCodeB = PointRayBox.FeatureCodeFromBoxNormal(normalBinB)
                    };
                    if (alignedMaxDistance < 0f || result.distance < alignedMaxDistance)
                    {
                        return result.distance <= maxDistance;
                    }
                    foundClosestEdge = true;
                }
            }

            // Test each point on each box against the points, edges, and planes of the other box using SIMD clamped distances.
            var aPoints03        = new simdFloat3(new float4(halfSizeA.x, -halfSizeA.x, halfSizeA.x, -halfSizeA.x), new float4(halfSizeA.yy, -halfSizeA.yy), halfSizeA.z);
            var aPoints47        = new simdFloat3(aPoints03.x, aPoints03.y, -halfSizeA.z);
            var aPoints03inB     = simd.transform(aInBSpace, aPoints03);
            var aPoints47inB     = simd.transform(aInBSpace, aPoints47);
            var aPoints03Clamped = simd.clamp(aPoints03inB, -halfSizeB, halfSizeB);
            var aPoints47Clamped = simd.clamp(aPoints47inB, -halfSizeB, halfSizeB);
            var aDistSqs03       = simd.distancesq(aPoints03inB, aPoints03Clamped);
            var aDistSqs47       = simd.distancesq(aPoints47inB, aPoints47Clamped);

            var bPoints03        = new simdFloat3(new float4(halfSizeB.x, -halfSizeB.x, halfSizeB.x, -halfSizeB.x), new float4(halfSizeB.yy, -halfSizeB.yy), halfSizeB.z);
            var bPoints47        = new simdFloat3(bPoints03.x, bPoints03.y, -halfSizeB.z);
            var bPoints03inA     = simd.transform(bInASpace, bPoints03);
            var bPoints47inA     = simd.transform(bInASpace, bPoints47);
            var bPoints03Clamped = simd.clamp(bPoints03inA, -halfSizeA, halfSizeA);
            var bPoints47Clamped = simd.clamp(bPoints47inA, -halfSizeA, halfSizeA);
            var bDistSqs03       = simd.distancesq(bPoints03inA, bPoints03Clamped);
            var bDistSqs47       = simd.distancesq(bPoints47inA, bPoints47Clamped);

            var a47Better = aDistSqs47 < aDistSqs03;
            var bestAs    = math.min(aDistSqs03, aDistSqs47);
            var b47Better = bDistSqs47 < bDistSqs03;
            var bestBs    = math.min(bDistSqs03, bDistSqs47);
            var asBetter  = bestAs < bestBs;
            var bests     = math.min(bestAs, bestBs);
            var best      = math.cmin(bests);
            if (foundClosestEdge && best > result.distance * result.distance)
            {
                return result.distance <= maxDistance;
            }
            if (best > maxDistance * maxDistance)
            {
                return false;
            }
            var bestIndex = math.tzcnt(math.bitmask(best == bests));
            var bestId    = bestIndex + math.select(0, 4, math.select(b47Better, a47Better, asBetter)[bestIndex]);  // math.select(8, 0, asBetter[bestIndex]);
            if (asBetter[bestIndex])
            {
                float3 hitB = default;
                switch (bestId)
                {
                    case 0:
                        hitB             = aPoints03Clamped.a;
                        result.distance  = aDistSqs03.x;
                        result.hitpointA = aPoints03.a;
                        break;
                    case 1:
                        hitB             = aPoints03Clamped.b;
                        result.distance  = aDistSqs03.y;
                        result.hitpointA = aPoints03.b;
                        break;
                    case 2:
                        hitB             = aPoints03Clamped.c;
                        result.distance  = aDistSqs03.z;
                        result.hitpointA = aPoints03.c;
                        break;
                    case 3:
                        hitB             = aPoints03Clamped.d;
                        result.distance  = aDistSqs03.w;
                        result.hitpointA = aPoints03.d;
                        break;
                    case 4:
                        hitB             = aPoints47Clamped.a;
                        result.distance  = aDistSqs47.x;
                        result.hitpointA = aPoints47.a;
                        break;
                    case 5:
                        hitB             = aPoints47Clamped.b;
                        result.distance  = aDistSqs47.y;
                        result.hitpointA = aPoints47.b;
                        break;
                    case 6:
                        hitB             = aPoints47Clamped.c;
                        result.distance  = aDistSqs47.z;
                        result.hitpointA = aPoints47.c;
                        break;
                    case 7:
                        hitB             = aPoints47Clamped.d;
                        result.distance  = aDistSqs47.w;
                        result.hitpointA = aPoints47.d;
                        break;
                }
                result.distance = math.sqrt(result.distance);
                var hitBInsideBox = halfSizeB - math.abs(hitB);
                // Sometimes due to floating point error, we actually have overlap here. So we need to push the hitpoint out to the surface.
                // First, try to correct very tiny floating point errors, so that we catch edges.
                var isVeryTinyError = hitBInsideBox < 1e-5f;
                hitB          = math.select(hitB, math.chgsign(halfSizeB, hitB), isVeryTinyError);
                hitBInsideBox = halfSizeB - math.abs(hitB);
                if (math.all(hitBInsideBox > 0f))
                {
                    // The error is a little bigger. Pick the closest axis and push out to the face.
                    var boostAxis   = math.tzcnt(math.bitmask(new bool4(hitBInsideBox == math.cmin(hitBInsideBox), false)));
                    var boostAmount = hitBInsideBox[boostAxis];
                    result.distance -= boostAmount;
                    hitB[boostAxis]  = math.chgsign(halfSizeB[boostAxis], hitB[boostAxis]);
                }
                result.hitpointB    = math.transform(bInASpace, hitB);
                result.featureCodeA = (ushort)bestId;
                result.normalA      = math.normalize(math.select(1f, -1f, (bestId & new int3(1, 2, 4)) != 0));
                result.normalB      = math.normalize(math.select(0f, math.chgsign(1f, hitB), hitB == math.chgsign(halfSizeB, hitB)));
                result.featureCodeB = PointRayBox.FeatureCodeFromBoxNormal(result.normalB);
                result.normalB      = math.rotate(bInASpace.rot, result.normalB);
            }
            else
            {
                float3 hitA = default;
                switch (bestId)
                {
                    case 0:
                        hitA             = bPoints03Clamped.a;
                        result.distance  = bDistSqs03.x;
                        result.hitpointB = bPoints03inA.a;
                        break;
                    case 1:
                        hitA             = bPoints03Clamped.b;
                        result.distance  = bDistSqs03.y;
                        result.hitpointB = bPoints03inA.b;
                        break;
                    case 2:
                        hitA             = bPoints03Clamped.c;
                        result.distance  = bDistSqs03.z;
                        result.hitpointB = bPoints03inA.c;
                        break;
                    case 3:
                        hitA             = bPoints03Clamped.d;
                        result.distance  = bDistSqs03.w;
                        result.hitpointB = bPoints03inA.d;
                        break;
                    case 4:
                        hitA             = bPoints47Clamped.a;
                        result.distance  = bDistSqs47.x;
                        result.hitpointB = bPoints47inA.a;
                        break;
                    case 5:
                        hitA             = bPoints47Clamped.b;
                        result.distance  = bDistSqs47.y;
                        result.hitpointB = bPoints47inA.b;
                        break;
                    case 6:
                        hitA             = bPoints47Clamped.c;
                        result.distance  = bDistSqs47.z;
                        result.hitpointB = bPoints47inA.c;
                        break;
                    case 7:
                        hitA             = bPoints47Clamped.d;
                        result.distance  = bDistSqs47.w;
                        result.hitpointB = bPoints47inA.d;
                        break;
                }
                result.distance = math.sqrt(result.distance);
                var hitAInsideBox = halfSizeA - math.abs(hitA);
                // Sometimes due to floating point error, we actually have overlap here. So we need to push the hitpoint out to the surface.
                // First, try to correct very tiny floating point errors, so that we catch edges.
                var isVeryTinyError = hitAInsideBox < 1e-5f;
                hitA          = math.select(hitA, math.chgsign(halfSizeA, hitA), isVeryTinyError);
                hitAInsideBox = halfSizeA - math.abs(hitA);
                if (math.all(hitAInsideBox > 0f))
                {
                    // The error is a little bigger. Pick the closest axis and push out to the face.
                    var boostAxis   = math.tzcnt(math.bitmask(new bool4(hitAInsideBox == math.cmin(hitAInsideBox), false)));
                    var boostAmount = hitAInsideBox[boostAxis];
                    result.distance -= boostAmount;
                    hitA[boostAxis]  = math.chgsign(halfSizeA[boostAxis], hitA[boostAxis]);
                }
                result.hitpointA    = hitA;
                result.featureCodeB = (ushort)bestId;
                result.normalB      = math.rotate(bInASpace.rot, math.normalize(math.select(1f, -1f, (bestId & new int3(1, 2, 4)) != 0)));
                result.normalA      = math.normalize(math.select(0f, math.chgsign(1f, hitA), hitA == math.chgsign(halfSizeA, hitA)));
                result.featureCodeA = PointRayBox.FeatureCodeFromBoxNormal(result.normalA);
            }
            return result.distance <= maxDistance;
        }

        private static float3 FacePointAxesDistances(in float3 halfSizeA, in float3 halfSizeB, in RigidTransform bInASpace)
        {
            float3 x       = math.rotate(bInASpace.rot, new float3(halfSizeB.x, 0, 0));
            float3 y       = math.rotate(bInASpace.rot, new float3(0, halfSizeB.y, 0));
            float3 z       = math.rotate(bInASpace.rot, new float3(0, 0, halfSizeB.z));
            var extents = math.abs(x) + math.abs(y) + math.abs(z);

            var distancesBetweenCenters = math.abs(bInASpace.pos);
            var sumExtents              = halfSizeA + extents;
            return distancesBetweenCenters - sumExtents;
        }
#endif
    }
}

