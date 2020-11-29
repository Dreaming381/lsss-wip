using Unity.Mathematics;

namespace Latios.PhysicsEngine
{
    internal static partial class DistanceQueries
    {
        public static bool DistanceBetween(BoxCollider boxA,
                                           BoxCollider boxB,
                                           RigidTransform bInASpace,
                                           RigidTransform aInBSpace,
                                           float maxDistance,
                                           out ColliderDistanceResultInternal result)
        {
            //Step 1: Points vs faces
            simdFloat3 bTopPoints     = default;
            simdFloat3 bBottomPoints  = default;
            bTopPoints.x              = math.select(-boxB.halfSize.x, boxB.halfSize.x, new bool4(true, true, false, false));
            bBottomPoints.x           = bTopPoints.x;
            bBottomPoints.y           = -boxB.halfSize.y;
            bTopPoints.y              = boxB.halfSize.y;
            bTopPoints.z              = math.select(-boxB.halfSize.z, boxB.halfSize.z, new bool4(true, false, true, false));
            bBottomPoints.z           = bTopPoints.z;
            bTopPoints               += boxB.center;
            bBottomPoints            += boxB.center;
            var bTopPointsInAOS       = simd.transform(bInASpace, bTopPoints) - boxA.center;  //OS = origin space
            var bBottomPointsInAOS    = simd.transform(bInASpace, bBottomPoints) - boxA.center;

            QueriesLowLevelUtils.OriginAabb8Points(boxA.halfSize,
                                                   bTopPointsInAOS,
                                                   bBottomPointsInAOS,
                                                   out float3 pointsClosestAOutInA,
                                                   out float3 pointsClosestBOutInA,
                                                   out float axisDistanceOutInA);
            float pointsSignedDistanceSqInA = math.distancesq(pointsClosestAOutInA, pointsClosestBOutInA);
            pointsSignedDistanceSqInA       = math.select(pointsSignedDistanceSqInA, -pointsSignedDistanceSqInA, axisDistanceOutInA <= 0f);
            bool4 bTopMatch                 =
                (bTopPointsInAOS.x == pointsClosestBOutInA.x) & (bTopPointsInAOS.y == pointsClosestBOutInA.y) & (bTopPointsInAOS.z == pointsClosestBOutInA.z);
            bool4 bBottomMatch =
                (bBottomPointsInAOS.x == pointsClosestBOutInA.x) & (bBottomPointsInAOS.y == pointsClosestBOutInA.y) & (bBottomPointsInAOS.z == pointsClosestBOutInA.z);
            int bInABIndex = math.tzcnt((math.bitmask(bBottomMatch) << 4) | math.bitmask(bTopMatch));

            simdFloat3 aTopPoints     = default;
            simdFloat3 aBottomPoints  = default;
            aTopPoints.x              = math.select(-boxA.halfSize.x, boxA.halfSize.x, new bool4(true, true, false, false));
            aBottomPoints.x           = aTopPoints.x;
            aBottomPoints.y           = -boxA.halfSize.y;
            aTopPoints.y              = boxA.halfSize.y;
            aTopPoints.z              = math.select(-boxA.halfSize.z, boxA.halfSize.z, new bool4(true, false, true, false));
            aBottomPoints.z           = bTopPoints.z;
            aTopPoints               += boxA.center;
            aBottomPoints            += boxA.center;
            var aTopPointsInBOS       = simd.transform(aInBSpace, aTopPoints) - boxB.center;
            var aBottomPointsInBOS    = simd.transform(aInBSpace, aBottomPoints) - boxB.center;

            QueriesLowLevelUtils.OriginAabb8Points(boxB.halfSize,
                                                   aTopPointsInBOS,
                                                   aBottomPointsInBOS,
                                                   out float3 pointsClosestBOutInB,
                                                   out float3 pointsClosestAOutInB,
                                                   out float axisDistanceOutInB);
            float pointsSignedDistanceSqInB = math.distancesq(pointsClosestAOutInB, pointsClosestBOutInB);
            pointsSignedDistanceSqInB       = math.select(pointsSignedDistanceSqInB, -pointsSignedDistanceSqInB, axisDistanceOutInB <= 0f);
            bool4 aTopMatch                 =
                (aTopPointsInBOS.x == pointsClosestAOutInB.x) & (aTopPointsInBOS.y == pointsClosestAOutInB.y) & (aTopPointsInBOS.z == pointsClosestAOutInB.z);
            bool4 aBottomMatch =
                (aBottomPointsInBOS.x == pointsClosestAOutInB.x) & (aBottomPointsInBOS.y == pointsClosestAOutInB.y) & (aBottomPointsInBOS.z == pointsClosestAOutInB.z);
            int aInBAIndex = math.tzcnt((math.bitmask(aBottomMatch) << 4) | math.bitmask(aTopMatch));

            //Step 2: Edges vs edges

            //For any pair of normals, if the normals are colinear, then there must also exist a point-face pair that is equidistant.
            //However, for a pair of normals, up to two edges from each box can be valid.
            //For box A, assemble the points and edges procedurally using the box dimensions.
            //For box B, use a simd dot product and mask it against the best result. The first 1 index and last 1 index are taken. In most cases, these are the same, which is fine.
            //It is also worth noting that unlike a true SAT, directionality matters here, so we want to find the separating axis directionally oriented from a to b to get the correct closest features.
            //That's the max dot for a and the min dot for b.
            float3     bCenterInASpace  = math.transform(bInASpace, boxB.center) - boxA.center;
            simdFloat3 normalsA         = new simdFloat3(new float3(1f, 0f, 0f), new float3(0f, 1f, 0f), new float3(0f, 0f, 1f), new float3(1f, 0f, 0f));
            simdFloat3 normalsB         = simd.mul(bInASpace.rot, normalsA);
            simdFloat3 axes03           = simd.cross(normalsA.aaab, normalsB);  //normalsB is already .abca
            simdFloat3 axes47           = simd.cross(normalsA.bbcc, normalsB.bcab);
            float3     axes8            = math.cross(normalsB.c, normalsB.c);
            axes03                      = simd.select(-axes03, axes03, simd.dot(axes03, bCenterInASpace) >= 0f);
            axes47                      = simd.select(-axes47, axes47, simd.dot(axes47, bCenterInASpace) >= 0f);
            axes8                       = math.select(-axes8, axes8, math.dot(axes8, bCenterInASpace) >= 0f);
            bool4      invalid03        = (axes03.x == 0f) & (axes03.y == 0f) & (axes03.z == 0f);
            bool4      invalid47        = (axes47.x == 0f) & (axes47.y == 0f) & (axes47.z == 0f);
            bool       invalid8         = axes8.Equals(float3.zero);
            simdFloat3 bLeftPointsInAOS = simd.shuffle(bTopPointsInAOS,
                                                       bBottomPointsInAOS,
                                                       math.ShuffleComponent.LeftZ,
                                                       math.ShuffleComponent.LeftW,
                                                       math.ShuffleComponent.RightZ,
                                                       math.ShuffleComponent.RightW);
            simdFloat3 bRightPointsInAOS = simd.shuffle(bTopPointsInAOS,
                                                        bBottomPointsInAOS,
                                                        math.ShuffleComponent.LeftX,
                                                        math.ShuffleComponent.LeftY,
                                                        math.ShuffleComponent.RightX,
                                                        math.ShuffleComponent.RightY);
            simdFloat3 bFrontPointsInAOS = simd.shuffle(bTopPointsInAOS,
                                                        bBottomPointsInAOS,
                                                        math.ShuffleComponent.LeftY,
                                                        math.ShuffleComponent.LeftW,
                                                        math.ShuffleComponent.RightY,
                                                        math.ShuffleComponent.RightW);
            simdFloat3 bBackPointsInAOS = simd.shuffle(bTopPointsInAOS,
                                                       bBottomPointsInAOS,
                                                       math.ShuffleComponent.LeftX,
                                                       math.ShuffleComponent.LeftZ,
                                                       math.ShuffleComponent.RightX,
                                                       math.ShuffleComponent.RightZ);
            simdFloat3 bNormalsX = new simdFloat3(new float3(0f, math.SQRT2 / 2f, math.SQRT2 / 2f),
                                                  new float3(0f, math.SQRT2 / 2f, -math.SQRT2 / 2f),
                                                  new float3(0f, -math.SQRT2 / 2f, math.SQRT2 / 2f),
                                                  new float3(0f, -math.SQRT2 / 2f, -math.SQRT2 / 2f));
            simdFloat3 bNormalsY = new simdFloat3(new float3(math.SQRT2 / 2f, 0f, math.SQRT2 / 2f),
                                                  new float3(math.SQRT2 / 2f, 0f, -math.SQRT2 / 2f),
                                                  new float3(-math.SQRT2 / 2f, 0f, math.SQRT2 / 2f),
                                                  new float3(-math.SQRT2 / 2f, 0f, -math.SQRT2 / 2f));
            simdFloat3 bNormalsZ = new simdFloat3(new float3(math.SQRT2 / 2f, math.SQRT2 / 2f, 0f),
                                                  new float3(-math.SQRT2 / 2f, math.SQRT2 / 2f, 0f),
                                                  new float3(math.SQRT2 / 2f, -math.SQRT2 / 2f, 0f),
                                                  new float3(-math.SQRT2 / 2f, -math.SQRT2 / 2f, 0f));
            float3 bCenterPlaneDistancesInA = simd.dot(bCenterInASpace, normalsB).xyz;

            //x vs x
            float3 axisXX         = axes03.a;
            float3 aLeftOnXX      = math.select(-boxA.halfSize, boxA.halfSize, axisXX > 0f);
            float3 aLeftExtraOnXX = math.select(aLeftOnXX, boxA.halfSize, axisXX == 0f);
            aLeftExtraOnXX.x      = -boxA.halfSize.x;
            simdFloat3 aPointsXX  = new simdFloat3(aLeftOnXX, aLeftOnXX, aLeftExtraOnXX, aLeftExtraOnXX);
            simdFloat3 aEdgesXX   = new simdFloat3(new float3(2f * boxA.halfSize.x, 0f, 0f));

            var                   dotsXX        = simd.dot(bLeftPointsInAOS, axisXX);
            float                 bestDotXX     = math.cmin(dotsXX);
            int                   dotsMaskXX    = math.bitmask(dotsXX == bestDotXX);
            math.ShuffleComponent bIndexXX      = (math.ShuffleComponent)(3 - (math.lzcnt(dotsMaskXX) - 28));
            math.ShuffleComponent bExtraIndexXX = (math.ShuffleComponent)math.tzcnt(dotsMaskXX);
            simdFloat3            bPointsXX     = simd.shuffle(bLeftPointsInAOS, bLeftPointsInAOS, bIndexXX, bExtraIndexXX, bIndexXX, bExtraIndexXX);
            simdFloat3            bEdgesXX      = simd.shuffle(bRightPointsInAOS, bRightPointsInAOS, bIndexXX, bExtraIndexXX, bIndexXX, bExtraIndexXX) - bPointsXX;
            simdFloat3            bNormalsXX    = simd.shuffle(bNormalsX, bNormalsX, bIndexXX, bExtraIndexXX, bIndexXX, bExtraIndexXX);
            QueriesLowLevelUtils.SegmentSegment(aPointsXX, aEdgesXX, bPointsXX, bEdgesXX, out simdFloat3 xxClosestAOut, out simdFloat3 xxClosestBOut);
            bool4 insideXX =
                (math.abs(xxClosestBOut.x) < boxA.halfSize.x) & (math.abs(xxClosestBOut.y) < boxA.halfSize.y) & (math.abs(xxClosestBOut.z) < boxA.halfSize.z);
            insideXX                  |= QueriesLowLevelUtils.ArePointsInsideObb(xxClosestAOut, normalsB, bCenterPlaneDistancesInA, boxB.halfSize);
            float4 xxSignedDistanceSq  = simd.distancesq(xxClosestAOut, xxClosestBOut);
            xxSignedDistanceSq         = math.select(xxSignedDistanceSq, -xxSignedDistanceSq, insideXX);

            //x vs y
            float3 axisXY         = axes03.b;
            float3 aLeftOnXY      = math.select(-boxA.halfSize, boxA.halfSize, axisXY > 0f);
            float3 aLeftExtraOnXY = math.select(aLeftOnXY, boxA.halfSize, axisXY == 0f);
            aLeftExtraOnXY.x      = -boxA.halfSize.x;
            simdFloat3 aPointsXY  = new simdFloat3(aLeftOnXY, aLeftOnXY, aLeftExtraOnXY, aLeftExtraOnXY);
            simdFloat3 aEdgesXY   = new simdFloat3(new float3(2f * boxA.halfSize.x, 0f, 0f));

            var                   dotsXY        = simd.dot(bBottomPointsInAOS, axisXY);
            float                 bestDotXY     = math.cmin(dotsXY);
            int                   dotsMaskXY    = math.bitmask(dotsXY == bestDotXY);
            math.ShuffleComponent bIndexXY      = (math.ShuffleComponent)(3 - (math.lzcnt(dotsMaskXY) - 28));
            math.ShuffleComponent bExtraIndexXY = (math.ShuffleComponent)math.tzcnt(dotsMaskXY);
            simdFloat3            bPointsXY     = simd.shuffle(bBottomPointsInAOS, bBottomPointsInAOS, bIndexXY, bExtraIndexXY, bIndexXY, bExtraIndexXY);
            simdFloat3            bEdgesXY      = simd.shuffle(bTopPointsInAOS, bTopPointsInAOS, bIndexXY, bExtraIndexXY, bIndexXY, bExtraIndexXY) - bPointsXY;
            simdFloat3            bNormalsXY    = simd.shuffle(bNormalsY, bNormalsY, bIndexXY, bExtraIndexXY, bIndexXY, bExtraIndexXY);
            QueriesLowLevelUtils.SegmentSegment(aPointsXY, aEdgesXY, bPointsXY, bEdgesXY, out simdFloat3 xyClosestAOut, out simdFloat3 xyClosestBOut);
            bool4 insideXY =
                (math.abs(xyClosestBOut.x) < boxA.halfSize.x) & (math.abs(xyClosestBOut.y) < boxA.halfSize.y) & (math.abs(xyClosestBOut.z) < boxA.halfSize.z);
            insideXY                  |= QueriesLowLevelUtils.ArePointsInsideObb(xyClosestAOut, normalsB, bCenterPlaneDistancesInA, boxB.halfSize);
            float4 xySignedDistanceSq  = simd.distancesq(xyClosestAOut, xyClosestBOut);
            xySignedDistanceSq         = math.select(xySignedDistanceSq, -xySignedDistanceSq, insideXY);

            //x vs z
            float3 axisXZ         = axes03.c;
            float3 aLeftOnXZ      = math.select(-boxA.halfSize, boxA.halfSize, axisXZ > 0f);
            float3 aLeftExtraOnXZ = math.select(aLeftOnXZ, boxA.halfSize, axisXZ == 0f);
            aLeftExtraOnXZ.x      = -boxA.halfSize.x;
            simdFloat3 aPointsXZ  = new simdFloat3(aLeftOnXZ, aLeftOnXZ, aLeftExtraOnXZ, aLeftExtraOnXZ);
            simdFloat3 aEdgesXZ   = new simdFloat3(new float3(2f * boxA.halfSize.x, 0f, 0f));

            var                   dotsXZ        = simd.dot(bFrontPointsInAOS, axisXZ);
            float                 bestDotXZ     = math.cmin(dotsXZ);
            int                   dotsMaskXZ    = math.bitmask(dotsXZ == bestDotXZ);
            math.ShuffleComponent bIndexXZ      = (math.ShuffleComponent)(3 - (math.lzcnt(dotsMaskXZ) - 28));
            math.ShuffleComponent bExtraIndexXZ = (math.ShuffleComponent)math.tzcnt(dotsMaskXZ);
            simdFloat3            bPointsXZ     = simd.shuffle(bFrontPointsInAOS, bFrontPointsInAOS, bIndexXZ, bExtraIndexXZ, bIndexXZ, bExtraIndexXZ);
            simdFloat3            bEdgesXZ      = simd.shuffle(bBackPointsInAOS, bBackPointsInAOS, bIndexXZ, bExtraIndexXZ, bIndexXZ, bExtraIndexXZ) - bPointsXZ;
            simdFloat3            bNormalsXZ    = simd.shuffle(bNormalsZ, bNormalsZ, bIndexXZ, bExtraIndexXZ, bIndexXZ, bExtraIndexXZ);
            QueriesLowLevelUtils.SegmentSegment(aPointsXZ, aEdgesXZ, bPointsXZ, bEdgesXZ, out simdFloat3 xzClosestAOut, out simdFloat3 xzClosestBOut);
            bool4 insideXZ =
                (math.abs(xzClosestBOut.x) < boxA.halfSize.x) & (math.abs(xzClosestBOut.y) < boxA.halfSize.y) & (math.abs(xzClosestBOut.z) < boxA.halfSize.z);
            insideXZ                  |= QueriesLowLevelUtils.ArePointsInsideObb(xzClosestAOut, normalsB, bCenterPlaneDistancesInA, boxB.halfSize);
            float4 xzSignedDistanceSq  = simd.distancesq(xzClosestAOut, xzClosestBOut);
            xzSignedDistanceSq         = math.select(xzSignedDistanceSq, -xzSignedDistanceSq, insideXZ);

            //y
            //y vs x
            float3 axisYX         = axes03.d;
            float3 aLeftOnYX      = math.select(-boxA.halfSize, boxA.halfSize, axisYX > 0f);
            float3 aLeftExtraOnYX = math.select(aLeftOnYX, boxA.halfSize, axisYX == 0f);
            aLeftExtraOnYX.y      = -boxA.halfSize.y;
            simdFloat3 aPointsYX  = new simdFloat3(aLeftOnYX, aLeftOnYX, aLeftExtraOnYX, aLeftExtraOnYX);
            simdFloat3 aEdgesYX   = new simdFloat3(new float3(0f, 2f * boxA.halfSize.y, 0f));

            var                   dotsYX        = simd.dot(bLeftPointsInAOS, axisYX);
            float                 bestDotYX     = math.cmin(dotsYX);
            int                   dotsMaskYX    = math.bitmask(dotsYX == bestDotYX);
            math.ShuffleComponent bIndexYX      = (math.ShuffleComponent)(3 - (math.lzcnt(dotsMaskYX) - 28));
            math.ShuffleComponent bExtraIndexYX = (math.ShuffleComponent)math.tzcnt(dotsMaskYX);
            simdFloat3            bPointsYX     = simd.shuffle(bLeftPointsInAOS, bLeftPointsInAOS, bIndexYX, bExtraIndexYX, bIndexYX, bExtraIndexYX);
            simdFloat3            bEdgesYX      = simd.shuffle(bRightPointsInAOS, bRightPointsInAOS, bIndexYX, bExtraIndexYX, bIndexYX, bExtraIndexYX) - bPointsYX;
            simdFloat3            bNormalsYX    = simd.shuffle(bNormalsX, bNormalsX, bIndexYX, bExtraIndexYX, bIndexYX, bExtraIndexYX);
            QueriesLowLevelUtils.SegmentSegment(aPointsYX, aEdgesYX, bPointsYX, bEdgesYX, out simdFloat3 yxClosestAOut, out simdFloat3 yxClosestBOut);
            bool4 insideYX =
                (math.abs(yxClosestBOut.x) < boxA.halfSize.x) & (math.abs(yxClosestBOut.y) < boxA.halfSize.y) & (math.abs(yxClosestBOut.z) < boxA.halfSize.z);
            insideYX                  |= QueriesLowLevelUtils.ArePointsInsideObb(yxClosestAOut, normalsB, bCenterPlaneDistancesInA, boxB.halfSize);
            float4 yxSignedDistanceSq  = simd.distancesq(yxClosestAOut, yxClosestBOut);
            yxSignedDistanceSq         = math.select(yxSignedDistanceSq, -yxSignedDistanceSq, insideYX);

            //y vs y
            float3 axisYY         = axes47.a;
            float3 aLeftOnYY      = math.select(-boxA.halfSize, boxA.halfSize, axisYY > 0f);
            float3 aLeftExtraOnYY = math.select(aLeftOnYY, boxA.halfSize, axisYY == 0f);
            aLeftExtraOnYY.y      = -boxA.halfSize.y;
            simdFloat3 aPointsYY  = new simdFloat3(aLeftOnYY, aLeftOnYY, aLeftExtraOnYY, aLeftExtraOnYY);
            simdFloat3 aEdgesYY   = new simdFloat3(new float3(0f, 2f * boxA.halfSize.y, 0f));

            var                   dotsYY        = simd.dot(bBottomPointsInAOS, axisYY);
            float                 bestDotYY     = math.cmin(dotsYY);
            int                   dotsMaskYY    = math.bitmask(dotsYY == bestDotYY);
            math.ShuffleComponent bIndexYY      = (math.ShuffleComponent)(3 - (math.lzcnt(dotsMaskYY) - 28));
            math.ShuffleComponent bExtraIndexYY = (math.ShuffleComponent)math.tzcnt(dotsMaskYY);
            simdFloat3            bPointsYY     = simd.shuffle(bBottomPointsInAOS, bBottomPointsInAOS, bIndexYY, bExtraIndexYY, bIndexYY, bExtraIndexYY);
            simdFloat3            bEdgesYY      = simd.shuffle(bTopPointsInAOS, bTopPointsInAOS, bIndexYY, bExtraIndexYY, bIndexYY, bExtraIndexYY) - bPointsYY;
            simdFloat3            bNormalsYY    = simd.shuffle(bNormalsY, bNormalsY, bIndexYY, bExtraIndexYY, bIndexYY, bExtraIndexYY);
            QueriesLowLevelUtils.SegmentSegment(aPointsYY, aEdgesYY, bPointsYY, bEdgesYY, out simdFloat3 yyClosestAOut, out simdFloat3 yyClosestBOut);
            bool4 insideYY =
                (math.abs(yyClosestBOut.x) < boxA.halfSize.x) & (math.abs(yyClosestBOut.y) < boxA.halfSize.y) & (math.abs(yyClosestBOut.z) < boxA.halfSize.z);
            insideYY                  |= QueriesLowLevelUtils.ArePointsInsideObb(yyClosestAOut, normalsB, bCenterPlaneDistancesInA, boxB.halfSize);
            float4 yySignedDistanceSq  = simd.distancesq(yyClosestAOut, yyClosestBOut);
            yySignedDistanceSq         = math.select(yySignedDistanceSq, -yySignedDistanceSq, insideYY);

            //y vs z
            float3 axisYZ         = axes47.b;
            float3 aLeftOnYZ      = math.select(-boxA.halfSize, boxA.halfSize, axisYZ > 0f);
            float3 aLeftExtraOnYZ = math.select(aLeftOnYZ, boxA.halfSize, axisYZ == 0f);
            aLeftExtraOnYZ.y      = -boxA.halfSize.y;
            simdFloat3 aPointsYZ  = new simdFloat3(aLeftOnYZ, aLeftOnYZ, aLeftExtraOnYZ, aLeftExtraOnYZ);
            simdFloat3 aEdgesYZ   = new simdFloat3(new float3(0f, 2f * boxA.halfSize.y, 0f));

            var                   dotsYZ        = simd.dot(bFrontPointsInAOS, axisYZ);
            float                 bestDotYZ     = math.cmin(dotsYZ);
            int                   dotsMaskYZ    = math.bitmask(dotsYZ == bestDotYZ);
            math.ShuffleComponent bIndexYZ      = (math.ShuffleComponent)(3 - (math.lzcnt(dotsMaskYZ) - 28));
            math.ShuffleComponent bExtraIndexYZ = (math.ShuffleComponent)math.tzcnt(dotsMaskYZ);
            simdFloat3            bPointsYZ     = simd.shuffle(bFrontPointsInAOS, bFrontPointsInAOS, bIndexYZ, bExtraIndexYZ, bIndexYZ, bExtraIndexYZ);
            simdFloat3            bEdgesYZ      = simd.shuffle(bBackPointsInAOS, bBackPointsInAOS, bIndexYZ, bExtraIndexYZ, bIndexYZ, bExtraIndexYZ) - bPointsYZ;
            simdFloat3            bNormalsYZ    = simd.shuffle(bNormalsZ, bNormalsZ, bIndexYZ, bExtraIndexYZ, bIndexYZ, bExtraIndexYZ);
            QueriesLowLevelUtils.SegmentSegment(aPointsYZ, aEdgesYZ, bPointsYZ, bEdgesYZ, out simdFloat3 yzClosestAOut, out simdFloat3 yzClosestBOut);
            bool4 insideYZ =
                (math.abs(yzClosestBOut.x) < boxA.halfSize.x) & (math.abs(yzClosestBOut.y) < boxA.halfSize.y) & (math.abs(yzClosestBOut.z) < boxA.halfSize.z);
            insideYZ                  |= QueriesLowLevelUtils.ArePointsInsideObb(yzClosestAOut, normalsB, bCenterPlaneDistancesInA, boxB.halfSize);
            float4 yzSignedDistanceSq  = simd.distancesq(yzClosestAOut, yzClosestBOut);
            yzSignedDistanceSq         = math.select(yzSignedDistanceSq, -yzSignedDistanceSq, insideYZ);

            //z
            //z vs x
            float3 axisZX         = axes47.c;
            float3 aLeftOnZX      = math.select(-boxA.halfSize, boxA.halfSize, axisZX > 0f);
            float3 aLeftExtraOnZX = math.select(aLeftOnZX, boxA.halfSize, axisZX == 0f);
            aLeftExtraOnZX.z      = -boxA.halfSize.z;
            simdFloat3 aPointsZX  = new simdFloat3(aLeftOnZX, aLeftOnZX, aLeftExtraOnZX, aLeftExtraOnZX);
            simdFloat3 aEdgesZX   = new simdFloat3(new float3(0f, 0f, 2f * boxA.halfSize.z));

            var                   dotsZX        = simd.dot(bLeftPointsInAOS, axisZX);
            float                 bestDotZX     = math.cmin(dotsZX);
            int                   dotsMaskZX    = math.bitmask(dotsZX == bestDotZX);
            math.ShuffleComponent bIndexZX      = (math.ShuffleComponent)(3 - (math.lzcnt(dotsMaskZX) - 28));
            math.ShuffleComponent bExtraIndexZX = (math.ShuffleComponent)math.tzcnt(dotsMaskZX);
            simdFloat3            bPointsZX     = simd.shuffle(bLeftPointsInAOS, bLeftPointsInAOS, bIndexZX, bExtraIndexZX, bIndexZX, bExtraIndexZX);
            simdFloat3            bEdgesZX      = simd.shuffle(bRightPointsInAOS, bRightPointsInAOS, bIndexZX, bExtraIndexZX, bIndexZX, bExtraIndexZX) - bPointsZX;
            simdFloat3            bNormalsZX    = simd.shuffle(bNormalsX, bNormalsX, bIndexZX, bExtraIndexZX, bIndexZX, bExtraIndexZX);
            QueriesLowLevelUtils.SegmentSegment(aPointsZX, aEdgesZX, bPointsZX, bEdgesZX, out simdFloat3 zxClosestAOut, out simdFloat3 zxClosestBOut);
            bool4 insideZX =
                (math.abs(zxClosestBOut.x) < boxA.halfSize.x) & (math.abs(zxClosestBOut.y) < boxA.halfSize.y) & (math.abs(zxClosestBOut.z) < boxA.halfSize.z);
            insideZX                  |= QueriesLowLevelUtils.ArePointsInsideObb(zxClosestAOut, normalsB, bCenterPlaneDistancesInA, boxB.halfSize);
            float4 zxSignedDistanceSq  = simd.distancesq(zxClosestAOut, zxClosestBOut);
            zxSignedDistanceSq         = math.select(zxSignedDistanceSq, -zxSignedDistanceSq, insideZX);

            //z vs y
            float3 axisZY         = axes47.d;
            float3 aLeftOnZY      = math.select(-boxA.halfSize, boxA.halfSize, axisZY > 0f);
            float3 aLeftExtraOnZY = math.select(aLeftOnZY, boxA.halfSize, axisZY == 0f);
            aLeftExtraOnZY.z      = -boxA.halfSize.z;
            simdFloat3 aPointsZY  = new simdFloat3(aLeftOnZY, aLeftOnZY, aLeftExtraOnZY, aLeftExtraOnZY);
            simdFloat3 aEdgesZY   = new simdFloat3(new float3(0f, 0f, 2f * boxA.halfSize.z));

            var                   dotsZY        = simd.dot(bBottomPointsInAOS, axisZY);
            float                 bestDotZY     = math.cmin(dotsZY);
            int                   dotsMaskZY    = math.bitmask(dotsZY == bestDotZY);
            math.ShuffleComponent bIndexZY      = (math.ShuffleComponent)(3 - (math.lzcnt(dotsMaskZY) - 28));
            math.ShuffleComponent bExtraIndexZY = (math.ShuffleComponent)math.tzcnt(dotsMaskZY);
            simdFloat3            bPointsZY     = simd.shuffle(bBottomPointsInAOS, bBottomPointsInAOS, bIndexZY, bExtraIndexZY, bIndexZY, bExtraIndexZY);
            simdFloat3            bEdgesZY      = simd.shuffle(bTopPointsInAOS, bTopPointsInAOS, bIndexZY, bExtraIndexZY, bIndexZY, bExtraIndexZY) - bPointsZY;
            simdFloat3            bNormalsZY    = simd.shuffle(bNormalsY, bNormalsY, bIndexZY, bExtraIndexZY, bIndexZY, bExtraIndexZY);
            QueriesLowLevelUtils.SegmentSegment(aPointsZY, aEdgesZY, bPointsZY, bEdgesZY, out simdFloat3 zyClosestAOut, out simdFloat3 zyClosestBOut);
            bool4 insideZY =
                (math.abs(zyClosestBOut.x) < boxA.halfSize.x) & (math.abs(zyClosestBOut.y) < boxA.halfSize.y) & (math.abs(zyClosestBOut.z) < boxA.halfSize.z);
            insideZY                  |= QueriesLowLevelUtils.ArePointsInsideObb(zyClosestAOut, normalsB, bCenterPlaneDistancesInA, boxB.halfSize);
            float4 zySignedDistanceSq  = simd.distancesq(zyClosestAOut, zyClosestBOut);
            zySignedDistanceSq         = math.select(zySignedDistanceSq, -zySignedDistanceSq, insideZY);

            //z vs z
            float3 axisZZ         = axes8;
            float3 aLeftOnZZ      = math.select(-boxA.halfSize, boxA.halfSize, axisZZ > 0f);
            float3 aLeftExtraOnZZ = math.select(aLeftOnZZ, boxA.halfSize, axisZZ == 0f);
            aLeftExtraOnZZ.z      = -boxA.halfSize.z;
            simdFloat3 aPointsZZ  = new simdFloat3(aLeftOnZZ, aLeftOnZZ, aLeftExtraOnZZ, aLeftExtraOnZZ);
            simdFloat3 aEdgesZZ   = new simdFloat3(new float3(0f, 0f, 2f * boxA.halfSize.z));

            var                   dotsZZ        = simd.dot(bFrontPointsInAOS, axisZZ);
            float                 bestDotZZ     = math.cmin(dotsZZ);
            int                   dotsMaskZZ    = math.bitmask(dotsZZ == bestDotZZ);
            math.ShuffleComponent bIndexZZ      = (math.ShuffleComponent)(3 - (math.lzcnt(dotsMaskZZ) - 28));
            math.ShuffleComponent bExtraIndexZZ = (math.ShuffleComponent)math.tzcnt(dotsMaskZZ);
            simdFloat3            bPointsZZ     = simd.shuffle(bFrontPointsInAOS, bFrontPointsInAOS, bIndexZZ, bExtraIndexZZ, bIndexZZ, bExtraIndexZZ);
            simdFloat3            bEdgesZZ      = simd.shuffle(bBackPointsInAOS, bBackPointsInAOS, bIndexZZ, bExtraIndexZZ, bIndexZZ, bExtraIndexZZ) - bPointsZZ;
            simdFloat3            bNormalsZZ    = simd.shuffle(bNormalsZ, bNormalsZ, bIndexZZ, bExtraIndexZZ, bIndexZZ, bExtraIndexZZ);
            QueriesLowLevelUtils.SegmentSegment(aPointsZZ, aEdgesZZ, bPointsZZ, bEdgesZZ, out simdFloat3 zzClosestAOut, out simdFloat3 zzClosestBOut);
            bool4 insideZZ =
                (math.abs(zzClosestBOut.x) < boxA.halfSize.x) & (math.abs(zzClosestBOut.y) < boxA.halfSize.y) & (math.abs(zzClosestBOut.z) < boxA.halfSize.z);
            insideZZ                  |= QueriesLowLevelUtils.ArePointsInsideObb(zzClosestAOut, normalsB, bCenterPlaneDistancesInA, boxB.halfSize);
            float4 zzSignedDistanceSq  = simd.distancesq(zzClosestAOut, zzClosestBOut);
            zzSignedDistanceSq         = math.select(zzSignedDistanceSq, -zzSignedDistanceSq, insideZZ);

            //Step 3: Find the best result.
            float4     bestEdgeDistancesSq = math.select(xxSignedDistanceSq, float.MaxValue, invalid03.x);
            simdFloat3 bestEdgeClosestAs   = xxClosestAOut;
            simdFloat3 bestEdgeClosestBs   = xxClosestBOut;
            simdFloat3 bestNormalsBs       = bNormalsXX;

            bool4 newEdgeIsBetter  = (xySignedDistanceSq < bestEdgeDistancesSq) ^ ((xySignedDistanceSq < 0f) & (bestEdgeDistancesSq < 0f));
            newEdgeIsBetter       &= !invalid03.y;
            bestEdgeDistancesSq    = math.select(bestEdgeDistancesSq, xySignedDistanceSq, newEdgeIsBetter);
            bestEdgeClosestAs      = simd.select(bestEdgeClosestAs, xyClosestAOut, newEdgeIsBetter);
            bestEdgeClosestBs      = simd.select(bestEdgeClosestBs, xyClosestBOut, newEdgeIsBetter);
            bestNormalsBs          = simd.select(bestNormalsBs, bNormalsXY, newEdgeIsBetter);

            newEdgeIsBetter      = (xzSignedDistanceSq < bestEdgeDistancesSq) ^ ((xzSignedDistanceSq < 0f) & (bestEdgeDistancesSq < 0f));
            newEdgeIsBetter     &= !invalid03.z;
            bestEdgeDistancesSq  = math.select(bestEdgeDistancesSq, xzSignedDistanceSq, newEdgeIsBetter);
            bestEdgeClosestAs    = simd.select(bestEdgeClosestAs, xzClosestAOut, newEdgeIsBetter);
            bestEdgeClosestBs    = simd.select(bestEdgeClosestBs, xzClosestBOut, newEdgeIsBetter);
            bestNormalsBs        = simd.select(bestNormalsBs, bNormalsXZ, newEdgeIsBetter);

            newEdgeIsBetter      = (yxSignedDistanceSq < bestEdgeDistancesSq) ^ ((yxSignedDistanceSq < 0f) & (bestEdgeDistancesSq < 0f));
            newEdgeIsBetter     &= !invalid03.w;
            bestEdgeDistancesSq  = math.select(bestEdgeDistancesSq, yxSignedDistanceSq, newEdgeIsBetter);
            bestEdgeClosestAs    = simd.select(bestEdgeClosestAs, yxClosestAOut, newEdgeIsBetter);
            bestEdgeClosestBs    = simd.select(bestEdgeClosestBs, yxClosestBOut, newEdgeIsBetter);
            bestNormalsBs        = simd.select(bestNormalsBs, bNormalsYX, newEdgeIsBetter);

            newEdgeIsBetter      = (yySignedDistanceSq < bestEdgeDistancesSq) ^ ((yySignedDistanceSq < 0f) & (bestEdgeDistancesSq < 0f));
            newEdgeIsBetter     &= !invalid47.x;
            bestEdgeDistancesSq  = math.select(bestEdgeDistancesSq, yySignedDistanceSq, newEdgeIsBetter);
            bestEdgeClosestAs    = simd.select(bestEdgeClosestAs, yyClosestAOut, newEdgeIsBetter);
            bestEdgeClosestBs    = simd.select(bestEdgeClosestBs, yyClosestBOut, newEdgeIsBetter);
            bestNormalsBs        = simd.select(bestNormalsBs, bNormalsYY, newEdgeIsBetter);

            newEdgeIsBetter      = (yzSignedDistanceSq < bestEdgeDistancesSq) ^ ((yzSignedDistanceSq < 0f) & (bestEdgeDistancesSq < 0f));
            newEdgeIsBetter     &= !invalid47.y;
            bestEdgeDistancesSq  = math.select(bestEdgeDistancesSq, yzSignedDistanceSq, newEdgeIsBetter);
            bestEdgeClosestAs    = simd.select(bestEdgeClosestAs, yzClosestAOut, newEdgeIsBetter);
            bestEdgeClosestBs    = simd.select(bestEdgeClosestBs, yzClosestBOut, newEdgeIsBetter);
            bestNormalsBs        = simd.select(bestNormalsBs, bNormalsYZ, newEdgeIsBetter);

            newEdgeIsBetter      = (zxSignedDistanceSq < bestEdgeDistancesSq) ^ ((zxSignedDistanceSq < 0f) & (bestEdgeDistancesSq < 0f));
            newEdgeIsBetter     &= !invalid47.z;
            bestEdgeDistancesSq  = math.select(bestEdgeDistancesSq, zxSignedDistanceSq, newEdgeIsBetter);
            bestEdgeClosestAs    = simd.select(bestEdgeClosestAs, zxClosestAOut, newEdgeIsBetter);
            bestEdgeClosestBs    = simd.select(bestEdgeClosestBs, zxClosestBOut, newEdgeIsBetter);
            bestNormalsBs        = simd.select(bestNormalsBs, bNormalsZX, newEdgeIsBetter);

            newEdgeIsBetter      = (zySignedDistanceSq < bestEdgeDistancesSq) ^ ((zySignedDistanceSq < 0f) & (bestEdgeDistancesSq < 0f));
            newEdgeIsBetter     &= !invalid47.w;
            bestEdgeDistancesSq  = math.select(bestEdgeDistancesSq, zySignedDistanceSq, newEdgeIsBetter);
            bestEdgeClosestAs    = simd.select(bestEdgeClosestAs, zyClosestAOut, newEdgeIsBetter);
            bestEdgeClosestBs    = simd.select(bestEdgeClosestBs, zyClosestBOut, newEdgeIsBetter);
            bestNormalsBs        = simd.select(bestNormalsBs, bNormalsZY, newEdgeIsBetter);

            newEdgeIsBetter      = (zzSignedDistanceSq < bestEdgeDistancesSq) ^ ((zzSignedDistanceSq < 0f) & (bestEdgeDistancesSq < 0f));
            newEdgeIsBetter     &= !invalid8;
            bestEdgeDistancesSq  = math.select(bestEdgeDistancesSq, zzSignedDistanceSq, newEdgeIsBetter);
            bestEdgeClosestAs    = simd.select(bestEdgeClosestAs, zzClosestAOut, newEdgeIsBetter);
            bestEdgeClosestBs    = simd.select(bestEdgeClosestBs, zzClosestBOut, newEdgeIsBetter);
            bestNormalsBs        = simd.select(bestNormalsBs, bNormalsZZ, newEdgeIsBetter);

            float  bestEdgeSignedDistanceSq = math.cmin(bestEdgeDistancesSq);
            int    bestIndex                = math.tzcnt(math.bitmask(bestEdgeSignedDistanceSq == bestEdgeDistancesSq));
            float3 bestEdgeClosestA         = bestEdgeClosestAs[bestIndex];
            float3 bestEdgeClosestB         = bestEdgeClosestBs[bestIndex];
            float3 bestEdgeBNormal          = bestNormalsBs[bestIndex];
            UnityEngine.Debug.Log($"distance xx: {xxSignedDistanceSq}");
            UnityEngine.Debug.Log($"valid xx: {!invalid03.x}");
            UnityEngine.Debug.Log($"inside xx:{ insideXX}");

            simdFloat3 topUnnormals    = default;
            simdFloat3 bottomUnnormals = default;
            topUnnormals.x             = math.select(-1f, 1f, new bool4(true, true, false, false));
            bottomUnnormals.x          = topUnnormals.x;
            bottomUnnormals.y          = -1f;
            topUnnormals.y             = 1f;
            topUnnormals.z             = math.select(-1f, 1f, new bool4(true, false, true, false));
            bottomUnnormals.z          = topUnnormals.z;

            float3 pointsNormalBFromBInA = simd.shuffle(topUnnormals, bottomUnnormals, (math.ShuffleComponent)bInABIndex);
            float3 pointsNormalAFromAInB = simd.shuffle(topUnnormals, bottomUnnormals, (math.ShuffleComponent)aInBAIndex);
            pointsNormalBFromBInA        = math.normalize(math.rotate(bInASpace, pointsNormalBFromBInA));
            pointsNormalAFromAInB        = math.normalize(pointsNormalAFromAInB);
            float3 pointsNormalBFromAInB = math.select(0f, 1f, pointsClosestBOutInB == boxB.halfSize) + math.select(0f, -1f, pointsClosestBOutInB == -boxB.halfSize);
            pointsNormalBFromAInB        = math.normalize(math.rotate(bInASpace, pointsNormalBFromAInB));

            bool pointsInAIsBetter = (pointsSignedDistanceSqInA <= bestEdgeSignedDistanceSq) ^ ((pointsSignedDistanceSqInA < 0f) & (bestEdgeSignedDistanceSq < 0f));
            //pointsInAIsBetter           = true;  //debug
            float  bestSignedDistanceSq = math.select(bestEdgeSignedDistanceSq, pointsSignedDistanceSqInA, pointsInAIsBetter);
            float3 bestClosestA         = math.select(bestEdgeClosestA, pointsClosestAOutInA, pointsInAIsBetter);
            float3 bestClosestB         = math.select(bestEdgeClosestB, pointsClosestBOutInA, pointsInAIsBetter);
            float3 bestNormalB          = math.select(bestEdgeBNormal, pointsNormalBFromBInA, pointsInAIsBetter);

            float3 bestNormalA = math.normalize(math.select(0f, 1f, bestClosestA == boxA.halfSize) + math.select(0f, -1f, bestClosestA == -boxA.halfSize));

            bool bInAIsBetter    = (bestSignedDistanceSq <= pointsSignedDistanceSqInB) ^ ((pointsSignedDistanceSqInA < 0f) & (bestSignedDistanceSq < 0f));
            bestSignedDistanceSq = math.select(pointsSignedDistanceSqInB, bestSignedDistanceSq, bInAIsBetter);
            bestClosestA         = math.select(math.transform(bInASpace, pointsClosestAOutInB + boxB.center), bestClosestA + boxA.center, bInAIsBetter);
            bestClosestB         = math.select(math.transform(bInASpace, pointsClosestBOutInB + boxB.center), bestClosestB + boxA.center, bInAIsBetter);
            bestNormalA          = math.select(pointsNormalAFromAInB, bestNormalA, bInAIsBetter);
            bestNormalB          = math.select(pointsNormalBFromAInB, bestNormalB, bInAIsBetter);

            //Step 4: Build result
            result = new ColliderDistanceResultInternal
            {
                hitpointA = bestClosestA,
                hitpointB = bestClosestB,
                normalA   = bestNormalA,
                normalB   = bestNormalB,
                distance  = math.sign(bestSignedDistanceSq) * math.sqrt(math.abs(bestSignedDistanceSq))
            };
            return result.distance <= maxDistance;
        }
    }
}

