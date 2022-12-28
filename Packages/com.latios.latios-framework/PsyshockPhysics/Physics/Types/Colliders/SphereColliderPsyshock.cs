using System;
using Unity.Mathematics;

namespace Latios.Psyshock
{
    /// <summary>
    /// A sphere defined by a center and a radius
    /// </summary>
    [Serializable]
    public struct SphereCollider
    {
        public enum StretchMode : byte
        {
            StretchCenter = 0,
            IgnoreStretch = 1,
        }

        public float3      center;
        public float       radius;
        public StretchMode stretchMode;

        public SphereCollider(float3 center, float radius, StretchMode stretchMode = StretchMode.StretchCenter)
        {
            this.center      = center;
            this.radius      = radius;
            this.stretchMode = stretchMode;
        }
    }
}

