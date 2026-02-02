using Latios.Calci;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace UnitTests
{
    public class LocalTransformTests
    {
        [Test]
        public void Randomness()
        {
            float3     parentPosition         = new float3(22, 44, 22);
            quaternion parentRotation         = quaternion.identity;
            float3     localPositionReference = new float3(0f, 1f, 1.5f);
            quaternion localRotationReference = quaternion.AxisAngle(math.normalize(new float3(1, 3, 5)), 1.2f);

            float3     worldPosition = parentPosition + localPositionReference;
            quaternion worldRotation = localRotationReference;

            Rng generator = new Rng("LocalTransformTests");
            var rng       = generator.GetSequence(0);

            for (int i = 0; i < 1000000; i++)
            {
                var oldParentPos  = parentPosition;
                var oldParentRot  = parentRotation;
                parentPosition   += rng.NextFloat3(-0.2f, 0.2f);
                parentRotation    = math.normalize(math.mul(math.slerp(quaternion.identity, rng.NextQuaternionRotation(), 0.01f), parentRotation));

                // How do we improve the next 4 lines to result in a finalLocalPosition more closely matching the reference?
                // Ideally, we don't create any new variables outside of a single loop iteration (as that will turn into a random-access in production).
                var localPosition = math.mul(math.conjugate(oldParentRot), worldPosition - oldParentPos);
                var localRotation = math.mul(math.conjugate(oldParentRot), worldRotation);

                worldPosition = parentPosition + math.rotate(parentRotation, localPosition);
                worldRotation = math.normalize(math.mul(parentRotation, localRotation));
            }

            var finalLocalPosition = math.mul(math.conjugate(parentRotation), worldPosition - parentPosition);
            var finalLocalRotation = math.mul(math.conjugate(parentRotation), worldRotation);

            UnityEngine.Debug.Log($"final pos: {finalLocalPosition}, final rot: {finalLocalRotation}, rot ref: {localRotationReference}");
        }

        [Test]
        public void Randomness2()
        {
            float3     parentPosition         = new float3(22, 44, 22);
            quaternion parentRotation         = quaternion.identity;
            float3     localPositionReference = new float3(0f, 1f, 1.5f);
            quaternion localRotationReference = quaternion.AxisAngle(math.normalize(new float3(1, 3, 5)), 1.2f);

            float3     worldPosition = parentPosition + localPositionReference;
            quaternion worldRotation = localRotationReference;

            Rng generator = new Rng("LocalTransformTests");
            var rng       = generator.GetSequence(0);

            for (int i = 0; i < 1000000; i++)
            {
                var oldParentPos  = parentPosition;
                var oldParentRot  = parentRotation;
                parentPosition   += rng.NextFloat3(-0.2f, 0.2f);
                parentRotation    = math.normalize(math.mul(math.slerp(quaternion.identity, rng.NextQuaternionRotation(), 0.01f), parentRotation));

                // How do we improve the next 4 lines to result in a finalLocalPosition more closely matching the reference?
                // Ideally, we don't create any new variables outside of a single loop iteration (as that will turn into a random-access in production).
                var diff          = worldPosition - oldParentPos;
                var diffDir       = math.normalize(diff);
                var diffMag       = math.length(diff);
                var localPosition = math.normalize(math.mul(math.conjugate(oldParentRot), diffDir)) * diffMag;
                var localRotation = math.mul(math.conjugate(oldParentRot), worldRotation);
                diffDir           = math.normalize(localPosition);
                diffMag           = math.length(localPosition);
                worldPosition     = parentPosition + math.normalize(math.rotate(parentRotation, diffDir)) * diffMag;
                worldRotation     = math.normalize(math.mul(parentRotation, localRotation));
            }

            var finalLocalPosition = math.mul(math.conjugate(parentRotation), worldPosition - parentPosition);
            var finalLocalRotation = math.mul(math.conjugate(parentRotation), worldRotation);

            UnityEngine.Debug.Log($"final pos: {finalLocalPosition}, final rot: {finalLocalRotation}, rot ref: {localRotationReference}");
        }

        [Test]
        public void Randomness3()
        {
            float3     parentPosition         = new float3(22, 44, 22);
            quaternion parentRotation         = quaternion.identity;
            float3     localPositionReference = new float3(0f, 1f, 1.5f);
            quaternion localRotationReference = quaternion.AxisAngle(math.normalize(new float3(1, 3, 5)), 1.2f);

            float3     worldPosition = parentPosition + localPositionReference;
            quaternion worldRotation = localRotationReference;

            Rng generator = new Rng("LocalTransformTests");
            var rng       = generator.GetSequence(0);

            for (int i = 0; i < 1000000; i++)
            {
                var oldParentPos  = parentPosition;
                var oldParentRot  = parentRotation;
                parentPosition   += rng.NextFloat3(-0.2f, 0.2f);
                parentRotation    = math.normalize(math.mul(math.slerp(quaternion.identity, rng.NextQuaternionRotation(), 0.01f), parentRotation));

                // How do we improve the next 4 lines to result in a finalLocalPosition more closely matching the reference?
                // Ideally, we don't create any new variables outside of a single loop iteration (as that will turn into a random-access in production).
                var diff          = worldPosition - oldParentPos;
                var diffDir       = math.normalize(diff);
                var diffMag       = math.length(diff);
                var localPosition = math.normalize(math.mul(math.conjugate(oldParentRot), diffDir)) * diffMag;
                localPosition     = math.round(localPosition * 16384f) / 16384f;
                var localRotation = math.mul(math.conjugate(oldParentRot), worldRotation);
                diffDir           = math.normalize(localPosition);
                diffMag           = math.length(localPosition);
                worldPosition     = parentPosition + math.normalize(math.rotate(parentRotation, diffDir)) * diffMag;
                worldRotation     = math.normalize(math.mul(parentRotation, localRotation));
            }

            var finalLocalPosition = math.mul(math.conjugate(parentRotation), worldPosition - parentPosition);
            var finalLocalRotation = math.mul(math.conjugate(parentRotation), worldRotation);

            UnityEngine.Debug.Log($"final pos: {finalLocalPosition}, final rot: {finalLocalRotation}, rot ref: {localRotationReference}");
        }

        [Test]
        public void Randomness4()
        {
            float3     parentPosition         = new float3(2020, 404, 6200);
            quaternion parentRotation         = quaternion.identity;
            float3     localPositionReference = new float3(0f, 1f, 1.5f);
            quaternion localRotationReference = quaternion.AxisAngle(math.normalize(new float3(1, 3, 5)), 1.2f);

            float3     worldPosition = parentPosition + localPositionReference;
            quaternion worldRotation = localRotationReference;

            Rng generator = new Rng("LocalTransformTests");
            var rng       = generator.GetSequence(0);

            for (int i = 0; i < 5000000; i++)
            {
                var oldParentPos  = parentPosition;
                var oldParentRot  = parentRotation;
                parentPosition   += rng.NextFloat3(-0.2f, 0.2f);
                parentRotation    = math.normalize(math.mul(math.slerp(quaternion.identity, rng.NextQuaternionRotation(), 0.01f), parentRotation));

                // How do we improve the next 4 lines to result in a finalLocalPosition more closely matching the reference?
                // Ideally, we don't create any new variables outside of a single loop iteration (as that will turn into a random-access in production).
                var diff          = worldPosition - oldParentPos;
                var diffDir       = math.normalize(diff);
                var diffMag       = math.length(diff);
                var localPosition = math.normalize(math.mul(math.conjugate(oldParentRot), diffDir)) * diffMag;
                localPosition     = math.round(localPosition * 1024f) / 1024f;
                var localRotation = math.mul(math.conjugate(oldParentRot), worldRotation);
                diffDir           = math.normalize(localPosition);
                diffMag           = math.length(localPosition);
                worldPosition     = parentPosition + math.normalize(math.rotate(parentRotation, diffDir)) * diffMag;
                worldRotation     = math.normalize(math.mul(parentRotation, localRotation));
            }

            var finalLocalPosition = math.mul(math.conjugate(parentRotation), worldPosition - parentPosition);
            var finalLocalRotation = math.mul(math.conjugate(parentRotation), worldRotation);

            UnityEngine.Debug.Log($"final pos: {finalLocalPosition}, final rot: {finalLocalRotation}, rot ref: {localRotationReference}");
        }
    }
}

