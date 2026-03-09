using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace TextMeshDOTS.HarfBuzz.Bitmap
{
    /*
        description of rasterizer: https://nothings.org/gamedev/rasterize/
        https://github.com/nothings/stb/blob/master/stb_truetype.h

        ------------------------------------------------------------------------------
        This software is available under 2 licenses -- choose whichever you prefer.
        ------------------------------------------------------------------------------
        ALTERNATIVE A - MIT License
        Copyright (c) 2017 Sean Barrett
        Permission is hereby granted, free of charge, to any person obtaining a copy of
        this software and associated documentation files (the "Software"), to deal in
        the Software without restriction, including without limitation the rights to
        use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies
        of the Software, and to permit persons to whom the Software is furnished to do
        so, subject to the following conditions:
        The above copyright notice and this permission notice shall be included in all
        copies or substantial portions of the Software.
        THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
        IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
        FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.IN NO EVENT SHALL THE
        AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
        LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
        OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
        SOFTWARE.
    */
    internal static class AntiAliasedRasterizerSTBTruetype
    {
        public static void Rasterize<T>(ref DrawData drawData, NativeArray<ColorARGB> textureData, T pattern, BBox clipRect, bool invert = false) where T : IPattern
        {
            PaintUtils.rasterizeCOLRMarker.Begin();

            var sdfEdges = drawData.edges;
            var contourIDs = drawData.contourIDs;
            var edges = new NativeList<Edge>(sdfEdges.Length, Allocator.Temp);
            for (int contourID = 0, end = contourIDs.Length - 1; contourID < end; contourID++) //for each contour
            {
                int startID = contourIDs[contourID];
                int nextStartID = contourIDs[contourID + 1];
                for (int edgeID = startID; edgeID < nextStartID; edgeID++) //for each edge
                {
                    var sdfEdge = sdfEdges[edgeID];
                    var start_pos = sdfEdge.start_pos;
                    var end_pos = sdfEdge.end_pos;
                    // skip the edge if horizontal
                    if (start_pos.y == end_pos.y)
                        continue;
                    // add edge from j to k to the list
                    var edge = new Edge();
                    edge.invert = false;
                    if (invert ? end_pos.y > start_pos.y : end_pos.y < start_pos.y)
                    {
                        edge.invert = true;
                        (start_pos, end_pos) = (end_pos, start_pos);
                    }
                    edge.x0 = start_pos.x;
                    edge.y0 = start_pos.y;
                    edge.x1 = end_pos.x;
                    edge.y1 = end_pos.y;
                    edges.Add(edge);
                }
            }
            edges.Sort(default(EdgeYMaxComparer));
            RasterizeSortedEdges(textureData, pattern, clipRect.intWidth, clipRect.intHeight, edges, (int)clipRect.min.x, (int)clipRect.min.y);
            PaintUtils.rasterizeCOLRMarker.End();
        }


        static void RasterizeSortedEdges<T>(NativeArray<ColorARGB> textureData, T pattern, int width, int height, NativeList<Edge> edges, int off_x, int off_y) where T : IPattern
        {
            var actives = new NativeList<ActiveEdge>(16, Allocator.Temp);
            int activeID = -1;
            int y, j = 0;
            var scanline = new NativeArray<float>(width, Allocator.Temp);
            var scanline_fill = new NativeArray<float>(width + 1, Allocator.Temp);

            y = off_y;
            var n = edges.Length - 1;
            var edgeID = 0;
            //edges.ElementAt(n).y0 = (float)(off_y + textureHeight) + 1;

            while (j < height)
            {
                float scanYTop = y + 1.0f;
                int stepID = activeID;
                int previousStepID = -1;

                ClearArray(scanline, 0, width);
                ClearArray(scanline_fill, 0, width + 1);

                while (stepID != -1)
                {
                    ref var step = ref actives.ElementAt(stepID);
                    if (step.y1 <= y)
                    {
                        if (previousStepID == -1)
                            activeID = step.nextID;
                        else
                            actives.ElementAt(previousStepID).nextID = step.nextID; //skip current active (effectivly deletes it)
                        stepID = step.nextID;
                        step.dir = 0;
                    }
                    else
                    {
                        previousStepID = stepID;
                        stepID = step.nextID; // advance through list
                    }
                }

                while (edgeID <= n && edges[edgeID].y0 <= scanYTop)
                {
                    var zID = AddActiveEdge(actives, edges[edgeID], off_x, y);
                    if (zID != -1)
                    {
                        ref var z = ref actives.ElementAt(zID);
                        if (j == 0 && off_y != 0)
                        {
                            if (z.y1 < y)
                                z.y1 = y;
                        }
                        z.nextID = activeID;
                        activeID = zID;
                    }
                    edgeID++;
                }              
                FillActiveEdges(scanline, scanline_fill, width, actives, activeID, y);

                float sum = 0;
                for (int i = 0; i < width; ++i)
                {
                    float k;
                    int m;
                    sum += scanline_fill[i];
                    k = scanline[i] + sum;
                    k = math.abs(k) * 255 + 0.5f;
                    m = (int)k;
                    if (m > 255) m = 255;

                    if (m > 1)
                    {
                        var color = pattern.GetColor(new float2(i + off_x, y));
                        var targetIndex = width * (y - off_y) + i; //(why not (i - off_x)?; substracting clipRect.min results in aliging glyph with (0,0) of bitmap
                        color.a = (byte)(color.a * (byte)m / 255);
                        textureData[targetIndex] = color;
                    }
                }

                stepID = activeID;
                while (stepID != -1)
                {
                    ref var z = ref actives.ElementAt(stepID);
                    z.x += z.dxdy; // advance to position for current scanline
                    stepID = z.nextID; // advance through list
                }

                y++;
                j++;
            }
        }
        static void FillActiveEdges(NativeArray<float> scanline, NativeArray<float> scanline_fill, int width, NativeList<ActiveEdge> actvies, int eID, float y_bottom)
        {
            float y_top = y_bottom + 1;

            while (eID != -1)
            {
                // brute force every pixel

                // compute intersection points with top & bottom
                ref var e = ref actvies.ElementAt(eID);
                //Debug.Assert(e.ey >= y_top);

                if (e.dxdy == 0)
                {
                    float x0 = e.x;
                    if (x0 < width)
                    {
                        if (x0 >= 0)
                        {
                            HandleClippedEdge(scanline, (int)x0, ref e, x0, y_bottom, x0, y_top);
                            HandleClippedEdge(scanline_fill, (int)x0 + 1, ref e, x0, y_bottom, x0, y_top);
                        }
                        else
                            HandleClippedEdge(scanline_fill, 0, ref e, x0, y_bottom, x0, y_top);
                    }
                }
                else
                {
                    float x0 = e.x;
                    float dxdy = e.dxdy;
                    float xnext = x0 + dxdy;
                    float x_bottom, x_top;
                    float sy0, sy1;
                    float dydx = e.dydx;
                    //Debug.Assert(e.sy <= y_bottom && e.ey >= y_top);

                    // compute endpoints of line segment clipped to this scanline (if the
                    // line segment starts on this scanline. x0 is the intersection of the
                    // line with y_top, but that may be off the line segment.
                    if (e.y0 > y_bottom)
                    {
                        x_bottom = x0 + dxdy * (e.y0 - y_bottom);
                        sy0 = e.y0;
                    }
                    else
                    {
                        x_bottom = x0;
                        sy0 = y_bottom;
                    }
                    if (e.y1 < y_top)
                    {
                        x_top = x0 + dxdy * (e.y1 - y_bottom);
                        sy1 = e.y1;
                    }
                    else
                    {
                        x_top = xnext;
                        sy1 = y_top;
                    }

                    if (x_bottom >= 0 && x_top >= 0 && x_bottom < width && x_top < width)
                    {
                        // from here on, we don't have to range check x values

                        if ((int)x_bottom == (int)x_top)
                        {
                            // simple case, only spans one pixel
                            int x = (int)x_bottom;
                            var height = (sy1 - sy0) * e.dir;
                            //Debug.Assert(x >= 0 && x < len);
                            var bottomWidth = (x + 1.0f) - x_bottom;
                            var topWidth = (x + 1.0f) - x_top;
                            scanline[x] += TrapezoidArea(height, bottomWidth, topWidth);
                            scanline_fill[x + 1] += height; // everything right of this pixel is filled
                        }
                        else
                        {
                            int x1, x2;
                            float y_crossing, y_final, step, sign, area;
                            // covers 2+ pixels
                            if (x_bottom > x_top)
                            {
                                // flip scanline vertically; signed area is the same
                                (x_bottom, x_top) = (x_top, x_bottom);
                                (x0, xnext) = (xnext, x0);
                                dydx = -dydx;
                            }
                            //Debug.Assert(dy >= 0);
                            //Debug.Assert(dx >= 0);

                            x1 = (int)x_bottom;
                            x2 = (int)x_top;
                            // compute intersection with y axis at x1+1
                            y_crossing = y_bottom + dydx * (x1 + 1 - x0);

                            // compute intersection with y axis at x2
                            y_final = y_bottom + dydx * (x2 - x0);

                            //     y_top  +------------+------------+------------+------------+------------+
                            //            |            |            |            |            |            |
                            //            |            |            |            |            |            |
                            //       sy1  |            |            |            |   xxxxxT...|............|
                            //   y_final  |            |     \-     |          xx*xxx.........|............|
                            //            |            | dy <       |    xxxxxx..|............|............|
                            //            |            |     /-   xx*xxxx........|............|............|
                            //            |            |     xxxxx..|............|............|............|
                            // y_crossing |            *xxxxx.......|............|............|............|
                            //       sy0  |      Bxxxxx|............|............|............|............|
                            //            |            |            |            |            |            |
                            //            |            |            |            |            |            |
                            //  y_bottom  +------|-----+------------+------------+--------|---+------------+
                            //         x1    x_bottom                            x2    x_top
                            //
                            // goal is to measure the area covered by '.' in each pixel

                            // if x2 is right at the right edge of x1, y_crossing can blow up, github #1057
                            // @TODO: maybe test against sy1 rather than y_bottom?
                            if (y_crossing > y_top)
                                y_crossing = y_top;

                            sign = e.dir;

                            // area of the rectangle covered from sy0..y_crossing
                            area = sign * (y_crossing - sy0);

                            // area of the triangle (x_top,sy0), (x1+1,sy0), (x1+1,y_crossing)
                            scanline[x1] += TriangleArea(area, x1 + 1 - x_bottom);

                            // check if final y_crossing is blown up; no test case for this
                            if (y_final > y_top)
                            {
                                y_final = y_top;
                                dydx = (y_final - y_crossing) / (x2 - (x1 + 1)); // if denom=0, y_final = y_crossing, so y_final <= y_bottom
                            }

                            // in second pixel, area covered by line segment found in first pixel
                            // is always a rectangle 1 wide * the height of that line segment; this
                            // is exactly what the variable 'area' stores. it also gets a contribution
                            // from the line segment within it. the THIRD pixel will get the first
                            // pixel's rectangle contribution, the second pixel's rectangle contribution,
                            // and its own contribution. the 'own contribution' is the same in every pixel except
                            // the leftmost and rightmost, a trapezoid that slides down in each pixel.
                            // the second pixel's contribution to the third pixel will be the
                            // rectangle 1 wide times the height change in the second pixel, which is dy.

                            step = sign * dydx; // dy is dy/dx, change in y for every 1 change in x,
                                                // which multiplied by 1-pixel-width is how much pixel area changes for each step in x
                                                // so the area advances by 'step' every time

                            for (int xi = x1 + 1; xi < x2; xi++)
                            {
                                scanline[xi] += area + step * 0.5f; // area of trapezoid is 1*step/2
                                area += step;
                            }
                            //Debug.Assert(math.abs(area) <= 1.01f); // accumulated error from area += step unless we round step down
                            //Debug.Assert(sy1 > y_final - 0.01f);

                            // area covered in the last pixel is the rectangle from all the pixels to the left,
                            // plus the trapezoid filled by the line segment in this pixel all the way to the right edge
                            var topWidth = (x2 + 1.0f) - x_top;
                            scanline[x2] += area + sign * TrapezoidArea(sy1 - y_final, 1.0f, topWidth);

                            // the rest of the line is filled based on the total height of the line segment in this pixel
                            scanline_fill[x2 + 1] += sign * (sy1 - sy0);
                        }
                    }
                    else
                    {
                        // if edge goes outside of box we're drawing, we require
                        // clipping logic. since this does not match the intended use
                        // of this library, we use a different, very slow brute
                        // force implementation
                        // note though that this does happen some of the time because
                        // x_top and x_bottom can be extrapolated at the top & bottom of
                        // the shape and actually lie outside the bounding box
                        int x;
                        for (x = 0; x < width; ++x)
                        {
                            // cases:
                            //
                            // there can be up to two intersections with the pixel. any intersection
                            // with left or right edges can be handled by splitting into two (or three)
                            // regions. intersections with top & bottom do not necessitate case-wise logic.
                            //
                            // the old way of doing this found the intersections with the left & right edges,
                            // then used some simple logic to produce up to three segments in sorted order
                            // from top-to-bottom. however, this had a problem: if an x edge was epsilon
                            // across the x border, then the corresponding y position might not be distinct
                            // from the other y segment, and it might ignored as an empty segment. to avoid
                            // that, we need to explicitly produce segments based on x positions.

                            // rename variables to clearly-defined pairs
                            float y0 = y_bottom;
                            float x1 = (float)(x);
                            float x2 = (float)(x + 1);
                            float x3 = xnext;
                            float y3 = y_top;

                            // x = e.x + e.dx * (y-y_top)
                            // (y-y_top) = (x - e.x) / e.dx
                            // y = (x - e.x) / e.dx + y_top
                            float y1 = (x - x0) / dxdy + y_bottom;
                            float y2 = (x + 1 - x0) / dxdy + y_bottom;

                            if (x0 < x1 && x3 > x2)
                            {         // three segments descending down-right
                                HandleClippedEdge(scanline, x, ref e, x0, y0, x1, y1);
                                HandleClippedEdge(scanline, x, ref e, x1, y1, x2, y2);
                                HandleClippedEdge(scanline, x, ref e, x2, y2, x3, y3);
                            }
                            else if (x3 < x1 && x0 > x2)
                            {  // three segments descending down-left
                                HandleClippedEdge(scanline, x, ref e, x0, y0, x2, y2);
                                HandleClippedEdge(scanline, x, ref e, x2, y2, x1, y1);
                                HandleClippedEdge(scanline, x, ref e, x1, y1, x3, y3);
                            }
                            else if (x0 < x1 && x3 > x1)
                            {  // two segments across x, down-right
                                HandleClippedEdge(scanline, x, ref e, x0, y0, x1, y1);
                                HandleClippedEdge(scanline, x, ref e, x1, y1, x3, y3);
                            }
                            else if (x3 < x1 && x0 > x1)
                            {  // two segments across x, down-left
                                HandleClippedEdge(scanline, x, ref e, x0, y0, x1, y1);
                                HandleClippedEdge(scanline, x, ref e, x1, y1, x3, y3);
                            }
                            else if (x0 < x2 && x3 > x2)
                            {  // two segments across x+1, down-right
                                HandleClippedEdge(scanline, x, ref e, x0, y0, x2, y2);
                                HandleClippedEdge(scanline, x, ref e, x2, y2, x3, y3);
                            }
                            else if (x3 < x2 && x0 > x2)
                            {  // two segments across x+1, down-left
                                HandleClippedEdge(scanline, x, ref e, x0, y0, x2, y2);
                                HandleClippedEdge(scanline, x, ref e, x2, y2, x3, y3);
                            }
                            else
                            {  // one segment
                                HandleClippedEdge(scanline, x, ref e, x0, y0, x3, y3);
                            }
                        }
                    }
                }
                eID = e.nextID;
            }
        }
        // the edge passed in here does not cross the vertical line at x or the vertical line at x+1
        // (i.e. it has already been clipped to those)
        static void HandleClippedEdge(NativeArray<float> scanline, int x, ref ActiveEdge e, float x0, float y0, float x1, float y1)
        {
            if (y0 == y1) return;
            //Debug.Assert(y0 < y1);
            //Debug.Assert(e.sy <= e.ey);
            if (y0 > e.y1) return;
            if (y1 < e.y0) return;
            if (y0 < e.y0)
            {
                x0 += (x1 - x0) * (e.y0 - y0) / (y1 - y0);
                y0 = e.y0;
            }
            if (y1 > e.y1)
            {
                x1 += (x1 - x0) * (e.y1 - y1) / (y1 - y0);
                y1 = e.y1;
            }

            //if (x0 == x)
            //    Debug.Assert(x1 <= x + 1);
            //else if (x0 == x + 1)
            //    Debug.Assert(x1 >= x);
            //else if (x0 <= x)
            //    Debug.Assert(x1 <= x);
            //else if (x0 >= x + 1)
            //    Debug.Assert(x1 >= x + 1);
            //else
            //    Debug.Assert(x1 >= x && x1 <= x + 1);

            if (x0 <= x && x1 <= x)
                scanline[x] += e.dir * (y1 - y0);
            else if (x0 >= x + 1 && x1 >= x + 1)
            {

            }
            else
            {
                //Debug.Assert(x0 >= x && x0 <= x + 1 && x1 >= x && x1 <= x + 1);
                scanline[x] += e.dir * (y1 - y0) * (1 - ((x0 - x) + (x1 - x)) / 2); // coverage = 1 - average x position
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float TrapezoidArea(float height, float top_width, float bottom_width)
        {
            //Debug.Assert(top_width >= 0);
            //Debug.Assert(bottom_width >= 0);
            return (top_width + bottom_width) / 2.0f * height;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float TriangleArea(float height, float width)
        {
            return 0.5f * height * width;
        }

        static void ClearArray<T>(NativeArray<T> array, int start, int end) where T : struct
        {
            var length = math.min(end, array.Length);
            for (int i = start; i < length; ++i)
                array[i] = default(T);
        }

        static int AddActiveEdge(NativeList<ActiveEdge> hh, Edge e, int off_x, float scanYTop)
        {
            var ae = new ActiveEdge();
            float dxdy = (e.x1 - e.x0) / (e.y1 - e.y0);

            ae.dxdy = dxdy;
            ae.dydx = dxdy != 0.0f ? (1.0f / dxdy) : 0.0f;
            ae.x = e.x0 + dxdy * (scanYTop - e.y0);
            ae.x -= off_x;
            ae.dir = e.invert ? 1.0f : -1.0f;
            ae.y0 = e.y0;
            ae.y1 = e.y1;
            //Debug.Assert(e.y1 > e.y0);
            ae.nextID = -1;
            hh.Add(ae);
            return hh.Length - 1;
        }
        public struct EdgeYMaxComparer : IComparer<Edge>
        {
            public int Compare(Edge a, Edge b)
            {
                return a.y0.CompareTo(b.y0);
            }
        }
    }
    internal struct Edge
    {
        public float x0;
        public float y0;
        public float x1;
        public float y1;
        public bool invert;
    }

    internal struct ActiveEdge
    {
        public int nextID;
        public float x;
        public float dxdy;
        public float dydx;
        public float dir;
        public float y0;
        public float y1;
    }
}
