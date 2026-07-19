using Unity.Collections;
using Unity.Mathematics;
using Unity.Profiling;

namespace Latios.Calligraphics.HarfBuzz
{
    internal static class PaintUtils
    {
        public static readonly ProfilerMarker rasterizeCOLRMarker  = new ProfilerMarker("Rasterize COLR");
        public static readonly ProfilerMarker rasterizeSDFMarker   = new ProfilerMarker("Rasterize SDF");
        public static readonly ProfilerMarker removeOverlapsMarker = new ProfilerMarker("Remove Overlaps");
        public static readonly ProfilerMarker blendMarker          = new ProfilerMarker("Blend");

        public static readonly ProfilerMarker paintMarker = new ProfilerMarker("Paint");

        public readonly static float2x3 AffineTransformIdentity = new float2x3 {
            c0                                                  = new float2(1, 0),  // xx, yx
            c1                                                  = new float2(0, 1),  // xy, yy
            c2                                                  = new float2(0, 0)
        };  // x0, y0

        public static void BlitRawTexture(NativeArray<ColorBGRA> src, int srcWidth, int srcHeight,  NativeArray<ColorBGRA> dest, int dstWidth, int dstHeight, int destX, int destY)
        {
            for (int y = 0; y < srcHeight; y++)
                NativeArray<ColorBGRA>.Copy(src, y * srcWidth, dest, (destY + y) * dstWidth + destX, srcWidth);
        }
        public static void SetBlack(NativeArray<ColorBGRA> result)
        {
            var color = new ColorBGRA(0, 0, 0, 255);
            for (int i = 0; i < result.Length; i++)
                result[i] = color;
        }
    }
}

