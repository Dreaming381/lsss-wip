#if !LATIOS_TRANSFORMS_UNITY
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Latios.Transforms
{
    internal static class HierarchyInternalUtilities
    {
        public static void UpdateTransform(ref TransformQvvs worldTransform, ref TransformQvs localTransform, in TransformQvvs parentTransform, InheritanceFlags flags)
        {
            if (flags == InheritanceFlags.Normal)
            {
                qvvs.mul(ref worldTransform, in parentTransform, in localTransform);
                return;
            }
            if (flags == InheritanceFlags.WorldAll)
            {
                localTransform = qvvs.inversemul(in parentTransform, in worldTransform);
                return;
            }
            if (flags.HasCopyParent())
            {
                worldTransform = parentTransform;
                localTransform = TransformQvs.identity;
                return;
            }

            var originalWorldTransform = worldTransform;
            qvvs.mul(ref worldTransform, in parentTransform, in localTransform);

            if ((flags & InheritanceFlags.WorldRotation) == InheritanceFlags.WorldRotation)
                worldTransform.rotation = originalWorldTransform.rotation;
            else if ((flags & InheritanceFlags.WorldRotation) != InheritanceFlags.Normal)
                worldTransform.rotation = ComputeMixedRotation(originalWorldTransform.rotation, worldTransform.rotation, flags);
            if ((flags & InheritanceFlags.WorldX) == InheritanceFlags.WorldX)
                worldTransform.position.x = originalWorldTransform.position.x;
            if ((flags & InheritanceFlags.WorldY) == InheritanceFlags.WorldY)
                worldTransform.position.y = originalWorldTransform.position.y;
            if ((flags & InheritanceFlags.WorldZ) == InheritanceFlags.WorldZ)
                worldTransform.position.z = originalWorldTransform.position.z;
            if ((flags & InheritanceFlags.WorldScale) == InheritanceFlags.WorldScale)
                worldTransform.scale = originalWorldTransform.scale;

            localTransform = qvvs.inversemul(in parentTransform, in worldTransform);
        }

        static quaternion ComputeMixedRotation(quaternion originalWorldRotation, quaternion hierarchyWorldRotation, InheritanceFlags flags)
        {
            var forward = math.select(math.forward(hierarchyWorldRotation),
                                      math.forward(originalWorldRotation),
                                      (flags & InheritanceFlags.WorldForward) == InheritanceFlags.WorldForward);
            var up = math.select(math.rotate(hierarchyWorldRotation, math.up()),
                                 math.rotate(originalWorldRotation, math.up()),
                                 (flags & InheritanceFlags.WorldUp) == InheritanceFlags.WorldUp);

            if ((flags & InheritanceFlags.StrictUp) == InheritanceFlags.StrictUp)
            {
                float3 right = math.normalizesafe(math.cross(up, forward), float3.zero);
                if (right.Equals(float3.zero))
                    return math.select(hierarchyWorldRotation.value, originalWorldRotation.value, (flags & InheritanceFlags.WorldUp) == InheritanceFlags.WorldUp);
                var newForward = math.cross(right, up);
                return new quaternion(new float3x3(right, up, newForward));
            }
            else
            {
                float3 right = math.normalizesafe(math.cross(up, forward), float3.zero);
                if (right.Equals(float3.zero))
                    return math.select(hierarchyWorldRotation.value,
                                       originalWorldRotation.value,
                                       (flags & InheritanceFlags.WorldForward) == InheritanceFlags.WorldForward);
                var newUp = math.cross(forward, right);
                return new quaternion(new float3x3(right, newUp, forward));
            }
        }
    }
}
#endif

