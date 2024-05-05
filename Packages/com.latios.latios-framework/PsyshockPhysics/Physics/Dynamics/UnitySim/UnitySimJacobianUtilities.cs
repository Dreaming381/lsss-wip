using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Latios.Psyshock
{
    public static partial class UnitySim
    {
        // Calculate the inverse effective mass of a linear jacobian
        static float CalculateInvEffectiveMassDiag(float3 angA, float3 invInertiaA, float invMassA,
                                                   float3 angB, float3 invInertiaB, float invMassB)
        {
            float3 angularPart = angA * angA * invInertiaA + angB * angB * invInertiaB;
            float  linearPart  = invMassA + invMassB;
            return angularPart.x + angularPart.y + angularPart.z + linearPart;
        }

        // Calculate the inverse effective mass for a pair of jacobians with perpendicular linear parts
        static float CalculateInvEffectiveMassOffDiag(float3 angA0, float3 angA1, float3 invInertiaA,
                                                      float3 angB0, float3 angB1, float3 invInertiaB)
        {
            return math.csum(angA0 * angA1 * invInertiaA + angB0 * angB1 * invInertiaB);
        }

        // Inverts a symmetric 3x3 matrix with diag = (0, 0), (1, 1), (2, 2), offDiag = (0, 1), (0, 2), (1, 2) = (1, 0), (2, 0), (2, 1)
        static bool InvertSymmetricMatrix(float3 diag, float3 offDiag, out float3 invDiag, out float3 invOffDiag)
        {
            float3 offDiagSq      = offDiag.zyx * offDiag.zyx;
            float  determinant    = (mathex.cproduct(diag) + 2.0f * mathex.cproduct(offDiag) - math.csum(offDiagSq * diag));
            bool   determinantOk  = (determinant != 0);
            float  invDeterminant = math.select(0.0f, 1.0f / determinant, determinantOk);
            invDiag               = (diag.yxx * diag.zzy - offDiagSq) * invDeterminant;
            invOffDiag            = (offDiag.yxx * offDiag.zzy - diag.zyx * offDiag) * invDeterminant;
            return determinantOk;
        }

        // Returns x - clamp(x, min, max)
        static float CalculateError(float x, float min, float max)
        {
            float error = math.max(x - max, 0.0f);
            error       = math.min(x - min, error);
            return error;
        }

        // Returns the amount of error for the solver to correct, where initialError is the pre-integration error and predictedError is the expected post-integration error
        // If (predicted > initial) HAVE overshot target = (Predicted - initial)*damping + initial*tau
        // If (predicted < initial) HAVE NOT met target = predicted * tau (ie: damping not used if target not met)
        public static float CalculateCorrection(float predictedError, float initialError, float tau, float damping)
        {
            return math.max(predictedError - initialError, 0.0f) * damping + math.min(predictedError, initialError) * tau;
        }
    }
}

