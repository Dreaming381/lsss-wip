using System;
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

        /// <summary>   A contact jacobian angular. </summary>
        public struct ContactJacobianAngular
        {
            /// <summary>   The angular a. </summary>
            public float3 angularA;
            /// <summary>   The angular b. </summary>
            public float3 angularB;
            /// <summary>   The effective mass. </summary>
            public float effectiveMass;
        }

        /// <summary>   A contact jacobian angle and velocity to reach the contact plane. </summary>
        public struct ContactJacobianContactParameters
        {
            /// <summary>   The jacobian. </summary>
            public ContactJacobianAngular jacobianAngular;

            /// <summary>
            /// Velocity needed to reach the contact plane in one frame, both if approaching (negative) and
            /// depenetrating (positive)
            /// </summary>
            public float velocityToReachContactPlane;
        }

        public struct ContactJacobianBodyParameters
        {
            // Linear friction jacobians.  Only store the angular part, linear part can be recalculated from BaseJacobian.Normal
            public ContactJacobianAngular friction0;  // effectiveMass stores friction effective mass matrix element (0, 0)
            public ContactJacobianAngular friction1;  // effectiveMass stores friction effective mass matrix element (1, 1)
            public float3                 frictionDirection0;
            public float3                 frictionDirection1;

            // Angular friction about the contact normal, no linear part
            public ContactJacobianAngular angularFriction;  // effectiveMass stores friction effective mass matrix element (2, 2)
            public float3                 frictionEffectiveMassOffDiag;  // Effective mass matrix (0, 1), (0, 2), (1, 2) == (1, 0), (2, 0), (2, 1)

            public float3 contactNormal;
            public float3 surfaceVelocityDv;

            public float coefficientOfFriction;

            public ContactJacobianBodyParameters(in ContactJacobianAngular friction0,
                                                 in ContactJacobianAngular friction1,
                                                 in ContactJacobianAngular angularFriction,
                                                 in float3 frictionEffectiveMassOffDiag,
                                                 in float3 contactNormal,
                                                 float coefficientOfFriction,
                                                 Velocity surfaceVelocity = default)
            {
                this.friction0                    = friction0;
                this.friction1                    = friction1;
                this.angularFriction              = angularFriction;
                this.frictionEffectiveMassOffDiag = frictionEffectiveMassOffDiag;
                this.contactNormal                = contactNormal;
                this.coefficientOfFriction        = coefficientOfFriction;

                mathex.GetDualPerpendicularNormalized(contactNormal, out frictionDirection0, out frictionDirection1);

                surfaceVelocityDv = default;
                if (!surfaceVelocity.Equals(float3.zero))
                {
                    float linVel0 = math.dot(surfaceVelocity.linear, frictionDirection0);
                    float linVel1 = math.dot(surfaceVelocity.linear, frictionDirection1);

                    float angVelProj  = math.dot(surfaceVelocity.angular, contactNormal);
                    surfaceVelocityDv = new float3(linVel0, linVel1, angVelProj);
                }
            }
        }

        // Internal motion data input for the solver stabilization
        public struct MotionStabilizationInput
        {
            public Velocity inputVelocity;
            public float    inverseInertiaScale;

            public static readonly MotionStabilizationInput kDefault = new MotionStabilizationInput
            {
                inputVelocity       = default,
                inverseInertiaScale = 1.0f
            };
        }

        public struct ContactJacobianImpulses
        {
            public float combinedContactPointsImpulse;
            public float friction0Impulse;
            public float friction1Impulse;
            public float frictionAngularImpulse;
        }

        public static bool SolveJacobian(ref Velocity velocityA, in Mass massA, in MotionStabilizationInput motionStabilizationSolverInputA,
                                         ref Velocity velocityB, in Mass massB, in MotionStabilizationInput motionStabilizationSolverInputB,
                                         ReadOnlySpan<ContactJacobianContactParameters> perContactParameters, Span<float> perContactImpulses,
                                         in ContactJacobianBodyParameters bodyParameters,
                                         bool enableFrictionVelocitiesHeuristic, float InvNumSolverIterations,
                                         out ContactJacobianImpulses outputImpulses)
        {
            // Copy velocity data
            Velocity tempVelocityA = velocityA;
            Velocity tempVelocityB = velocityB;

            // Solve normal impulses
            bool  hasCollisionEvent = false;
            float sumImpulses       = 0.0f;
            outputImpulses          = default;

            for (int j = 0; j < perContactParameters.Length; j++)
            {
                ref readonly ContactJacobianContactParameters jacAngular     = ref perContactParameters[j];
                var                                           contactImpulse = perContactImpulses[j];

                // Solve velocity so that predicted contact distance is greater than or equal to zero
                float relativeVelocity = GetJacVelocity(bodyParameters.contactNormal, jacAngular.jacobianAngular,
                                                        tempVelocityA.linear, tempVelocityA.angular, tempVelocityB.linear, tempVelocityB.angular);
                float dv = jacAngular.velocityToReachContactPlane - relativeVelocity;

                float impulse            = dv * jacAngular.jacobianAngular.effectiveMass;
                float accumulatedImpulse = math.max(contactImpulse + impulse, 0.0f);
                if (accumulatedImpulse != contactImpulse)
                {
                    float deltaImpulse = accumulatedImpulse - contactImpulse;
                    ApplyImpulse(deltaImpulse, bodyParameters.contactNormal, jacAngular.jacobianAngular, ref tempVelocityA, ref tempVelocityB, in massA, in massB,
                                 motionStabilizationSolverInputA.inverseInertiaScale, motionStabilizationSolverInputB.inverseInertiaScale);
                }

                contactImpulse                               = accumulatedImpulse;
                perContactImpulses[j]                        = contactImpulse;
                sumImpulses                                 += accumulatedImpulse;
                outputImpulses.combinedContactPointsImpulse += contactImpulse;

                // Force contact event even when no impulse is applied, but there is penetration.
                hasCollisionEvent |= jacAngular.velocityToReachContactPlane > 0.0f;
            }

            // Export collision event
            hasCollisionEvent |= outputImpulses.combinedContactPointsImpulse > 0.0f;

            // Solve friction
            if (sumImpulses > 0.0f)
            {
                // Choose friction axes
                mathex.GetDualPerpendicularNormalized(bodyParameters.contactNormal, out float3 frictionDir0, out float3 frictionDir1);

                // Calculate impulses for full stop
                float3 imp;
                {
                    // Take velocities that produce minimum energy (between input and solver velocity) as friction input
                    float3 frictionLinVelA = tempVelocityA.linear;
                    float3 frictionAngVelA = tempVelocityA.angular;
                    float3 frictionLinVelB = tempVelocityB.linear;
                    float3 frictionAngVelB = tempVelocityB.angular;
                    if (enableFrictionVelocitiesHeuristic)
                    {
                        GetFrictionVelocities(motionStabilizationSolverInputA.inputVelocity.linear, motionStabilizationSolverInputA.inputVelocity.angular,
                                              tempVelocityA.linear, tempVelocityA.angular,
                                              math.rcp(massA.inverseInertia), math.rcp(massA.inverseMass),
                                              out frictionLinVelA, out frictionAngVelA);
                        GetFrictionVelocities(motionStabilizationSolverInputB.inputVelocity.linear, motionStabilizationSolverInputB.inputVelocity.angular,
                                              tempVelocityB.linear, tempVelocityB.angular,
                                              math.rcp(massB.inverseInertia), math.rcp(massB.inverseMass),
                                              out frictionLinVelB, out frictionAngVelB);
                    }

                    // Calculate the jacobian dot velocity for each of the friction jacobians
                    float dv0 = bodyParameters.surfaceVelocityDv.x - GetJacVelocity(frictionDir0,
                                                                                    bodyParameters.friction0,
                                                                                    frictionLinVelA,
                                                                                    frictionAngVelA,
                                                                                    frictionLinVelB,
                                                                                    frictionAngVelB);
                    float dv1 = bodyParameters.surfaceVelocityDv.y - GetJacVelocity(frictionDir1,
                                                                                    bodyParameters.friction1,
                                                                                    frictionLinVelA,
                                                                                    frictionAngVelA,
                                                                                    frictionLinVelB,
                                                                                    frictionAngVelB);
                    float dva = bodyParameters.surfaceVelocityDv.z - math.csum(
                        bodyParameters.angularFriction.angularA * frictionAngVelA + bodyParameters.angularFriction.angularB * frictionAngVelB);

                    // Reassemble the effective mass matrix
                    float3 effectiveMassDiag = new float3(bodyParameters.friction0.effectiveMass,
                                                          bodyParameters.friction1.effectiveMass,
                                                          bodyParameters.angularFriction.effectiveMass);
                    float3x3 effectiveMass = BuildSymmetricMatrix(effectiveMassDiag, bodyParameters.frictionEffectiveMassOffDiag);

                    // Calculate the impulse
                    imp = math.mul(effectiveMass, new float3(dv0, dv1, dva));
                }

                // Clip TODO.ma calculate some contact radius and use it to influence balance between linear and angular friction
                float maxImpulse              = sumImpulses * bodyParameters.coefficientOfFriction * InvNumSolverIterations;
                float frictionImpulseSquared  = math.lengthsq(imp);
                imp                          *= math.min(1.0f, maxImpulse * math.rsqrt(frictionImpulseSquared));

                // Apply impulses
                ApplyImpulse(imp.x, frictionDir0, bodyParameters.friction0, ref tempVelocityA, ref tempVelocityB,
                             in massA, in massB,
                             motionStabilizationSolverInputA.inverseInertiaScale, motionStabilizationSolverInputB.inverseInertiaScale);
                ApplyImpulse(imp.y, frictionDir1, bodyParameters.friction1, ref tempVelocityA, ref tempVelocityB,
                             in massA, in massB,
                             motionStabilizationSolverInputA.inverseInertiaScale, motionStabilizationSolverInputB.inverseInertiaScale);

                tempVelocityA.angular += imp.z * bodyParameters.angularFriction.angularA * motionStabilizationSolverInputA.inverseInertiaScale * massA.inverseInertia;
                tempVelocityB.angular += imp.z * bodyParameters.angularFriction.angularB * motionStabilizationSolverInputB.inverseInertiaScale * massB.inverseInertia;

                // Accumulate them
                outputImpulses.friction0Impulse       = imp.x;
                outputImpulses.friction1Impulse       = imp.y;
                outputImpulses.frictionAngularImpulse = imp.z;
            }

            // Write back linear and angular velocities. Changes to other properties, like InverseMass, should not be persisted.
            velocityA = tempVelocityA;
            velocityB = tempVelocityB;

            return hasCollisionEvent;
        }

        static float GetJacVelocity(float3 linear, ContactJacobianAngular jacAngular,
                                    float3 linVelA, float3 angVelA, float3 linVelB, float3 angVelB)
        {
            float3 temp  = (linVelA - linVelB) * linear;
            temp        += angVelA * jacAngular.angularA;
            temp        += angVelB * jacAngular.angularB;
            return math.csum(temp);
        }

        private static void ApplyImpulse(
            float impulse, float3 linear, ContactJacobianAngular jacAngular,
            ref Velocity velocityA, ref Velocity velocityB,
            in Mass massA, in Mass massB,
            float inverseInertiaScaleA = 1.0f, float inverseInertiaScaleB = 1.0f)
        {
            velocityA.linear += impulse * linear * massA.inverseMass;
            velocityB.linear -= impulse * linear * massB.inverseMass;

            // Scale the impulse with inverseInertiaScale
            velocityA.angular += impulse * jacAngular.angularA * inverseInertiaScaleA * massA.inverseInertia;
            velocityB.angular += impulse * jacAngular.angularB * inverseInertiaScaleB * massB.inverseInertia;
        }

        static void GetFrictionVelocities(
            float3 inputLinearVelocity, float3 inputAngularVelocity,
            float3 intermediateLinearVelocity, float3 intermediateAngularVelocity,
            float3 inertia, float mass,
            out float3 frictionLinearVelocityOut, out float3 frictionAngularVelocityOut)
        {
            float inputEnergy;
            {
                float linearEnergySq  = mass * math.lengthsq(inputLinearVelocity);
                float angularEnergySq = math.dot(inertia * inputAngularVelocity, inputAngularVelocity);
                inputEnergy           = linearEnergySq + angularEnergySq;
            }

            float intermediateEnergy;
            {
                float linearEnergySq  = mass * math.lengthsq(intermediateLinearVelocity);
                float angularEnergySq = math.dot(inertia * intermediateAngularVelocity, intermediateAngularVelocity);
                intermediateEnergy    = linearEnergySq + angularEnergySq;
            }

            if (inputEnergy < intermediateEnergy)
            {
                // Make sure we don't change the sign of intermediate velocity when using the input one.
                // If sign was to be changed, zero it out since it produces less energy.
                bool3 changedSignLin       = inputLinearVelocity * intermediateLinearVelocity < float3.zero;
                bool3 changedSignAng       = inputAngularVelocity * intermediateAngularVelocity < float3.zero;
                frictionLinearVelocityOut  = math.select(inputLinearVelocity, float3.zero, changedSignLin);
                frictionAngularVelocityOut = math.select(inputAngularVelocity, float3.zero, changedSignAng);
            }
            else
            {
                frictionLinearVelocityOut  = intermediateLinearVelocity;
                frictionAngularVelocityOut = intermediateAngularVelocity;
            }
        }

        // Builds a symmetric 3x3 matrix from diag = (0, 0), (1, 1), (2, 2), offDiag = (0, 1), (0, 2), (1, 2) = (1, 0), (2, 0), (2, 1)
        static float3x3 BuildSymmetricMatrix(float3 diag, float3 offDiag)
        {
            return new float3x3(
                new float3(diag.x, offDiag.x, offDiag.y),
                new float3(offDiag.x, diag.y, offDiag.z),
                new float3(offDiag.y, offDiag.z, diag.z)
                );
        }
    }
}

