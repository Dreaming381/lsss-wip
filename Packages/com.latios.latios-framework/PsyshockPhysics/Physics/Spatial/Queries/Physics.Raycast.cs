using System;
using Latios.Transforms;
using Unity.Mathematics;

namespace Latios.Psyshock
{
    public static partial class Physics
    {
        public static bool Raycast(float3 start, float3 end, Collider collider, in TransformQvvs transform, out RaycastResult result)
        {
            return PointRayDispatch.Raycast(new Ray(start, end), in collider, in transform, out result);
        }

        public static bool Raycast(in Ray ray, Collider collider, in TransformQvvs transform, out RaycastResult result)
        {
            return PointRayDispatch.Raycast(ray, in collider, in transform, out result);
        }

        public static bool Raycast(float3 start, float3 end, in CollisionLayer layer, out RaycastResult result, out LayerBodyInfo layerBodyInfo)
        {
            return Raycast(new Ray(start, end), in layer, out result, out layerBodyInfo);
        }

        public static bool Raycast(in Ray ray, in CollisionLayer layer, out RaycastResult result, out LayerBodyInfo layerBodyInfo)
        {
            result        = default;
            layerBodyInfo = default;
            var processor = new LayerQueryProcessors.RaycastClosestImmediateProcessor(ray, ref result, ref layerBodyInfo);
            FindObjects(AabbFrom(ray), in layer, in processor).RunImmediate();
            var hit                 = result.subColliderIndex >= 0;
            result.subColliderIndex = math.max(result.subColliderIndex, 0);
            return hit;
        }

        public static bool RaycastAny(float3 start, float3 end, in CollisionLayer layer, out RaycastResult result, out LayerBodyInfo layerBodyInfo)
        {
            return RaycastAny(new Ray(start, end), in layer, out result, out layerBodyInfo);
        }

        public static bool RaycastAny(in Ray ray, in CollisionLayer layer, out RaycastResult result, out LayerBodyInfo layerBodyInfo)
        {
            result        = default;
            layerBodyInfo = default;
            var processor = new LayerQueryProcessors.RaycastAnyImmediateProcessor(ray, ref result, ref layerBodyInfo);
            FindObjects(AabbFrom(ray), in layer, in processor).RunImmediate();
            var hit                 = result.subColliderIndex >= 0;
            result.subColliderIndex = math.max(result.subColliderIndex, 0);
            return hit;
        }
    }
}

