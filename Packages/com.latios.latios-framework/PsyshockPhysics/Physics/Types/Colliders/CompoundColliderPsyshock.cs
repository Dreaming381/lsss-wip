using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;

namespace Latios.Psyshock
{
    /// <summary>
    /// A uniformly scaled collection of colliders and their relative transforms.
    /// </summary>
    [Serializable]
    [StructLayout(LayoutKind.Explicit)]
    public struct CompoundCollider
    {
        public enum StretchMode : byte
        {
            RotateStretchLocally = 0,
            IgnoreStretch = 1,
            StretchPositionsOnly = 2
        }

        [FieldOffset(0)] public BlobAssetReference<CompoundColliderBlob> compoundColliderBlob;
        [FieldOffset(8)] public float                                    scale;
        [FieldOffset(12)] public float3                                  stretch;  // Todo: Alignment?
        [FieldOffset(24)] public StretchMode                             stretchMode;

        public CompoundCollider(BlobAssetReference<CompoundColliderBlob> compoundColliderBlob, float scale = 1f, StretchMode stretchMode = StretchMode.RotateStretchLocally)
        {
            this.compoundColliderBlob = compoundColliderBlob;
            this.scale                = scale;
            this.stretch              = 1f;
            this.stretchMode          = stretchMode;
        }

        public CompoundCollider(BlobAssetReference<CompoundColliderBlob> compoundColliderBlob,
                                float scale,
                                float3 stretch,
                                StretchMode stretchMode = StretchMode.RotateStretchLocally)
        {
            this.compoundColliderBlob = compoundColliderBlob;
            this.scale                = scale;
            this.stretch              = stretch;
            this.stretchMode          = stretchMode;
        }

        internal void GetScaledStretchedSubCollider(int index, out Collider blobCollider, out RigidTransform blobTransform)
        {
            ref var blob = ref compoundColliderBlob.Value;
            if (math.all(new float4(stretch, scale) == 1f))
            {
                blobTransform = blob.transforms[index];
                blobCollider  = blob.colliders[index];
                return;
            }

            switch (stretchMode)
            {
                case StretchMode.RotateStretchLocally:
                {
                    blobTransform      = blob.transforms[index];
                    blobTransform.pos *= scale * stretch;
                    var localStretch   = math.InverseRotateFast(blobTransform.rot, stretch);
                    blobCollider       = blob.colliders[index];
                    Physics.ScaleStretchCollider(ref blobCollider, scale, localStretch);
                    break;
                }
                case StretchMode.IgnoreStretch:
                {
                    blobTransform      = blob.transforms[index];
                    blobTransform.pos *= scale;
                    blobCollider       = blob.colliders[index];
                    Physics.ScaleStretchCollider(ref blobCollider, scale, 1f);
                    break;
                }
                case StretchMode.StretchPositionsOnly:
                {
                    blobTransform      = blob.transforms[index];
                    blobTransform.pos *= scale;
                    blobCollider       = blob.colliders[index];
                    Physics.ScaleStretchCollider(ref blobCollider, scale, 1f);
                    break;
                }
                default:
                {
                    blobTransform = default;
                    blobCollider  = default;
                    break;
                }
            }
        }
    }

    internal struct BlobCollider
    {
#pragma warning disable CS0649
        internal float4x4 storage;
#pragma warning restore CS0649
    }

    //Todo: Use some acceleration structure in a future version
    /// <summary>
    /// A blob asset composed of a collection of colliders and their transforms in a unified coordinate space
    /// </summary>
    public struct CompoundColliderBlob
    {
        internal BlobArray<BlobCollider> blobColliders;
        public BlobArray<RigidTransform> transforms;
        public Aabb                      localAabb;

        public unsafe ref BlobArray<Collider> colliders => ref UnsafeUtility.AsRef<BlobArray<Collider> >(UnsafeUtility.AddressOf(ref blobColliders));
        //ref UnsafeUtility.As<BlobArray<BlobCollider>, BlobArray<Collider>>(ref blobColliders);
    }
}

