using System.Diagnostics;
using Latios.Transforms;
using Unity.Mathematics;

namespace Latios.Psyshock
{
    public static partial class Physics
    {
        #region Collider vs Collider
        public static bool ColliderCast(in Collider colliderToCast,
                                        in TransformQvvs castStart,
                                        float3 castEnd,
                                        in Collider targetCollider,
                                        in TransformQvvs targetTransform,
                                        out ColliderCastResult result)
        {
            var scaledColliderToCast = colliderToCast;
            var scaledTargetCollider = targetCollider;
            ScaleStretchCollider(ref scaledColliderToCast, castStart.scale,       castStart.stretch);
            ScaleStretchCollider(ref scaledTargetCollider, targetTransform.scale, targetTransform.stretch);
            return ColliderColliderDispatch.ColliderCast(in scaledColliderToCast,
                                                         new RigidTransform(castStart.rotation, castStart.position),
                                                         castEnd,
                                                         in scaledTargetCollider,
                                                         new RigidTransform(targetTransform.rotation, targetTransform.position),
                                                         out result);
        }

        // Todo: Legacy, remove
        public static bool ColliderCast(in Collider colliderToCast,
                                        in RigidTransform castStart,
                                        float3 castEnd,
                                        in Collider targetCollider,
                                        in RigidTransform targetTransform,
                                        out ColliderCastResult result)
        {
            return ColliderColliderDispatch.ColliderCast(in colliderToCast, in castStart, castEnd, in targetCollider, in targetTransform, out result);
        }
        #endregion

        #region Collider vs Layer
        public static bool ColliderCast(in Collider colliderToCast,
                                        in TransformQvvs castStart,
                                        float3 castEnd,
                                        in CollisionLayer layer,
                                        out ColliderCastResult result,
                                        out LayerBodyInfo layerBodyInfo)
        {
            var scaledCastStart      = new RigidTransform(castStart.rotation, castStart.position);
            var scaledColliderToCast = colliderToCast;
            ScaleStretchCollider(ref scaledColliderToCast, castStart.scale, castStart.stretch);

            result        = default;
            layerBodyInfo = default;
            var processor = new LayerQueryProcessors.ColliderCastClosestImmediateProcessor(in scaledColliderToCast, in scaledCastStart, castEnd, ref result, ref layerBodyInfo);
            FindObjects(AabbFrom(in scaledColliderToCast, in scaledCastStart, castEnd), in layer, in processor).RunImmediate();
            var hit                         = result.subColliderIndexOnTarget >= 0;
            result.subColliderIndexOnTarget = math.max(result.subColliderIndexOnTarget, 0);
            return hit;
        }

        public static bool ColliderCastAny(Collider colliderToCast,
                                           in TransformQvvs castStart,
                                           float3 castEnd,
                                           in CollisionLayer layer,
                                           out ColliderCastResult result,
                                           out LayerBodyInfo layerBodyInfo)
        {
            var scaledCastStart      = new RigidTransform(castStart.rotation, castStart.position);
            var scaledColliderToCast = colliderToCast;
            ScaleStretchCollider(ref scaledColliderToCast, castStart.scale, castStart.stretch);

            result        = default;
            layerBodyInfo = default;
            var processor = new LayerQueryProcessors.ColliderCastAnyImmediateProcessor(in scaledColliderToCast, in scaledCastStart, castEnd, ref result, ref layerBodyInfo);
            FindObjects(AabbFrom(in scaledColliderToCast, in scaledCastStart, castEnd), in layer, in processor).RunImmediate();
            var hit                         = result.subColliderIndexOnTarget >= 0;
            result.subColliderIndexOnTarget = math.max(result.subColliderIndexOnTarget, 0);
            return hit;
        }
        #endregion
    }
}

