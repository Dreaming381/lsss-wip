using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.TextCore;

namespace TextMeshDOTS.HarfBuzz.Bitmap
{
    internal static class SDF_generic
    {
        //generates SDF directly from bezier curves provided by Harfbuzz.
        //approach is inspired by FreeType 

        /// <summary>
        /// Converts a glyph into a SDF bitmap. While function accepts all kinds of edges found in font files
        /// (quadratic beziers, cubic beziers, lines), consider to generates lines before using this function for performance reasons
        /// </summary>
        public static bool SDFGenerateSubDivision(SDFOrientation orientation, ref DrawData drawData, ref NativeArray<byte> buffer, ref GlyphRect atlasRect, int padding, int atlasWidth, int atlasHeight, int spread = SDFCommon.DEFAULT_SPREAD)
        {
            PaintUtils.rasterizeSDFMarker.Begin();
            if (drawData.contourIDs.Length < 2 || drawData.edges.Length == 0)
                return false;

            if (spread < SDFCommon.MIN_SPREAD || spread > SDFCommon.MAX_SPREAD)
                return false;

            bool flip_y = true;
            var offset = drawData.glyphRect.min - padding;
            var atlasRectWidth = atlasRect.width;
            var atlasRectHeight = atlasRect.height;

            float sp_sq = math.select(spread, spread * spread, SDFCommon.USE_SQUARED_DISTANCES);

            var size = atlasRectWidth * atlasRectHeight;
            var targetDistances = new NativeArray<float>(size, Allocator.Temp);
            var targetCrosses = new NativeArray<float>(size, Allocator.Temp);
            var targetSigns = new NativeArray<int>(size, Allocator.Temp);

            //Truetype: CW for outer contours, CCW for holes, so we want right of p0 to P1 to be filled (=negative sign), so have to flip sign
            //Postscript: CCW for outer contours, CW for holes, so we want right of p0 to P1 to be filled (=positive sign)
            int flipSign = orientation == SDFOrientation.FILL_RIGHT ? -1 : 1;

            var edges = drawData.edges;
            var contourIDs = drawData.contourIDs;
            for (int contourID = 0, end = contourIDs.Length - 1; contourID < end; contourID++) //for each contour
            {
                int startID = contourIDs[contourID];
                int nextStartID = contourIDs[contourID + 1];
                for (int edgeID = startID; edgeID < nextStartID; edgeID++) //for each edge
                {
                    var edge = edges[edgeID];
                    edge.start_pos -= offset;
                    edge.control1 -= offset;
                    edge.control2 -= offset;
                    edge.end_pos -= offset;

                    var cbox = GetControlBox(edge);
                    cbox.Expand(spread);
                    float2 gridPoint;
                    float distance=default;
                    float cross= default;
                    int sign= default;
                    /* now loop over the pixels in the control box. */
                    for (int y = math.max((int)cbox.min.y, 0), yEnd = math.min((int)cbox.max.y, atlasRectHeight); y < yEnd; y++)
                    {
                        if (y < 0 || y >= atlasRectHeight)
                            continue;
                        gridPoint.y = y + 0.5f; // use the center of any pixel to be rendered within cbox
                        for (int x = math.max((int)cbox.min.x, 0), xEnd = math.min((int)cbox.max.x, atlasRectWidth); x < xEnd; x++)
                        {
                            if (x < 0 || x >= atlasRectWidth)
                                continue;

                            gridPoint.x = x + 0.5f; // use the center of any pixel to be rendered within cbox 
                            SDFEdgeGetMinDistance(edge, gridPoint, out distance, out cross, out sign);
                            //sign is positive when gridPointx lies to the left of the vector from p0 to p1, so left will be filled
                            //flip it if we want the right to be filled
                            sign *= flipSign;

                            var index = math.select(((atlasRectHeight - y - 1) * atlasRectWidth) + x, (y * atlasRectWidth) + x, flip_y);
                            SDFCommon.GetTarget_DistanceCrossSign(targetDistances, targetCrosses, targetSigns, index, out float targetDistance, out float targetCross, out int targetSign);
                            SDFCommon.ValidateDistanceCrossSign(ref distance, ref cross, ref sign, ref targetDistance, ref targetCross, ref targetSign, sp_sq, out var validDistance, out var validCross, out var validSign);
                            SDFCommon.SetTarget_DistanceCrossSign(targetDistances, targetCrosses, targetSigns, index, ref validDistance, ref validCross, ref validSign);
                        }
                    }
                }
            }
            SDFCommon.FinalPass(targetDistances, targetSigns, spread, atlasRectWidth, atlasRectHeight);

            //convert signed distance (range: negative = inside, positive=outside) to alpha bitmap (range: 0 (inside) to 255 (outside))
            SDFCommon.GetAlphaTexture(targetDistances, buffer, spread, atlasRect.x, atlasRect.y, atlasRectWidth, atlasRectHeight, atlasWidth, atlasHeight);
            PaintUtils.rasterizeSDFMarker.End();
            return true;
        }        

        static BBox GetControlBox(SDFEdge edge)
        {
            BBox cbox = BBox.Empty;
            bool is_set = false;


            switch (edge.edge_type)
            {
                case SDFEdgeType.CUBIC:
                    cbox.min = edge.control2;
                    cbox.max = edge.control2;

                    is_set = true;
                    goto case SDFEdgeType.QUADRATIC;

                case SDFEdgeType.QUADRATIC:
                    if (is_set)
                    {
                        cbox.min.x = edge.control1.x < cbox.min.x ? edge.control1.x : cbox.min.x;
                        cbox.min.y = edge.control1.y < cbox.min.y ? edge.control1.y : cbox.min.y;

                        cbox.max.x = edge.control1.x > cbox.max.x ? edge.control1.x : cbox.max.x;
                        cbox.max.y = edge.control1.y > cbox.max.y ? edge.control1.y : cbox.max.y;
                    }
                    else
                    {
                        cbox.min = edge.control1;
                        cbox.max = edge.control1;

                        is_set = true;
                    }
                    goto case SDFEdgeType.LINE;

                case SDFEdgeType.LINE:
                    if (is_set)
                    {
                        cbox.min.x = edge.start_pos.x < cbox.min.x ? edge.start_pos.x : cbox.min.x;
                        cbox.max.x = edge.start_pos.x > cbox.max.x ? edge.start_pos.x : cbox.max.x;

                        cbox.min.y = edge.start_pos.y < cbox.min.y ? edge.start_pos.y : cbox.min.y;
                        cbox.max.y = edge.start_pos.y > cbox.max.y ? edge.start_pos.y : cbox.max.y;
                    }
                    else
                    {
                        cbox.min = edge.start_pos;
                        cbox.max = edge.start_pos;
                    }

                    cbox.min.x = edge.end_pos.x < cbox.min.x ? edge.end_pos.x : cbox.min.x;
                    cbox.max.x = edge.end_pos.x > cbox.max.x ? edge.end_pos.x : cbox.max.x;

                    cbox.min.y = edge.end_pos.y < cbox.min.y ? edge.end_pos.y : cbox.min.y;
                    cbox.max.y = edge.end_pos.y > cbox.max.y ? edge.end_pos.y : cbox.max.y;

                    break;

                default:
                    break;
            }

            return cbox;
        }
        static BBox GetBBox(SDFEdge edge)
        {
            switch (edge.edge_type)
            {
                case SDFEdgeType.CUBIC:
                    return BezierMath.GetCubicBezierBBox(edge.start_pos, edge.control1, edge.control2, edge.end_pos);
                case SDFEdgeType.QUADRATIC:
                    return BezierMath.GetQuadraticBezierBBox(edge.start_pos, edge.control1, edge.end_pos);
                case SDFEdgeType.LINE:
                    return BezierMath.GetLineBBox(edge.start_pos, edge.end_pos);
                default:
                    break;
            }
            return BBox.Empty;
        }
        public static void SDFEdgeGetMinDistance(SDFEdge edge, float2 gridPoint, out float distance, out float cross, out int sign)
        {
            var p0 = edge.start_pos;
            var p1 = edge.control1;
            var p2 = edge.control2;
            var p3 = edge.end_pos;

            switch (edge.edge_type)
            {
                case SDFEdgeType.LINE:
                    SDFCommon.GetMinDistanceLineToPoint(p0.x, p0.y, p3.x, p3.y, gridPoint.x, gridPoint.y, out distance, out cross, out sign);
                    break;
                case SDFEdgeType.QUADRATIC:
                    GetMinDistanceQuadraticNewton(p0,p1,p3, gridPoint, out distance, out cross, out sign);
                    break;
                case SDFEdgeType.CUBIC:
                    GetMinDistanceCubicNewton(gridPoint, p1, p2, p3, gridPoint, out distance, out cross, out sign);
                    break;
                default:
                    distance = default;
                    cross = default;
                    sign = default;
                    break;
            }
        }        
        
        static bool GetMinDistanceQuadraticNewton(float2 p0, float2 p1, float2 p2, float2 point, out float distance, out float cross, out int sign)
        {
            float min = int.MaxValue;           // shortest distance
            float min_factor = 0;               // factor at shortest distance
            float2 nearest_point = default;     // point on curve nearest to `point`

            // compute substitution coefficients
            var aA = p0 - 2 * p1 + p2;
            var bB = 2 * (p1 - p0);
            var cC = p0;

            // do Newton's iterations
            for (int iterations = 0; iterations <= SDFCommon.MAX_NEWTON_DIVISIONS; iterations++)
            {
                float factor = (float)iterations / SDFCommon.MAX_NEWTON_DIVISIONS;

                for (int steps = 0; steps < SDFCommon.MAX_NEWTON_STEPS; steps++)
                {
                    var factor2 = factor * factor;
                    var curvePoint = (aA * factor2) + (bB * factor) + cC; // B(t) = t^2 * A + t * B + p0                    
                    var dist_vector = curvePoint - point;                // P(t) in the comment
                    var length = SDFCommon.USE_SQUARED_DISTANCES ? math.lengthsq(dist_vector) : math.length(dist_vector);
                    if (length < min)
                    {
                        min = length;
                        min_factor = factor;
                        nearest_point = curvePoint;
                    }

                    /* This is Newton's approximation.          */
                    /*   t := P(t) . B'(t) /                    */
                    /*          (B'(t) . B'(t) + P(t) . B''(t)) */
                    var d1 = (2 * factor * aA) + bB;                            // B'(t) = 2tA + B
                    var d2 = 2 * aA;                                            // B''(t) = 2A                   
                    var temp1 = math.dot(dist_vector, d1);                      // temp1 = P(t) . B'(t)
                    var temp2 = math.dot(d1, d1) + math.dot(dist_vector, d2);   // temp2 = B'(t) . B'(t) + P(t) . B''(t)
                    factor -= temp1 / temp2;

                    if (factor < 0 || factor > 1)
                        break;
                }
            }
            var direction = 2 * (aA * min_factor) + bB; // B'(t) = 2t * A + B

            // assign values, determine the sign
            var nearestVector = nearest_point - point;
            cross = BezierMath.cross2D(nearestVector.x, nearestVector.y, direction.x, direction.y);
            distance = min;
            sign = cross < 0 ? -1 : 1;

            bool nIsEndPoint = BezierMath.EqualsForSmallValues(min_factor, 0, BezierMath.epsilon100_rel) || BezierMath.EqualsForSmallValues(min_factor, 1, BezierMath.epsilon100_rel);
            if (Hint.Unlikely(nIsEndPoint))
            {
                direction = math.normalize(direction);
                nearestVector = math.normalize(nearestVector);
                cross = BezierMath.cross2D(direction.x, direction.y, nearestVector.x, nearestVector.y);
            }
            else
                cross = 1; // the two are perpendicular
           
            return true;
        }
        static bool GetMinDistanceCubicNewton(float2 p0, float2 p1, float2 p2, float2 p3, float2 point, out float distance, out float cross, out int sign)
        {
            float2 nearest_point = default;  // point on curve nearest to `point`
            float min_factor = 0;            // factor at shortest distance
            float min_factor_sq = 0;         // factor at shortest distance
            float min = int.MaxValue;        // shortest distance

            // compute substitution coefficients
            var aA = -p0 + 3 * (p1 - p2) + p3;
            var bB = 3 * (p0 - 2 * p1 + p2);
            var cC = 3 * (p1 - p0);
            var dD = p0;

            for (int iterations = 0; iterations <= SDFCommon.MAX_NEWTON_DIVISIONS; iterations++)
            {
                float factor = (float)iterations / SDFCommon.MAX_NEWTON_DIVISIONS;
                for (int steps = 0; steps < SDFCommon.MAX_NEWTON_STEPS; steps++)
                {
                    var factor2 = factor * factor;
                    var factor3 = factor2 * factor;
                    var curve_point = aA * factor3 + bB * factor2 + cC * factor + dD; // B(t) = t^3 * A + t^2 * B + t * C + D
                    var dist_vector = curve_point - point;                              // P(t) in the comment
                    var length = SDFCommon.USE_SQUARED_DISTANCES ? math.lengthsq(dist_vector) : math.length(dist_vector);
                    if (length < min)
                    {
                        min = length;
                        min_factor = factor;
                        min_factor_sq = factor2;
                        nearest_point = curve_point;
                    }

                    /* This the Newton's approximation.         */
                    /*   t := P(t) . B'(t) /                    */
                    /*          (B'(t) . B'(t) + P(t) . B''(t)) */
                    var d1 = aA * 3 * factor2 + bB * 2 * factor + cC;           // B'(t) = 3t^2 * A + 2t * B + C
                    var d2 = aA * 6 * factor + 2 * bB;                          // B''(t) = 6t * A + 2B
                    var temp1 = math.dot(dist_vector, d1);                      // temp1 = P(t) . B'(t)                  
                    var temp2 = math.dot(d1, d1) + math.dot(dist_vector, d2);   // temp2 = B'(t) . B'(t) + P(t) . B''(t)

                    factor -= temp1 / temp2;

                    if (factor < 0 || factor > 1)
                        break;
                }
            }
            var direction = 3 * min_factor_sq * aA + 2 * min_factor * bB + cC;  // B'(t) = 3t^2 * A + 2t * B + C

            // assign values, determine the sign
            var nearestVector = nearest_point - point;
            cross = BezierMath.cross2D(nearestVector.x, nearestVector.y, direction.x, direction.y);

            distance = min;
            sign = cross < 0 ? -1 : 1;
            bool nIsEndPoint = BezierMath.EqualsForSmallValues(min_factor, 0, BezierMath.epsilon100_rel) || BezierMath.EqualsForSmallValues(min_factor, 1, BezierMath.epsilon100_rel);
            if (Hint.Unlikely(nIsEndPoint))
            {
                //compute `cross` if not perpendicular
                direction = math.normalize(direction);
                nearestVector = math.normalize(nearestVector);
                cross = BezierMath.cross2D(direction.x, direction.y, nearestVector.x, nearestVector.y);
            }
            else
                cross = 1; // the two are perpendicular
            
            return true;
        }        
    }
}