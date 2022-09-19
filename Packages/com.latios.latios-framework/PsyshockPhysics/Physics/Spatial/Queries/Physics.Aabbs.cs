using System;
using System.Diagnostics;
using Unity.Mathematics;

namespace Latios.Psyshock
{
    public static partial class Physics
    {
        #region Rays
        public static Aabb AabbFrom(Ray ray)
        {
            return new Aabb(math.min(ray.start, ray.end), math.max(ray.start, ray.end));
        }

        public static Aabb AabbFrom(float3 rayStart, float3 rayEnd)
        {
            return new Aabb(math.min(rayStart, rayEnd), math.max(rayStart, rayEnd));
        }
        #endregion

        #region Colliders
        public static Aabb AabbFrom(SphereCollider sphere, RigidTransform transform)
        {
            float3 wc   = math.transform(transform, sphere.center);
            Aabb   aabb = new Aabb(wc - sphere.radius, wc + sphere.radius);
            return aabb;
        }

        public static Aabb AabbFrom(CapsuleCollider capsule, RigidTransform transform)
        {
            float3 a = math.transform(transform, capsule.pointA);
            float3 b = math.transform(transform, capsule.pointB);
            return new Aabb(math.min(a, b) - capsule.radius, math.max(a, b) + capsule.radius);
        }

        public static Aabb AabbFrom(BoxCollider box, RigidTransform transform)
        {
            return TransformAabb(new float4x4(transform), box.center, box.halfSize);
        }

        public static Aabb AabbFrom(TriangleCollider triangle, RigidTransform transform)
        {
            var transformedTriangle = simd.transform(transform, new simdFloat3(triangle.pointA, triangle.pointB, triangle.pointC, triangle.pointA));
            var aabb                = new Aabb(math.min(transformedTriangle.a, transformedTriangle.b), math.max(transformedTriangle.a, transformedTriangle.b));
            return CombineAabb(transformedTriangle.c, aabb);
        }

        public static Aabb AabbFrom(ConvexCollider convex, RigidTransform transform)
        {
            var         local = convex.convexColliderBlob.Value.localAabb;
            float3      c     = (local.min + local.max) / 2f;
            BoxCollider box   = new BoxCollider(c, local.max - c);
            return AabbFrom(ScaleCollider(box, new PhysicsScale(convex.scale)), transform);
        }

        public static Aabb AabbFrom(CompoundCollider compound, RigidTransform transform)
        {
            var         local = compound.compoundColliderBlob.Value.localAabb;
            float3      c     = (local.min + local.max) / 2f;
            BoxCollider box   = new BoxCollider(c, local.max - c);
            return AabbFrom(ScaleCollider(box, new PhysicsScale(compound.scale)), transform);
        }
        #endregion

        #region ColliderCasts
        public static Aabb AabbFrom(SphereCollider sphereToCast, RigidTransform castStart, float3 castEnd)
        {
            var aabbStart = AabbFrom(sphereToCast, castStart);
            var diff      = castEnd - castStart.pos;
            var aabbEnd   = new Aabb(aabbStart.min + diff, aabbStart.max + diff);
            return CombineAabb(aabbStart, aabbEnd);
        }

        public static Aabb AabbFrom(CapsuleCollider capsuleToCast, RigidTransform castStart, float3 castEnd)
        {
            var aabbStart = AabbFrom(capsuleToCast, castStart);
            var diff      = castEnd - castStart.pos;
            var aabbEnd   = new Aabb(aabbStart.min + diff, aabbStart.max + diff);
            return CombineAabb(aabbStart, aabbEnd);
        }

        public static Aabb AabbFrom(BoxCollider boxToCast, RigidTransform castStart, float3 castEnd)
        {
            var aabbStart = AabbFrom(boxToCast, castStart);
            var diff      = castEnd - castStart.pos;
            var aabbEnd   = new Aabb(aabbStart.min + diff, aabbStart.max + diff);
            return CombineAabb(aabbStart, aabbEnd);
        }

        public static Aabb AabbFrom(TriangleCollider triangleToCast, RigidTransform castStart, float3 castEnd)
        {
            var aabbStart = AabbFrom(triangleToCast, castStart);
            var diff      = castEnd - castStart.pos;
            var aabbEnd   = new Aabb(aabbStart.min + diff, aabbStart.max + diff);
            return CombineAabb(aabbStart, aabbEnd);
        }

        public static Aabb AabbFrom(ConvexCollider convexToCast, RigidTransform castStart, float3 castEnd)
        {
            var aabbStart = AabbFrom(convexToCast, castStart);
            var diff      = castEnd - castStart.pos;
            var aabbEnd   = new Aabb(aabbStart.min + diff, aabbStart.max + diff);
            return CombineAabb(aabbStart, aabbEnd);
        }

        public static Aabb AabbFrom(CompoundCollider compoundToCast, RigidTransform castStart, float3 castEnd)
        {
            var aabbStart = AabbFrom(compoundToCast, castStart);
            var diff      = castEnd - castStart.pos;
            var aabbEnd   = new Aabb(aabbStart.min + diff, aabbStart.max + diff);
            return CombineAabb(aabbStart, aabbEnd);
        }
        #endregion

        #region Dispatch
        public static Aabb AabbFrom(Collider collider, RigidTransform transform)
        {
            switch (collider.type)
            {
                case ColliderType.Sphere:
                    SphereCollider sphere = collider;
                    return AabbFrom(sphere, transform);
                case ColliderType.Capsule:
                    CapsuleCollider capsule = collider;
                    return AabbFrom(capsule, transform);
                case ColliderType.Box:
                    BoxCollider box = collider;
                    return AabbFrom(box, transform);
                case ColliderType.Triangle:
                    TriangleCollider triangle = collider;
                    return AabbFrom(triangle, transform);
                case ColliderType.Convex:
                    ConvexCollider convex = collider;
                    return AabbFrom(convex, transform);
                case ColliderType.Compound:
                    CompoundCollider compound = collider;
                    return AabbFrom(compound, transform);
                default:
                    ThrowUnsupportedType();
                    return new Aabb();
            }
        }

        public static Aabb AabbFrom(Collider colliderToCast, RigidTransform castStart, float3 castEnd)
        {
            switch (colliderToCast.type)
            {
                case ColliderType.Sphere:
                    SphereCollider sphere = colliderToCast;
                    return AabbFrom(sphere, castStart, castEnd);
                case ColliderType.Capsule:
                    CapsuleCollider capsule = colliderToCast;
                    return AabbFrom(capsule, castStart, castEnd);
                case ColliderType.Box:
                    BoxCollider box = colliderToCast;
                    return AabbFrom(box, castStart, castEnd);
                case ColliderType.Triangle:
                    TriangleCollider triangle = colliderToCast;
                    return AabbFrom(triangle, castStart, castEnd);
                case ColliderType.Convex:
                    ConvexCollider convex = colliderToCast;
                    return AabbFrom(convex, castStart, castEnd);
                case ColliderType.Compound:
                    CompoundCollider compound = colliderToCast;
                    return AabbFrom(compound, castStart, castEnd);
                default:
                    ThrowUnsupportedType();
                    return new Aabb();
            }
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private static void ThrowUnsupportedType()
        {
            throw new InvalidOperationException("Collider type not supported yet");
        }
        #endregion
    }
}

