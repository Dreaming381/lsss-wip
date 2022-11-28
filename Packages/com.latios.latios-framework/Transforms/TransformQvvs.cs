using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace Latios.Transforms
{
    [StructLayout(LayoutKind.Explicit, Size = 48)]
    public struct TransformQvvs
    {
        [FieldOffset(0)] public quaternion rotation;
        [FieldOffset(16)] public float3    position;
        [FieldOffset(28)] public int       worldIndex;  // User-define-able, can be used for floating origin or something
        [FieldOffset(32)] public float3    stretch;
        [FieldOffset(44)] public float     scale;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct TransformQvs
    {
        [FieldOffset(0)] public quaternion rotation;
        [FieldOffset(16)] public float3    position;
        [FieldOffset(28)] public float     scale;
    }

    public static class qvvs
    {
        // Assuming B represents a transform in A's space, this converts B into the same space
        // A resides in
        public static TransformQvvs mul(in TransformQvvs a, in TransformQvvs b)
        {
            return new TransformQvvs
            {
                rotation   = math.mul(a.rotation, b.rotation),
                position   = a.position + math.rotate(a.rotation, b.position * a.stretch * a.scale),  // We scale by A's stretch and scale because we are leaving A's space
                worldIndex = a.worldIndex,  // We inherit A's index because we were relative to A
                stretch    = b.stretch,  // We retain B's stretch as the result is B in a different coordinate space
                scale      = a.scale * b.scale
            };
        }

        // Assuming B represents a transform in A's space, this converts B into the same space
        // A resides in, where bStretch is forwarded from
        public static void mul(ref TransformQvvs bWorld, in TransformQvvs a, TransformQvs b)
        {
            bWorld.rotation = math.mul(a.rotation, b.rotation);
            bWorld.position = a.position + math.rotate(a.rotation, b.position * a.stretch * a.scale);
            // bWorld.worldIndex is preserved
            // bWorld.stretch is preserved
            bWorld.scale = a.scale * b.scale;
        }

        public static float3x4 ToMatrix(ref this TransformQvvs transform)
        {
            return float3x4.TRS(transform.position, transform.rotation, transform.scale * transform.stretch);
        }
    }
}

