using Latios.Transforms;
using Unity.Burst.CompilerServices;
using Unity.Mathematics;

namespace Latios.Kinemation
{
    public struct RootMotionDeltaAccumulator  // Note: Must be initialized to default to work.
    {
        private TransformQvvs delta;

        public void Accumulate(ref BufferPoseBlender blender,
                               ref SkeletonClip clip,
                               float previousClipTime,
                               float loopCycleTransitions,
                               KeyframeInterpolationMode keyframeInterpolationMode = KeyframeInterpolationMode.Interpolate)
        {
            var sampledRoot           = blender.bufferAsQvvs.Reinterpret<TransformQvvs>()[0];
            blender.bufferAsQvvs[0]   = default;
            var normalizedSampledRoot = sampledRoot;
            normalizedSampledRoot.NormalizeBone();
            Accumulate(in normalizedSampledRoot, math.asfloat(sampledRoot.worldIndex), ref clip, previousClipTime, loopCycleTransitions, keyframeInterpolationMode);
        }

        public void Accumulate(ref OptimizedSkeletonAspect skeleton,
                               ref SkeletonClip clip,
                               float previousClipTime,
                               float loopCycleTransitions,
                               KeyframeInterpolationMode keyframeInterpolationMode = KeyframeInterpolationMode.Interpolate)
        {
            var array                 = skeleton.rawLocalTransformsRW;
            var sampledRoot           = array[0];
            array[0]                  = default;
            var normalizedSampledRoot = sampledRoot;
            normalizedSampledRoot.NormalizeBone();
            Accumulate(in normalizedSampledRoot, math.asfloat(sampledRoot.worldIndex), ref clip, previousClipTime, loopCycleTransitions, keyframeInterpolationMode);
        }

        public void SampleAccumulate(ref SkeletonClip clip,
                                     float currentLoopingTime,
                                     float previousLoopingTime,
                                     float weight,
                                     KeyframeInterpolationMode keyframeInterpolationMode = KeyframeInterpolationMode.Interpolate)
        {
            var loopCycleTransitions = clip.CountLoopCycleTransitions(currentLoopingTime, previousLoopingTime);
            var currentClipTime      = clip.LoopToClipTime(currentLoopingTime);
            var previousClipTime     = clip.LoopToClipTime(previousLoopingTime);
            var current              = clip.SampleBone(0, currentClipTime);
            Accumulate(in current, weight, ref clip, previousClipTime, loopCycleTransitions, keyframeInterpolationMode);
        }

        private void Accumulate(in TransformQvvs current,
                                float weight,
                                ref SkeletonClip clip,
                                float previousClipTime,
                                float loopCycleTransitions,
                                KeyframeInterpolationMode keyframeInterpolationMode)
        {
            var previousRoot = clip.SampleBone(0, previousClipTime, keyframeInterpolationMode);
            if (Hint.Likely(math.abs(loopCycleTransitions) < 0.5f))
            {
                var newDelta = RootMotionTools.DeltaBetween(current, previousRoot);
                newDelta     = RootMotionTools.ApplyWeight(newDelta, weight);
                delta        = RootMotionTools.AddDeltas(delta, newDelta);
            }
            else
            {
                var           beginRoot = clip.SampleBone(0, 0f);
                var           endRoot   = clip.SampleBone(0, clip.duration);
                TransformQvvs newDelta;
                if (Hint.Likely(loopCycleTransitions > 0f))
                {
                    var h = RootMotionTools.DeltaBetween(endRoot, previousRoot);
                    var t = RootMotionTools.DeltaBetween(current, beginRoot);
                    if (Hint.Unlikely(loopCycleTransitions > 1.5f))
                    {
                        var middleDelta = RootMotionTools.DeltaBetween(endRoot, beginRoot);
                        var toAdd       = middleDelta;
                        for (float i = 2.5f; i < loopCycleTransitions; i += 1f)
                            middleDelta = RootMotionTools.ConcatenateDeltas(middleDelta, toAdd);
                        newDelta        = RootMotionTools.ConcatenateDeltas(RootMotionTools.ConcatenateDeltas(h, middleDelta), t);
                    }
                    else
                        newDelta = RootMotionTools.ConcatenateDeltas(h, t);
                }
                else
                {
                    var h = RootMotionTools.DeltaBetween(endRoot, current);
                    var t = RootMotionTools.DeltaBetween(previousRoot, beginRoot);
                    if (Hint.Unlikely(loopCycleTransitions < -1.5f))
                    {
                        var middleDelta = RootMotionTools.DeltaBetween(beginRoot, endRoot);
                        var toAdd       = middleDelta;
                        for (float i = -2.5f; i < loopCycleTransitions; i -= 1f)
                            middleDelta = RootMotionTools.ConcatenateDeltas(middleDelta, toAdd);
                        newDelta        = RootMotionTools.ConcatenateDeltas(RootMotionTools.ConcatenateDeltas(h, middleDelta), t);
                    }
                    else
                        newDelta = RootMotionTools.ConcatenateDeltas(h, t);
                }
                newDelta = RootMotionTools.ApplyWeight(newDelta, weight);
                delta    = RootMotionTools.AddDeltas(delta, newDelta);
            }
        }

        public TransformQvvs rawDelta => delta;
        public TransformQvvs normalizedDelta
        {
            get
            {
                var r = rawDelta;
                r.NormalizeBone();
                return r;
            }
        }
    }

    public static class RootMotionTools
    {
        public static TransformQvvs DeltaBetween(in TransformQvvs current, in TransformQvvs previous)
        {
            return new TransformQvvs
            {
                position   = current.position - previous.position,
                rotation   = math.mul(current.rotation, math.inverse(previous.rotation)),
                worldIndex = current.worldIndex,
                scale      = current.scale / previous.scale,
                stretch    = current.stretch / previous.stretch
            };
        }

        public static TransformQvvs ApplyWeight(TransformQvvs bone, float weight)
        {
            bone.position       *= weight;
            bone.rotation.value *= weight;
            bone.scale          *= weight;
            bone.stretch        *= weight;
            bone.worldIndex      = math.asint(math.asfloat(bone.worldIndex) * weight);
            return bone;
        }

        public static TransformQvvs AddDeltas(in TransformQvvs deltaA, in TransformQvvs deltaB)
        {
            return new TransformQvvs
            {
                position   = deltaA.position + deltaB.position,
                rotation   = deltaA.rotation.value + deltaB.rotation.value,
                scale      = deltaA.scale + deltaB.scale,
                stretch    = deltaA.stretch + deltaB.stretch,
                worldIndex = math.asint(math.asfloat(deltaA.worldIndex) + math.asfloat(deltaB.worldIndex)),
            };
        }

        public static TransformQvvs ConcatenateDeltas(in TransformQvvs deltaFirst, in TransformQvvs deltaSecond)
        {
            return new TransformQvvs
            {
                position   = deltaFirst.position + deltaSecond.position,
                rotation   = math.mul(deltaSecond.rotation, deltaFirst.rotation),
                scale      = deltaFirst.scale * deltaSecond.scale,
                stretch    = deltaFirst.stretch * deltaSecond.stretch,
                worldIndex = deltaFirst.worldIndex,
            };
        }
    }
}

