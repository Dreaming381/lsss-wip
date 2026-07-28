using Latios.Calligraphics.HarfBuzz;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;

namespace Latios.Calligraphics.Systems
{
    public partial struct GenerateGlyphsSystem
    {
        partial struct GenerateRenderGlyphsJob
        {
            static float2 RotatePoint(float2 point, float2 pivot, float sin, float cos)
            {
                float2 translated = point - pivot;
                return new float2(
                    translated.x * cos - translated.y * sin,
                    translated.x * sin + translated.y * cos
                    ) + pivot;
            }

            static half4 GetColorAsHDRHalf4(UnityEngine.Color32 c)
            {
                return new half4(new half(c.r / 255f), new half(c.g / 255f), new half(c.b / 255f), new half(c.a / 255f));
            }
        }
    }
}

