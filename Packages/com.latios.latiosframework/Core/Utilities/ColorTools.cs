using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Latios
{
    public static class ColorExtensions
    {
        public static float4 ToFloat4(this Color color)
        {
            return new float4(color.r, color.g, color.b, color.a);
        }

        public static half4 ToHalf4(this Color color)
        {
            return new half4(color.ToFloat4());
        }

        public static float4 ToFloat4(this Color32 color)
        {
            return ((Color)color).ToFloat4();
        }

        public static half4 ToHalf4(this Color32 color)
        {
            return ((Color)color).ToHalf4();
        }
    }

    /// <summary>
    /// Conversions between various color spaces
    /// </summary>
    public static class ColorTools
    {
        /// <summary>
        /// Converts an sRGB-encoded value to linear light using the exact piecewise sRGB transfer
        /// function, matching UnityEngine.Mathf.GammaToLinearSpace.
        /// </summary>
        public static float SrgbToLinear(float srgb)
        {
            return srgb <= 0.04045f ? srgb / 12.92f : math.pow((srgb + 0.055f) / 1.055f, 2.4f);
        }

        /// <summary>
        /// Converts an sRGB-encoded value to linear light using a cubic approximation of the sRGB
        /// transfer function.
        /// </summary>
        /// <remarks>
        /// Two fused multiply-adds and a multiply, with no transcendentals and no branch, so it
        /// vectorizes where <see cref="SrgbToLinear(float)"/> does not. Worst error against the exact
        /// curve is 0.0017, which is under half of an 8-bit quantization step, and 0 and 1 both map
        /// exactly so black stays black and white stays white. Values outside 0 to 1 are not
        /// meaningful; the polynomial diverges from the curve beyond that range.
        /// </remarks>
        public static float SrgbToLinearFast(float srgb)
        {
            return srgb * (srgb * (srgb * 0.305306011f + 0.682171111f) + 0.012522878f);
        }
    }
}

