using System.IO;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Latios.Calligraphics.HarfBuzz.Bitmap
{
    internal static class SDFCommon
    {
        public static void ClearArray<T>(this NativeArray<T> array) where T : unmanaged
        {
            unsafe
            {
                UnsafeUtility.MemClear(array.GetUnsafePtr(), (long)array.Length * sizeof(T));
            }
        }
        // SPREAD represents the number of texels away from the closest edge before the texel values saturate.
        // 8 bit: distance can be from -128 (outside) to +127 (inside) --> store in 8 bit alpha channel.
        // 16 bit: distance can be from -32,768 (outside) to +32,767 (inside) -->store in 16 bit alpha
        // When converting to 8 bit alpha, we add SPREAD to give distances from 0..2*SPREAD, and multiply
        // by (256/(2*SPREAD ) via this line of code in GetAlphaTexture():
        // var scaleTo8Bit = 256 / (spread * 2);
        public const int DEFAULT_SPREAD       = 8;  // SPREAD and Atlas padding are related, but do not set SPREAD too small
        public const int MIN_SPREAD           = 2;
        public const int MAX_SPREAD           = 32;
        public const int MAX_NEWTON_STEPS     = 4;
        public const int MAX_NEWTON_DIVISIONS = 4;
        public const int FT_TRIG_SAFE_MSB     = 29;

        public static void FinalPass(
            NativeArray<float> distances,
            NativeArray<int> signs,
            int spread, int atlasRectWidth, int atlasRectHeight, bool isHole = false)
        {
            var outSideSign = isHole ? 1 : -1;
            for (int row = 0; row < atlasRectHeight; row++)
            {
                /* We assume the starting pixel of each row is outside. */
                int current_sign = outSideSign;
                for (int column = 0; column < atlasRectWidth; column++)
                {
                    var sourceIndex = atlasRectWidth * row + column;

                    var distance = distances[sourceIndex];
                    var sign     = signs[sourceIndex];

                    // if the pixel is not set
                    // its shortest distance is more than `spread`
                    // so just clamp distance to spread...
                    // sign can be ignore as it is not needed anymore after this method)
                    if (sign == 0)
                        distance = spread;
                    else
                        current_sign = sign;

                    /* clamp the values */
                    distance = math.select(distance, spread, distance > spread);

                    // determine if distance is inside(+) or outside(-)
                    distance *= current_sign;

                    distances[sourceIndex] = distance;  //store the final distance which will be used by GetAlphaTexture
                }
            }
        }

        public static void GetAlphaTexture(
            NativeArray<float> distances,
            NativeArray<byte> buffer,
            int spread, int atlasX, int atlasY, int atlasRectWidth, int atlasRectHeight, int atlasWidth, int atlasHeight)
        {
            float scaleTo8Bit = 255f / (spread * 2);

            if (buffer.Length == distances.Length && buffer.Length == atlasRectWidth * atlasRectHeight)
            {
                for (int i = 0; i < buffer.Length; i++)
                {
                    var result = (distances[i] + spread) * scaleTo8Bit;
                    //var result = math.clamp(distances[i] / spread, -1f, 1f) * 127f + 127f;
                    buffer[i] = (byte)result;
                }
            }
            else
            {
                for (int row = 0, rowEnd = math.min(atlasRectHeight, atlasHeight); row < rowEnd; row++)
                {
                    for (int column = 0, columnEnd = math.min(atlasRectWidth, atlasWidth); column < columnEnd; column++)
                    {
                        var sourceIndex = atlasRectWidth * row + column;
                        var targetIndex = (atlasWidth * (row + atlasY)) + (column + atlasX);

                        // convert to byte range of alpha8 texture
                        var result = (distances[sourceIndex] + spread) * scaleTo8Bit;
                        //var result          = math.clamp(distances[sourceIndex] / spread, -1f, 1f) * 127f + 127f;
                        buffer[targetIndex] = (byte)result;
                    }
                }
            }
        }

        public static void GetAlphaTexture(
            NativeArray<float> distances,
            NativeArray<ushort> buffer,
            int spread, int atlasX, int atlasY, int atlasRectWidth, int atlasRectHeight, int atlasWidth, int atlasHeight)
        {
            float scaleTo16Bit = 65535f / (spread * 2);

            if (buffer.Length == distances.Length && buffer.Length == atlasRectWidth * atlasRectHeight)
            {
                for (int i = 0; i < buffer.Length; i++)
                {
                    var result = (distances[i] + spread) * scaleTo16Bit;
                    buffer[i]  = (ushort)result;
                }
            }
            else
            {
                for (int row = 0, rowEnd = math.min(atlasRectHeight, atlasHeight); row < rowEnd; row++)
                {
                    for (int column = 0, columnEnd = math.min(atlasRectWidth, atlasWidth); column < columnEnd; column++)
                    {
                        var sourceIndex = atlasRectWidth * row + column;
                        var targetIndex = (atlasWidth * (row + atlasY)) + (column + atlasX);

                        // convert to byte range of alpha16 texture
                        var result          = (distances[sourceIndex] + spread) * scaleTo16Bit;
                        buffer[targetIndex] = (ushort)result;
                    }
                }
            }
        }

        public static void MergeSDF(
            NativeArray<float> destinationDistances,
            NativeArray<float> destinationCrosses,
            NativeArray<int>   destinationSigns,
            NativeArray<float> sourceDistances,
            NativeArray<float> sourceCrosses,
            NativeArray<int>   sourceSigns,
            bool isHole)
        {
            if (isHole)
            {
                for (int i = 0, ii = sourceDistances.Length; i < ii; i++)
                {
                    var condition = sourceDistances[i] < destinationDistances[i];
                    {
                        destinationDistances[i] = math.select(destinationDistances[i], sourceDistances[i], condition);
                        destinationCrosses[i]   = math.select(destinationCrosses[i], sourceCrosses[i], condition);
                        destinationSigns[i]     = math.select(destinationSigns[i], sourceSigns[i], condition);
                    }
                }
            }
            else
            {
                for (int i = 0, ii = sourceDistances.Length; i < ii; i++)
                {
                    var condition           = sourceDistances[i] > destinationDistances[i];
                    destinationDistances[i] = math.select(destinationDistances[i], sourceDistances[i], condition);
                    destinationCrosses[i]   = math.select(destinationCrosses[i], sourceCrosses[i], condition);
                    destinationSigns[i]     = math.select(destinationSigns[i], sourceSigns[i], condition);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void GetTarget_DistanceCrossSign(
            NativeArray<float> distances,
            NativeArray<float> crosses,
            NativeArray<int> signs,
            int index, out float targetDistance, out float targetCross, out int targetSign)
        {
            targetDistance = distances[index];
            targetCross    = crosses[index];
            targetSign     = signs[index];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetTarget_DistanceCrossSign(
            NativeArray<float> distances,
            NativeArray<float> crosses,
            NativeArray<int> signs,
            int index, ref float validDistance, ref float validCross, ref int validSign)
        {
            distances[index] = validDistance;
            crosses[index]   = validCross;
            signs[index]     = validSign;
        }

        /// <summary> legacy method provides early out to skip many ops </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ValidateDistanceCrossSign(
            ref float distance,
            ref float cross,
            ref int sign,
            ref float targetDistance,
            ref float targetCross,
            ref int targetSign,
            float spread,
            out float validDistance,
            out float validCross,
            out int validSign
            )
        {
            if (distance > spread)
            {
                validDistance = targetDistance;
                validCross    = targetCross;
                validSign     = targetSign;
                return;
            }
            if (targetSign == 0)  // check if the pixel is already set
            {
                validDistance = distance;
                validCross    = cross;
                validSign     = sign;
                return;
            }
            else
            {
                if (BezierMath.EqualsForLargeValues(targetDistance, distance))
                {
                    var condition = math.abs(cross) > math.abs(targetCross);
                    validDistance = math.select(targetDistance, distance, condition);
                    validCross    = math.select(targetCross, cross, condition);
                    validSign     = math.select(targetSign, sign, condition);
                    return;
                }
                else if (targetDistance > distance)
                {
                    validDistance = distance;
                    validCross    = cross;
                    validSign     = sign;
                    return;
                }
            }
            validDistance = targetDistance;
            validCross    = targetCross;
            validSign     = targetSign;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void GetMinDistanceLineToPoint(float ax, float ay, float bx, float by, float px, float py, out float distance, out float cross, out int sign)
        {
            var abx        = bx - ax;  // Vector from A to B
            var aby        = by - ay;  // Vector from A to B
            var apx        = px - ax;  // Vector from A to P
            var apy        = py - ay;  // Vector from A to P
            var abLengthSq = abx * abx + aby * aby;  // squared distance from A to B
            var abLength   = math.sqrt(abLengthSq);  // normalized distance from A to B
            var frac       = abx * apx + aby * apy;
            frac           = math.max(frac, 0.0f);  // Check if P projection is over vectorAB
            frac           = math.min(frac, abLengthSq);  // Check if P projection is over vectorAB

            frac   /= abLengthSq;  // The normalized "distance" from a to your closest point
            var nx  = ax + abx * frac;  // nearest point on egde
            var ny  = ay + aby * frac;  // nearest point on egde

            var npx        = px - nx;  // Vector from nearest point to P
            var npy        = py - ny;  // Vector from nearest point to P
            var npLengthSq = npx * npx + npy * npy;  // squared distance from nearest point to P
            var npLength   = math.sqrt(npLengthSq);  // normalized distance from nearest point to P

            var abxNorm = abx / abLength;
            var abyNorm = aby / abLength;
            var npxNorm = npx / npLength;
            var npyNorm = npy / npLength;

            // cross of normalized vector A--B with nP->P.
            // positive if the points A, B, and P occur in counterclockwise order
            // (CCW, P lies to the left of the vector from A to B).
            // negative if they occur in clockwise order
            // (CW, P lies to the right of the vector from A to B).
            // this result is identical with ORIENT2D
            // the sign of the cross is used determine the sign of the distance
            // so this here is the heart of the SDF renderer
            // all sign flips to determine what is filled due to
            // different definitions of polygons in Postscript and TrueType should be done elsewhere
            cross    = BezierMath.cross2D(abxNorm, abyNorm, npxNorm, npyNorm);
            sign     = math.select(1, -1, cross < 0);
            distance = npLength;

            var isEndPoint = math.abs(frac) < BezierMath.epsilon1Float_abs | BezierMath.EqualsForSmallValues(frac, 1, BezierMath.epsilon1Float_abs);
            cross          = math.select(1, cross, isEndPoint);
        }

        /// /// <summary>
        /// positive area = CCW, negative area = CW (works for closed and open polygon (identical result))
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SignedArea(NativeList<SDFEdge> data, int start, int end)
        {
            float area = default;
            for (int i = start, prev = end - 1; i < end; prev = i++) //from (0, prev) until (end, prev)
                area += (data[prev].start_pos.x - data[i].start_pos.x) * (data[i].start_pos.y + data[prev].start_pos.y);
            return area * 0.5f;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PolyOrientation GetPolyOrientation(double signedArea)
        {
            if (signedArea < 0)
                return PolyOrientation.CW;
            else if (signedArea > 0)
                return PolyOrientation.CCW;
            else
                return PolyOrientation.None;
        }
        public enum PolyOrientation : byte
        {
            CW = 0,
            CCW = 1,
            None = 2,
        }

        public static void WriteGlyphOutlineToFile(string path, NativeList<SDFEdge> edges)
        {
            if(edges.Length == 0)
                return;
            StreamWriter writer = new StreamWriter(path, false);
            var          edge   = edges[0];
            writer.WriteLine($"{edge.start_pos.x} {edge.start_pos.y}");
            for (int i = 0, end = edges.Length; i < end; i++)
            {
                edge = edges[i];
                writer.WriteLine($"{edge.end_pos.x} {edge.end_pos.y}");
            }
            writer.WriteLine();
            writer.Close();
        }

        public static void WriteGlyphOutlineToFile(string path, DrawData drawData)
        {
            var edges = drawData.edges;
            if (edges.Length == 0)
                return;
            var startIDs = drawData.contourIDs;

            StreamWriter writer = new StreamWriter(path, false);
            for (int i = 0, ii = startIDs.Length - 1; i < ii; i++)
            {
                var startID     = startIDs[i];
                var nextStartID = startIDs[i + 1];
                for (int k = startID; k < nextStartID; k++)
                {
                    var edge = edges[k];
                    writer.WriteLine($"{edge.start_pos.x} {edge.start_pos.y}");
                }
                writer.WriteLine($"{edges[nextStartID-1].end_pos.x} {edges[nextStartID - 1].end_pos.y}");
                writer.WriteLine();
            }

            //var edge = edges[0];
            //writer.WriteLine($"{edge.start_pos.x} {edge.start_pos.y}");
            //for (int i = 0, end = edges.Length; i < end; i++)
            //{
            //    edge = edges[i];
            //    writer.WriteLine($"{edge.end_pos.x} {edge.end_pos.y}");
            //}

            writer.Close();
        }

        public static void WriteGlyphOutlineToFile(string path, NativeList<Edge> edges)
        {
            if (edges.Length == 0)
                return;
            StreamWriter writer = new StreamWriter(path, false);
            var          edge   = edges[0];

            for (int i = 0, end = edges.Length; i < end; i++)
            {
                edge = edges[i];
                writer.WriteLine($"{edge.x0} {edge.y0} {edge.invert}");
                writer.WriteLine($"{edge.x1} {edge.y1}");
                writer.WriteLine();
            }
            writer.WriteLine();
            writer.Close();
        }
        public static void WriteMinDistancesToFile(string path, in NativeArray<SDFDebug> sdfDebug)
        {
            if (sdfDebug.Length == 0)
                return;
            StreamWriter writer = new StreamWriter(path, false);
            for (int i = 0, end = sdfDebug.Length; i < end; i++)
            {
                writer.WriteLine($"{sdfDebug[i]}");
            }
            writer.WriteLine();
            writer.Close();
        }
        public static void WriteArrayToFile(string path, in NativeArray<float> array, int arrayWidth, int row)
        {
            if (array.Length == 0)
                return;
            StreamWriter writer = new StreamWriter(path, false);
            var          start  = arrayWidth * row;
            var          end    = start + arrayWidth;
            for (int i = start; i < end; i++)
            {
                writer.WriteLine($"{array[i]}");
            }
            writer.WriteLine();
            writer.Close();
        }
        public static void WriteArrayToFile(string path, in NativeArray<byte> array, int arrayWidth, int row)
        {
            if (array.Length == 0)
                return;
            StreamWriter writer = new StreamWriter(path, false);
            var          start  = arrayWidth * row;
            var          end    = start + arrayWidth;
            for (int i = start; i < end; i++)
            {
                writer.WriteLine($"{array[i]}");
            }
            writer.WriteLine();
            writer.Close();
        }
        public static void WriteArrayToFile(string path, in NativeArray<byte> array)
        {
            if (array.Length == 0)
                return;
            StreamWriter writer = new StreamWriter(path, false);
            for (int i = 0, end = array.Length; i < end; i++)
            {
                writer.WriteLine($"{array[i]}");
            }
            writer.WriteLine();
            writer.Close();
        }
        public static void WriteArrayToFile(string path, in NativeArray<int> array)
        {
            if (array.Length == 0)
                return;
            StreamWriter writer = new StreamWriter(path, false);
            for (int i = 0, end = array.Length; i < end; i++)
            {
                writer.WriteLine($"{array[i]}");
            }
            writer.WriteLine();
            writer.Close();
        }

        public static void WriteSDFDebugToFile(string path, NativeArray<SDFDebug> sdfDebug)
        {
            if (sdfDebug.Length == 0)
                return;
            StreamWriter writer = new StreamWriter(path, false);
            for (int i = 0, end = sdfDebug.Length; i < end; i++)
            {
                var c = sdfDebug[i];
                writer.WriteLine($"{c.row} {c.column} {c.distanceRaw} {c.signRaw} {c.currentSignRaw} {c.distance} {c.sign} {c.currentSign} {c.cross}");
            }
            writer.WriteLine();
            writer.Close();
        }
        public static void WriteGlyphOutlineToFile(string path, ref DrawData drawData, bool fullBezier = false)
        {
            var edges      = drawData.edges;
            var contourIDs = drawData.contourIDs;
            if (contourIDs.Length < 2 || edges.Length == 0)
                return;

            StreamWriter writer = new StreamWriter(path, false);
            SDFEdge      edge;
            for (int contourID = 0, end = contourIDs.Length - 1; contourID < end; contourID++)  //for each contour
            {
                int startID     = contourIDs[contourID];
                int nextStartID = contourIDs[contourID + 1];
                for (int edgeID = startID; edgeID < nextStartID; edgeID++)  //for each edge
                {
                    edge = edges[edgeID];
                    if(fullBezier)
                        writer.WriteLine($"{edge.start_pos.x} {edge.start_pos.y} {edge.control1.x} {edge.control1.y} {edge.end_pos.x} {edge.end_pos.y} {edge.edge_type}");
                    else
                        writer.WriteLine($"{edge.start_pos.x} {edge.start_pos.y}");
                }
                writer.WriteLine();
            }
            writer.Close();
        }
    }
    public struct SDFDebug
    {
        public int   row;
        public int   column;
        public float distanceRaw;
        public int   signRaw;
        public float distance;
        public int   sign;
        public float cross;
        public int   currentSignRaw;
        public int   currentSign;
        public SDFDebug(int row, int column, float distanceRaw, int signRaw, int currentSignRaw, float cross)
        {
            this.row            = row;
            this.column         = column;
            this.distanceRaw    = distanceRaw;
            this.signRaw        = signRaw;
            this.currentSignRaw = currentSignRaw;
            this.distance       = float.MinValue;
            this.sign           = int.MinValue;
            this.cross          = cross;
            this.currentSign    = 0;
        }
    }
}

