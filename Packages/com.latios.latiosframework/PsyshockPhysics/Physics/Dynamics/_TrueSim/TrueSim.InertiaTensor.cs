using System;
using Latios.Transforms;
using Unity.Mathematics;

namespace Latios.Psyshock
{
    public static partial class TrueSim
    {
        public static float3x3 ScaleInertiaTensor(float3x3 original, float scale) => original * math.square(scale);

        public static float3x3 StretchInertiaTensor(float3x3 original, float3 stretch)
        {
            // The inertia tensor matrix diagonal components (not necessarily a diagonalized inertia tensor) are defined as follows:
            // diagonal.x = sum_1_k(mass_k * (y_k^2 + z_k^2)) = sum_1_k(mass_k * y_k^2) + sum_1_k(mass_k * z_k^2)
            // And for uniform density, m_k is constant, so:
            // diagonal.x = mass * sum_1_k(y_k^2) + sum_1_k(z_k^2)
            // diagonal.y = mass * sum_1_k(x_k^2) + sum_1_k(z_k^2)
            // diagonal.z = mass * sum_1_k(x_k^2) + sum_1_k(y_k^2)
            // The base inertia diagonal has mass divided out to be 1f, so we can drop it from our expression.
            //
            // We can define a property s as the sum of diagonals.
            // diagonal.x + diagonal.y + diagonal.z = sum_1_k(y_k^2) + sum_1_k(z_k^2) + sum_1_k(x_k^2) + sum_1_k(z_k^2) + sum_1_k(x_k^2) + sum_1_k(y_k^2)
            // diagonal.x + diagonal.y + diagonal.z = 2 * ( sum_1_k(x_k^2) + sum_1_k(y_k^2) + sum_1_k(z_k^2) )
            //
            // And with this, we can write this expression:
            // (diagonal.x + diagonal.y + diagonal.z) / 2 - diagonal.x = sum_1_k(x_k^2)
            // And we can do similar for the other two axes.
            //
            // Applying stretch changes the expression of sum_1_k(x_k^2) to sum_1_k( (x_k * stretch.x)^2 ) = sum_1_k(x_k^2 * stretch.x^2) = stretch.x^2 * sum_1_k(x_k^2)
            // And with that, we have all the data we need to reassemble the inertia tensor.
            var diagonal        = new float3(original.c0.x, original.c1.y, original.c2.z);
            var diagonalHalfSum = math.csum(diagonal) / 2f;
            var xSqySqzSq       = diagonalHalfSum - diagonal;
            var newDiagonal     = stretch * stretch * xSqySqzSq;

            // The off diagonals are just products, so we can actually just scale those.
            var scaleMatrix = new float3x3(new float3(0f, stretch.x * stretch.yz),
                                           new float3(stretch.x * stretch.y, 0f, stretch.x * stretch.z),
                                           new float3(stretch.z * stretch.xy, 0f));
            var result  = original * scaleMatrix;
            result.c0.x = newDiagonal.x;
            result.c1.y = newDiagonal.y;
            result.c2.z = newDiagonal.z;
            return result;
        }

        public static float3x3 RotateInertiaTensor(float3x3 original, quaternion rotation)
        {
            var rotMat        = new float3x3(rotation);
            var rotMatInverse = new float3x3(math.conjugate(rotation));
            return math.mul(rotMat, math.mul(original, rotMatInverse));
        }

        public static float3x3 TranslateInertiaTensor(float3x3 original, float3 translation)
        {
            float3 shift          = translation;
            float3 shiftSq        = shift * shift;
            var    diag           = new float3(shiftSq.y + shiftSq.z, shiftSq.x + shiftSq.z, shiftSq.x + shiftSq.y);
            var    offDiag        = new float3(shift.x * shift.y, shift.y * shift.z, shift.z * shift.x) * -1.0f;
            var    inertiaMatrix  = original;
            inertiaMatrix.c0     += new float3(diag.x, offDiag.x, offDiag.z);
            inertiaMatrix.c1     += new float3(offDiag.x, diag.y, offDiag.y);
            inertiaMatrix.c2     += new float3(offDiag.z, offDiag.y, diag.z);
            return inertiaMatrix;
        }

        public static float3x3 TransformInertiaTensor(float3x3 original, RigidTransform transform)
        {
            return TranslateInertiaTensor(RotateInertiaTensor(original, transform.rot), transform.pos);
        }

        public static float3x3 TransformInertiaTensor(float3x3 original, TransformQvvs transform)
        {
            float3x3 result = ScaleInertiaTensor(original, transform.scale);
            result          = StretchInertiaTensor(result, transform.stretch);
            result          = RotateInertiaTensor(result, transform.rotation);
            return TranslateInertiaTensor(result, transform.position);
        }

        /// <summary>
        /// Computes the composite mass-independent inertia tensor (gyration tensor) from the individual gyration tensors pre-transformed into the composite space.
        /// </summary>
        /// <param name="individualTransformedGyrationTensors">The individual gyration tensors that make up the composite, already transformed into composite space</param>
        /// <param name="individualMasses">The individual total masses of each object making up the composite</param>
        /// <returns>The gyration tensor of the overall compound</returns>
        public static float3x3 GyrationTensorFrom(ReadOnlySpan<float3x3> individualTransformedGyrationTensors, ReadOnlySpan<float> individualMasses)
        {
            float3x3 it = float3x3.zero;
            float    m  = 0f;
            for (int i = 0; i < individualTransformedGyrationTensors.Length; i++)
            {
                it += individualTransformedGyrationTensors[i] * m;
                m  += individualMasses[i];
            }
            return it / m;
        }
    }
}

