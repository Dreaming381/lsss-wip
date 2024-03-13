using Latios.Transforms;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Latios.Psyshock
{
    public static partial class UnitySim
    {
        public struct Velocity
        {
            public float3 linear;
            public float3 angular;
        }

        public struct Mass
        {
            public float  inverseMass;
            public float3 inverseInertia;
        }

        public struct MotionExpansion
        {
            float4 uniformXlinearYzw;

            // Note: Gravity and all other non-constraint forces should be applied to velocity already.
            public MotionExpansion(in Velocity velocity, float deltaTime, float angularExpansionFactor)
            {
                var linear = velocity.linear * deltaTime;
                // math.length(AngularVelocity) * timeStep is conservative approximation of sin((math.length(AngularVelocity) * timeStep)
                var uniform       = 0.05f + math.min(math.length(velocity.angular) * deltaTime * angularExpansionFactor, angularExpansionFactor);
                uniformXlinearYzw = new float4(uniform, linear);
            }

            public Aabb ExpandAabb(Aabb aabb)
            {
                var linear = uniformXlinearYzw.yzw;
                aabb.min   = math.min(aabb.min, aabb.min + linear) - uniformXlinearYzw.x;
                aabb.max   = math.max(aabb.max, aabb.max + linear) + uniformXlinearYzw.x;
                return aabb;
            }

            // Dynamic vs static
            public static float GetMaxDistance(in MotionExpansion motionExpansion)
            {
                return math.length(motionExpansion.uniformXlinearYzw.yzw) + motionExpansion.uniformXlinearYzw.x + 0.05f;
            }

            // Dynamic vs dynamic
            public static float GetMaxDistance(in MotionExpansion motionExpansionA, in MotionExpansion motionExpansionB)
            {
                var tempB        = motionExpansionB.uniformXlinearYzw;
                tempB.x          = -tempB.x;
                var tempCombined = motionExpansionA.uniformXlinearYzw - tempB;
                return math.length(tempCombined.yzw) + tempCombined.x;
            }
        }

        public static void Integrate(ref RigidTransform inertialPoseWorldTransform, ref Velocity velocity, float linearDamping, float angularDamping, float deltaTime)
        {
            inertialPoseWorldTransform.pos += velocity.linear * deltaTime;
            var halfDeltaAngle              = velocity.angular * 0.5f * deltaTime;
            var dq                          = new quaternion(new float4(halfDeltaAngle, 1f));
            inertialPoseWorldTransform.rot  = math.normalize(math.mul(inertialPoseWorldTransform.rot, dq));

            var dampFactors   = math.clamp(1f - new float2(linearDamping, angularDamping) * deltaTime, 0f, 1f);
            velocity.linear  *= dampFactors.x;
            velocity.angular *= dampFactors.y;
        }

        public static TransformQvvs ApplyInertialPoseWorldTransformDeltaToWorldTransform(in TransformQvvs oldWorldTransform,
                                                                                         in RigidTransform oldInertialPoseWorldTransform,
                                                                                         in RigidTransform newInertialPoseWorldTransform)
        {
            var oldTransform = new RigidTransform(oldWorldTransform.rotation, oldWorldTransform.position);
            // oldInertialPoseWorldTransform = oldWorldTransform * localInertial
            // newInertialPoseWorldTransfrom = newWorldTransform * localInertial
            // inverseOldWorldTransform * oldInertialWorldTransform = inverseOldWorldTransform * oldWorldTransform * localInertial
            // inverseOldWorldTransform * oldInertialWorldTransform = localInertial
            // newInertialPoseWorldTransform * inverseLocalInertial = newWorldTransform * localInertial * inverseLocalInertial
            // newInertialPoseWorldTransform * inverseLocalInertial = newWorldTransform
            // newInertialPoseWorldTransform * inverse(inverseOldWorldTransform * oldInertialWorldTransform) = newWorldTransform
            // newInertialPoseWorldTransform * inverseOldInertialWorldTransform * oldWorldTransform = newWorldTransform
            var newTransform = math.mul(newInertialPoseWorldTransform, math.mul(math.inverse(oldInertialPoseWorldTransform), oldTransform));
            return new TransformQvvs
            {
                position   = newTransform.pos,
                rotation   = newTransform.rot,
                scale      = oldWorldTransform.scale,
                stretch    = oldWorldTransform.stretch,
                worldIndex = oldWorldTransform.worldIndex
            };
        }

        public static TransformQvvs ApplyWorldTransformFromInertialPoses(in TransformQvvs oldWorldTransform,
                                                                         in RigidTransform inertialPoseWorldTransform,
                                                                         quaternion localTensorOrientation,
                                                                         float3 localCenterOfMassUnscaled)
        {
            var localInertial     = new RigidTransform(localTensorOrientation, localCenterOfMassUnscaled * oldWorldTransform.stretch * oldWorldTransform.scale);
            var newWorldTransform = math.mul(inertialPoseWorldTransform, math.inverse(localInertial));
            return new TransformQvvs(newWorldTransform.pos, newWorldTransform.rot, oldWorldTransform.scale, oldWorldTransform.stretch, oldWorldTransform.worldIndex);
        }
    }
}

