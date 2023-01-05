using System;
using Latios.Transforms;
using Unity.Mathematics;

namespace Latios.Psyshock
{
    public static partial class Physics
    {
        #region Point vs Collider
        public static bool DistanceBetween(float3 point, in Collider collider, in TransformQvvs transform, float maxDistance, out PointDistanceResult result)
        {
            return PointRayDispatch.DistanceBetween(point, in collider, in transform, maxDistance, out result);
        }
        #endregion

        #region Point vs Layer
        public static bool DistanceBetween(float3 point, in CollisionLayer layer, float maxDistance, out PointDistanceResult result, out LayerBodyInfo layerBodyInfo)
        {
            result             = default;
            layerBodyInfo      = default;
            var processor      = new LayerQueryProcessors.PointDistanceClosestImmediateProcessor(point, maxDistance, ref result, ref layerBodyInfo);
            var offsetDistance = math.max(maxDistance, 0f);
            FindObjects(AabbFrom(point - offsetDistance, point + offsetDistance), layer, processor).RunImmediate();
            var hit                 = result.subColliderIndex >= 0;
            result.subColliderIndex = math.max(result.subColliderIndex, 0);
            return hit;
        }

        public static bool DistanceBetweenAny(float3 point, in CollisionLayer layer, float maxDistance, out PointDistanceResult result, out LayerBodyInfo layerBodyInfo)
        {
            result             = default;
            layerBodyInfo      = default;
            var processor      = new LayerQueryProcessors.PointDistanceAnyImmediateProcessor(point, maxDistance, ref result, ref layerBodyInfo);
            var offsetDistance = math.max(maxDistance, 0f);
            FindObjects(AabbFrom(point - offsetDistance, point + offsetDistance), layer, processor).RunImmediate();
            var hit                 = result.subColliderIndex >= 0;
            result.subColliderIndex = math.max(result.subColliderIndex, 0);
            return hit;
        }
        #endregion

        #region Collider vs Collider
        public static bool DistanceBetween(in Collider colliderA,
                                           in TransformQvvs transformA,
                                           in Collider colliderB,
                                           in TransformQvvs transformB,
                                           float maxDistance,
                                           out ColliderDistanceResult result)
        {
            var scaledColliderA = colliderA;
            var scaledColliderB = colliderB;

            ScaleStretchCollider(ref scaledColliderA, transformA.scale, transformA.stretch);
            ScaleStretchCollider(ref scaledColliderB, transformB.scale, transformB.stretch);
            return ColliderColliderDispatch.DistanceBetween(in scaledColliderA,
                                                            new RigidTransform(transformA.rotation, transformA.position),
                                                            in scaledColliderB,
                                                            new RigidTransform(transformB.rotation, transformB.position),
                                                            maxDistance,
                                                            out result);
        }

        // Todo: Legacy, remove
        public static bool DistanceBetween(Collider colliderA,
                                           in RigidTransform transformA,
                                           Collider colliderB,
                                           in RigidTransform transformB,
                                           float maxDistance,
                                           out ColliderDistanceResult result)
        {
            return ColliderColliderDispatch.DistanceBetween(in colliderA, in transformA, in colliderB, in transformB, maxDistance, out result);
        }
        #endregion

        #region Collider vs Layer
        public static bool DistanceBetween(in Collider collider,
                                           in TransformQvvs transform,
                                           in CollisionLayer layer,
                                           float maxDistance,
                                           out ColliderDistanceResult result,
                                           out LayerBodyInfo layerBodyInfo)
        {
            var scaledTransform = new RigidTransform(transform.rotation, transform.position);
            var scaledCollider  = collider;
            ScaleStretchCollider(ref scaledCollider, transform.scale, transform.stretch);

            result        = default;
            layerBodyInfo = default;
            var processor = new LayerQueryProcessors.ColliderDistanceClosestImmediateProcessor(in scaledCollider,
                                                                                               in scaledTransform,
                                                                                               maxDistance,
                                                                                               ref result,
                                                                                               ref layerBodyInfo);
            var aabb            = AabbFrom(in scaledCollider, in scaledTransform);
            var offsetDistance  = math.max(maxDistance, 0f);
            aabb.min           -= offsetDistance;
            aabb.max           += offsetDistance;
            FindObjects(aabb, in layer, in processor).RunImmediate();
            var hit                  = result.subColliderIndexB >= 0;
            result.subColliderIndexB = math.max(result.subColliderIndexB, 0);
            return hit;
        }

        public static bool DistanceBetweenAny(Collider collider,
                                              in TransformQvvs transform,
                                              in CollisionLayer layer,
                                              float maxDistance,
                                              out ColliderDistanceResult result,
                                              out LayerBodyInfo layerBodyInfo)
        {
            var scaledTransform = new RigidTransform(transform.rotation, transform.position);
            var scaledCollider  = collider;
            ScaleStretchCollider(ref scaledCollider, transform.scale, transform.stretch);

            result              = default;
            layerBodyInfo       = default;
            var processor       = new LayerQueryProcessors.ColliderDistanceAnyImmediateProcessor(in scaledCollider, in scaledTransform, maxDistance, ref result, ref layerBodyInfo);
            var aabb            = AabbFrom(in scaledCollider, in scaledTransform);
            var offsetDistance  = math.max(maxDistance, 0f);
            aabb.min           -= offsetDistance;
            aabb.max           += offsetDistance;
            FindObjects(aabb, in layer, in processor).RunImmediate();
            var hit                  = result.subColliderIndexB >= 0;
            result.subColliderIndexB = math.max(result.subColliderIndexB, 0);
            return hit;
        }
        #endregion
    }
}

