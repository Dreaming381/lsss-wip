using Unity.Mathematics;

namespace Latios.Psyshock
{
    public static partial class Physics
    {
        #region Sphere
        public static bool ColliderCast(SphereCollider sphereToCast,
                                        RigidTransform castStart,
                                        float3 castEnd,
                                        SphereCollider targetSphere,
                                        RigidTransform targetSphereTransform,
                                        out ColliderCastResult result)
        {
            var cso     = targetSphere;
            cso.radius += sphereToCast.radius;
            var  start  = math.transform(castStart, sphereToCast.center);
            var  ray    = new Ray(start, start + castEnd - castStart.pos);
            bool hit    = Raycast(ray, cso, targetSphereTransform, out var raycastResult);
            if (hit)
            {
                var hitTransform  = castStart;
                hitTransform.pos += raycastResult.position - start;
                DistanceBetween(sphereToCast, hitTransform, targetSphere, targetSphereTransform, 1f, out var distanceResult);
                result = new ColliderCastResult
                {
                    hitpointOnCaster         = distanceResult.hitpointA,
                    hitpointOnTarget         = distanceResult.hitpointB,
                    normalOnSweep            = distanceResult.normalA,
                    normalOnTarget           = distanceResult.normalB,
                    subColliderIndexOnCaster = distanceResult.subColliderIndexA,
                    subColliderIndexOnTarget = distanceResult.subColliderIndexB,
                    distance                 = math.distance(hitTransform.pos, castStart.pos)
                };
                return true;
            }
            result = default;
            return false;
        }

        public static bool ColliderCast(SphereCollider sphereToCast,
                                        RigidTransform castStart,
                                        float3 castEnd,
                                        CapsuleCollider targetCapsule,
                                        RigidTransform targetCapsuleTransform,
                                        out ColliderCastResult result)
        {
            var cso     = targetCapsule;
            cso.radius += sphereToCast.radius;
            var  start  = math.transform(castStart, sphereToCast.center);
            var  ray    = new Ray(start, start + castEnd - castStart.pos);
            bool hit    = Raycast(ray, cso, targetCapsuleTransform, out var raycastResult);
            if (hit)
            {
                var hitTransform  = castStart;
                hitTransform.pos += raycastResult.position - start;
                DistanceBetween(sphereToCast, hitTransform, targetCapsule, targetCapsuleTransform, 1f, out var distanceResult);
                result = new ColliderCastResult
                {
                    hitpointOnCaster         = distanceResult.hitpointA,
                    hitpointOnTarget         = distanceResult.hitpointB,
                    normalOnSweep            = distanceResult.normalA,
                    normalOnTarget           = distanceResult.normalB,
                    subColliderIndexOnCaster = distanceResult.subColliderIndexA,
                    subColliderIndexOnTarget = distanceResult.subColliderIndexB,
                    distance                 = math.distance(hitTransform.pos, castStart.pos)
                };
                return true;
            }
            result = default;
            return false;
        }

        public static bool ColliderCast(SphereCollider sphereToCast,
                                        RigidTransform castStart,
                                        float3 castEnd,
                                        BoxCollider targetBox,
                                        RigidTransform targetBoxTransform,
                                        out ColliderCastResult result)
        {
            var  start               = math.transform(castStart, sphereToCast.center);
            var  casterInTargetSpace = math.mul(math.inverse(targetBoxTransform), castStart);
            var  ray                 = new Ray(math.transform(casterInTargetSpace, start), math.transform(casterInTargetSpace, start + castEnd - castStart.pos));
            bool hit                 = Raycasting.RaycastRoundedBox(ray, targetBox, sphereToCast.radius, out var fraction, out var normal);
            if (hit)
            {
                var hitTransform = castStart;
                hitTransform.pos = math.lerp(castStart.pos, castEnd, fraction);
                DistanceBetween(sphereToCast, hitTransform, targetBox, targetBoxTransform, 1f, out var distanceResult);
                result = new ColliderCastResult
                {
                    hitpointOnCaster         = distanceResult.hitpointA,
                    hitpointOnTarget         = distanceResult.hitpointB,
                    normalOnSweep            = distanceResult.normalA,
                    normalOnTarget           = distanceResult.normalB,
                    subColliderIndexOnCaster = distanceResult.subColliderIndexA,
                    subColliderIndexOnTarget = distanceResult.subColliderIndexB,
                    distance                 = math.distance(hitTransform.pos, castStart.pos)
                };
                return true;
            }
            result = default;
            return false;
        }

        public static bool ColliderCast(SphereCollider sphereToCast,
                                        RigidTransform castStart,
                                        float3 castEnd,
                                        CompoundCollider targetCompound,
                                        RigidTransform targetCompoundTransform,
                                        out ColliderCastResult result)
        {
            bool hit              = false;
            result                = default;
            result.distance       = float.MaxValue;
            ref var blob          = ref targetCompound.compoundColliderBlob.Value;
            var     compoundScale = new PhysicsScale { scale = targetCompound.scale, state = PhysicsScale.State.Uniform };
            for (int i = 0; i < blob.colliders.Length; i++)
            {
                var blobTransform  = blob.transforms[i];
                blobTransform.pos *= targetCompound.scale;
                bool newHit        = ColliderCast(sphereToCast, castStart, castEnd, ScaleCollider(blob.colliders[i], compoundScale),
                                                  math.mul(targetCompoundTransform, blobTransform),
                                                  out var newResult);

                newResult.subColliderIndexOnTarget  = i;
                newHit                             &= newResult.distance < result.distance;
                hit                                |= newHit;
                result                              = newHit ? newResult : result;
            }
            return hit;
        }
        #endregion

        #region Capsule
        public static bool ColliderCast(CapsuleCollider capsuleToCast,
                                        RigidTransform castStart,
                                        float3 castEnd,
                                        SphereCollider targetSphere,
                                        RigidTransform targetSphereTransform,
                                        out ColliderCastResult result)
        {
            var cso           = capsuleToCast;
            cso.radius       += targetSphere.radius;
            var  castReverse  = castStart.pos - castEnd;
            var  start        = math.transform(targetSphereTransform, targetSphere.center);
            var  ray          = new Ray(start, start + castReverse);
            bool hit          = Raycast(ray, cso, targetSphereTransform, out var raycastResult);
            if (hit)
            {
                var hitTransform  = castStart;
                hitTransform.pos -= castReverse - (raycastResult.position - start);
                DistanceBetween(capsuleToCast, hitTransform, targetSphere, targetSphereTransform, 1f, out var distanceResult);
                result = new ColliderCastResult
                {
                    hitpointOnCaster         = distanceResult.hitpointA,
                    hitpointOnTarget         = distanceResult.hitpointB,
                    normalOnSweep            = distanceResult.normalA,
                    normalOnTarget           = distanceResult.normalB,
                    subColliderIndexOnCaster = distanceResult.subColliderIndexA,
                    subColliderIndexOnTarget = distanceResult.subColliderIndexB,
                    distance                 = math.distance(hitTransform.pos, castStart.pos)
                };
                return true;
            }
            result = default;
            return false;
        }

        #endregion

        #region Box
        public static bool ColliderCast(BoxCollider boxToCast,
                                        RigidTransform castStart,
                                        float3 castEnd,
                                        SphereCollider targetSphere,
                                        RigidTransform targetSphereTransform,
                                        out ColliderCastResult result)
        {
            var  castReverse        = castStart.pos - castEnd;
            var  worldToCasterSpace = math.inverse(castStart);
            var  start              = math.transform(targetSphereTransform, targetSphere.center);
            var  ray                = new Ray(math.transform(worldToCasterSpace, start), math.transform(worldToCasterSpace, start + castReverse));
            bool hit                = Raycasting.RaycastRoundedBox(ray, boxToCast, targetSphere.radius, out var fraction, out var normal);
            if (hit)
            {
                var hitTransform = castStart;
                hitTransform.pos = math.lerp(castEnd, castStart.pos, fraction);
                DistanceBetween(boxToCast, hitTransform, targetSphere, targetSphereTransform, 1f, out var distanceResult);
                result = new ColliderCastResult
                {
                    hitpointOnCaster         = distanceResult.hitpointA,
                    hitpointOnTarget         = distanceResult.hitpointB,
                    normalOnSweep            = distanceResult.normalA,
                    normalOnTarget           = distanceResult.normalB,
                    subColliderIndexOnCaster = distanceResult.subColliderIndexA,
                    subColliderIndexOnTarget = distanceResult.subColliderIndexB,
                    distance                 = math.distance(hitTransform.pos, castStart.pos)
                };
                return true;
            }
            result = default;
            return false;
        }
        #endregion

        #region Compound
        public static bool ColliderCast(CompoundCollider compountToCast,
                                        RigidTransform castStart,
                                        float3 castEnd,
                                        SphereCollider targetSphere,
                                        RigidTransform targetSphereTransform,
                                        out ColliderCastResult result)
        {
            bool hit              = false;
            result                = default;
            result.distance       = float.MaxValue;
            ref var blob          = ref compountToCast.compoundColliderBlob.Value;
            var     compoundScale = new PhysicsScale { scale = compountToCast.scale, state = PhysicsScale.State.Uniform };
            for (int i = 0; i < blob.colliders.Length; i++)
            {
                var blobTransform  = blob.transforms[i];
                blobTransform.pos *= compountToCast.scale;
                var  start         = math.mul(castStart, blobTransform);
                bool newHit        = ColliderCast(ScaleCollider(blob.colliders[i], compoundScale),
                                                  start, start.pos + (castEnd - castStart.pos),
                                                  targetSphere,
                                                  targetSphereTransform,
                                                  out var newResult);

                newResult.subColliderIndexOnCaster  = i;
                newHit                             &= newResult.distance < result.distance;
                hit                                |= newHit;
                result                              = newHit ? newResult : result;
            }
            return hit;
        }
        #endregion
    }
}

