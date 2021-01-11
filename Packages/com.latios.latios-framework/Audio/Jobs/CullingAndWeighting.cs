using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace Latios.Audio
{
    internal static class CullingAndWeighting
    {
        public const int BATCH_SIZE = 128;

        //Parallel
        //The weighting algorithm is fairly pricey
        [BurstCompile]
        public struct OneshotsJob : IJobParallelForBatch
        {
            [ReadOnly] public NativeList<ListenerWithTransform>               listenersWithTransforms;
            [ReadOnly] public NativeArray<OneshotEmitter>                     emitters;
            [NativeDisableParallelForRestriction] public NativeArray<Weights> weights;
            public NativeStream.Writer                                        ranges;  //int3: listener, emitterStart, emitterCount

            public void Execute(int startIndex, int count)
            {
                var baseWeights = new NativeArray<Weights>(listenersWithTransforms.Length, Allocator.Temp, NativeArrayOptions.ClearMemory);
                for (int i = 0; i < listenersWithTransforms.Length; i++)
                {
                    int c = listenersWithTransforms[i].listener.ildProfile.anglesPerLeftChannel.Length +
                            listenersWithTransforms[i].listener.ildProfile.anglesPerRightChannel.Length;
                    Weights w = default;
                    for (int j = 0; j < c; j++)
                    {
                        w.channelWeights.Add(0f);
                    }
                    c = listenersWithTransforms[i].listener.interAuralTimeDelayResolution;
                    c = 2 * c + 1;
                    for (int j = 0; j < c; j++)
                    {
                        w.itdWeights.Add(0f);
                    }
                    baseWeights[i] = w;
                }

                ranges.BeginForEachIndex(startIndex / BATCH_SIZE);
                var activeRanges = new NativeArray<int2>(listenersWithTransforms.Length, Allocator.Temp, NativeArrayOptions.ClearMemory);

                for (int i = startIndex; i < startIndex + count; i++)
                {
                    var emitter = emitters[i];
                    for (int j = 0; j < listenersWithTransforms.Length; j++)
                    {
                        if (math.distancesq(emitter.transform.pos, listenersWithTransforms[j].transform.pos) < emitter.source.outerRange * emitter.source.outerRange)
                        {
                            var w = baseWeights[j];

                            EmitterParameters e = new EmitterParameters
                            {
                                cone            = emitter.cone,
                                innerRange      = emitter.source.innerRange,
                                outerRange      = emitter.source.outerRange,
                                rangeFadeMargin = emitter.source.rangeFadeMargin,
                                transform       = emitter.transform,
                                useCone         = emitter.useCone,
                                volume          = emitter.source.volume
                            };
                            ComputeWeights(ref w, e, in listenersWithTransforms.ElementAt(j));

                            weights[i * listenersWithTransforms.Length + j] = w;

                            if (activeRanges[j].y == 0)
                                activeRanges[j] = new int2(i, 1);
                            else
                                activeRanges[j] += new int2(0, 1);
                        }
                        else if (activeRanges[j].y > 0)
                        {
                            ranges.Write(new int3(j, activeRanges[j]));
                            activeRanges[j] = int2.zero;
                        }
                    }
                }

                ranges.EndForEachIndex();
            }
        }

        //Parallel
        //The weighting algorithm is fairly pricey
        [BurstCompile]
        public struct LoopedJob : IJobParallelForBatch
        {
            [ReadOnly] public NativeList<ListenerWithTransform>               listenersWithTransforms;
            [ReadOnly] public NativeArray<LoopedEmitter>                      emitters;
            [NativeDisableParallelForRestriction] public NativeArray<Weights> weights;
            public NativeStream.Writer                                        ranges;  //int3: listener, emitterStart, emitterCount

            public void Execute(int startIndex, int count)
            {
                var baseWeights = new NativeArray<Weights>(listenersWithTransforms.Length, Allocator.Temp, NativeArrayOptions.ClearMemory);
                for (int i = 0; i < listenersWithTransforms.Length; i++)
                {
                    int c = listenersWithTransforms[i].listener.ildProfile.anglesPerLeftChannel.Length +
                            listenersWithTransforms[i].listener.ildProfile.anglesPerRightChannel.Length;
                    Weights w = default;
                    for (int j = 0; j < c; j++)
                    {
                        w.channelWeights.Add(0f);
                    }
                    c = listenersWithTransforms[i].listener.interAuralTimeDelayResolution;
                    c = 2 * c + 1;
                    for (int j = 0; j < c; j++)
                    {
                        w.itdWeights.Add(0f);
                    }
                    baseWeights[i] = w;
                }

                ranges.BeginForEachIndex(startIndex / BATCH_SIZE);
                var activeRanges = new NativeArray<int2>(listenersWithTransforms.Length, Allocator.Temp, NativeArrayOptions.ClearMemory);

                for (int i = startIndex; i < startIndex + count; i++)
                {
                    var emitter = emitters[i];
                    for (int j = 0; j < listenersWithTransforms.Length; j++)
                    {
                        if (math.distancesq(emitter.transform.pos, listenersWithTransforms[j].transform.pos) < emitter.source.outerRange * emitter.source.outerRange)
                        {
                            var w = baseWeights[j];

                            EmitterParameters e = new EmitterParameters
                            {
                                cone            = emitter.cone,
                                innerRange      = emitter.source.innerRange,
                                outerRange      = emitter.source.outerRange,
                                rangeFadeMargin = emitter.source.rangeFadeMargin,
                                transform       = emitter.transform,
                                useCone         = emitter.useCone,
                                volume          = emitter.source.volume
                            };
                            ComputeWeights(ref w, e, in listenersWithTransforms.ElementAt(j));

                            weights[i * listenersWithTransforms.Length + j] = w;

                            if (activeRanges[j].y == 0)
                                activeRanges[j] = new int2(i, 1);
                            else
                                activeRanges[j] += new int2(0, 1);
                        }
                        else if (activeRanges[j].y > 0)
                        {
                            ranges.Write(new int3(j, activeRanges[j]));
                            activeRanges[j] = int2.zero;
                        }
                    }
                }

                ranges.EndForEachIndex();
            }
        }

        private struct EmitterParameters
        {
            public float volume;
            public float innerRange;
            public float outerRange;
            public float rangeFadeMargin;

            public RigidTransform         transform;
            public AudioSourceEmitterCone cone;
            public bool                   useCone;
        }

        private static void ComputeWeights(ref Weights weights, EmitterParameters emitter, in ListenerWithTransform listener)
        {
            float volume = emitter.volume * listener.listener.volume;

            var emitterInListenerSpace    = math.mul(math.inverse(listener.transform), emitter.transform);
            var emitterPositionNormalized = math.normalizesafe(emitterInListenerSpace.pos, float3.zero);

            //attenuation
            {
                float d     = math.length(emitterInListenerSpace.pos);
                float atten = 1f;
                if (d > emitter.innerRange)
                {
                    //The offset is the distance from the innerRange minus 1 unit clamped between the innerRange and the margin.
                    //The minus one offset ensures the falloff is always 1 or larger, making the transition betweem the innerRange
                    //and the falloff region continuous (by calculus terminology).
                    float falloff = math.min(d, emitter.outerRange - emitter.rangeFadeMargin) - (emitter.innerRange - 1f);
                    atten         = math.saturate(math.rcp(falloff * falloff));
                }
                if (d > emitter.outerRange - emitter.rangeFadeMargin)
                {
                    float factor = (d - (emitter.outerRange - emitter.rangeFadeMargin)) / emitter.rangeFadeMargin;
                    factor       = math.saturate(factor);
                    atten        = math.lerp(atten, 0f, factor);
                }

                if (emitter.useCone)
                {
                    float cosine = math.dot(math.forward(emitterInListenerSpace.rot), -emitterPositionNormalized);
                    if (cosine <= emitter.cone.cosOuterAngle)
                    {
                        atten *= emitter.cone.outerAngleAttenuation;
                    }
                    else if (cosine < emitter.cone.cosInnerAngle)
                    {
                        float factor  = math.unlerp(emitter.cone.cosOuterAngle, emitter.cone.cosInnerAngle, cosine);
                        atten        *= math.lerp(emitter.cone.outerAngleAttenuation, 1f, factor);
                    }
                }
                volume *= atten;
            }

            //ITD
            {
                float itd                     = (math.dot(emitterPositionNormalized, math.right()) * 0.5f + 0.5f) * weights.itdWeights.Length;
                float frac                    = math.modf(itd, out float integer);
                int   indexLow                = math.clamp((int)integer, 0, weights.itdWeights.Length);
                int   indexHigh               = math.clamp(indexLow + 1, 0, weights.itdWeights.Length);
                weights.itdWeights[indexLow]  = volume * frac;
                weights.itdWeights[indexHigh] = volume * (1f - frac);
            }

            //ILD
            {
                float2 xz     = math.normalizesafe(emitterPositionNormalized.xz, new float2(0f, 1f));
                float2 angles = default;
                angles.x      = math.atan2(xz.y, xz.x);
                float2 yz     = math.normalizesafe(emitterPositionNormalized.yz, new float2(1f, 0f));
                angles.y      = math.atan2(yz.y, yz.x);

                //First, find if there is a perfect match
                for (int i = 0; i < listener.listener.ildProfile.anglesPerLeftChannel.Length; i++)
                {
                    bool perfectMatch = math.all(((angles >= listener.listener.ildProfile.anglesPerLeftChannel[i].xz) &
                                                  (angles <= listener.listener.ildProfile.anglesPerLeftChannel[i].yw)) |
                                                 ((angles + 2f * math.PI >= listener.listener.ildProfile.anglesPerLeftChannel[i].xz) &
                                                  (angles + 2f * math.PI <= listener.listener.ildProfile.anglesPerLeftChannel[i].yw)));
                    if (perfectMatch)
                    {
                        weights.channelWeights[i] = 1f;  //The ratio gets applied graph-side.
                        return;
                    }
                }

                for (int i = 0; i < listener.listener.ildProfile.anglesPerRightChannel.Length; i++)
                {
                    bool perfectMatch = math.all(((angles >= listener.listener.ildProfile.anglesPerRightChannel[i].xz) &
                                                  (angles <= listener.listener.ildProfile.anglesPerRightChannel[i].yw)) |
                                                 ((angles + 2f * math.PI >= listener.listener.ildProfile.anglesPerRightChannel[i].xz) &
                                                  (angles + 2f * math.PI <= listener.listener.ildProfile.anglesPerRightChannel[i].yw)));
                    if (perfectMatch)
                    {
                        weights.channelWeights[i + listener.listener.ildProfile.anglesPerLeftChannel.Length] = 1f;  //The ratio gets applied graph-side.
                        return;
                    }
                }

                //No perfect match.
                int4              bestMinMaxXYIndices = default;  //This should always be overwritten
                float4            bestAngleDeltas     = new float4(-2f * math.PI, 2f * math.PI, -2f * math.PI, 2f * math.PI);
                FixedListInt128   candidateChannels   = default;
                FixedListFloat128 candidateDistances  = default;

                //Find our limits
                var                 leftChannelDeltas   = listener.listener.ildProfile.anglesPerLeftChannel;
                var                 rightChannelDeltas  = listener.listener.ildProfile.anglesPerRightChannel;
                FixedList512<bool2> leftChannelInsides  = default;
                FixedList512<bool2> rightChannelInsides = default;
                for (int i = 0; i < leftChannelDeltas.Length; i++)
                {
                    var delta  = leftChannelDeltas[i] - angles.xyxy;
                    var temp   = delta;
                    delta     += math.select(0f, new float4(2f * math.PI, -2f * math.PI, 2f * math.PI, -2f * math.PI), delta * new float4(1f, -1f, 1f, -1f) < 0f);
                    delta     -= math.select(0f,
                                             new float4(2f * math.PI, -2f * math.PI, 2f * math.PI, -2f * math.PI),
                                             delta * new float4(1f, -1f, 1f, -1f) >= 2f * math.PI);
                    temp                 -= math.select(0f, 2f * math.PI, temp.xxzz > 0f);
                    bool2 inside          = temp.yw >= 0f;
                    leftChannelDeltas[i]  = delta;
                    leftChannelInsides.Add(inside);
                }
                for (int i = 0; i < rightChannelDeltas.Length; i++)
                {
                    var delta  = rightChannelDeltas[i] - angles.xyxy;
                    var temp   = delta;
                    delta     += math.select(0f, new float4(2f * math.PI, -2f * math.PI, 2f * math.PI, -2f * math.PI), delta * new float4(1f, -1f, 1f, -1f) < 0f);
                    delta     -= math.select(0f,
                                             new float4(2f * math.PI, -2f * math.PI, 2f * math.PI, -2f * math.PI),
                                             delta * new float4(1f, -1f, 1f, -1f) >= 2f * math.PI);
                    temp                  -= math.select(0f, 2f * math.PI, temp.xxzz > 0f);
                    bool2 inside           = temp.yw >= 0f;
                    rightChannelDeltas[i]  = delta;
                    rightChannelInsides.Add(inside);
                }
                //By this point, any delta should be (positive, negative, positive, negative)

                //Find our search region
                for (int i = 0; i < leftChannelDeltas.Length; i++)
                {
                    bool2 inside = leftChannelInsides[i];
                    var   delta  = leftChannelDeltas[i];
                    if (inside.x)
                    {
                        //above
                        if (delta.z <= bestAngleDeltas.z)
                        {
                            delta.z               = bestAngleDeltas.z;
                            bestMinMaxXYIndices.z = i;
                        }
                        //below
                        if (delta.w >= bestAngleDeltas.w)
                        {
                            delta.w               = bestAngleDeltas.w;
                            bestMinMaxXYIndices.w = i;
                        }
                    }
                    if (inside.y)
                    {
                        //right
                        if (delta.x <= bestAngleDeltas.x)
                        {
                            delta.x               = bestAngleDeltas.x;
                            bestMinMaxXYIndices.x = i;
                        }
                        //left
                        if (delta.y >= bestAngleDeltas.y)
                        {
                            delta.y               = bestAngleDeltas.y;
                            bestMinMaxXYIndices.y = i;
                        }
                    }
                }
                for (int i = 0; i < rightChannelDeltas.Length; i++)
                {
                    bool2 inside = rightChannelInsides[i];
                    var   delta  = rightChannelDeltas[i];
                    if (inside.x)
                    {
                        //above
                        if (delta.z <= bestAngleDeltas.z)
                        {
                            delta.z               = bestAngleDeltas.z;
                            bestMinMaxXYIndices.z = i + leftChannelDeltas.Length;
                        }
                        //below
                        if (delta.w >= bestAngleDeltas.w)
                        {
                            delta.w               = bestAngleDeltas.w;
                            bestMinMaxXYIndices.w = i + leftChannelDeltas.Length;
                        }
                    }
                    if (inside.y)
                    {
                        //right
                        if (delta.x <= bestAngleDeltas.x)
                        {
                            delta.x               = bestAngleDeltas.x;
                            bestMinMaxXYIndices.x = i + leftChannelDeltas.Length;
                        }
                        //left
                        if (delta.y >= bestAngleDeltas.y)
                        {
                            delta.y               = bestAngleDeltas.y;
                            bestMinMaxXYIndices.y = i + leftChannelDeltas.Length;
                        }
                    }
                }

                //Add our constraining indices to the pot
                candidateChannels.Add(bestMinMaxXYIndices.x);
                candidateDistances.Add(bestAngleDeltas.x);
                if (bestMinMaxXYIndices.x != bestMinMaxXYIndices.y)
                    candidateChannels.Add(bestMinMaxXYIndices.y);
                else
                    candidateDistances[0] = math.min(candidateDistances[0], bestAngleDeltas.y);

                if (math.all(bestMinMaxXYIndices.xy != bestMinMaxXYIndices.z))
                    candidateChannels.Add(bestMinMaxXYIndices.z);
                else if (bestMinMaxXYIndices.x == bestMinMaxXYIndices.z)
                    candidateDistances[0] = math.min(candidateDistances[0], bestAngleDeltas.z);
                else
                    candidateDistances[1] = math.min(candidateDistances[1], bestAngleDeltas.z);

                if (math.all(bestMinMaxXYIndices.xyz != bestMinMaxXYIndices.w))
                    candidateChannels.Add(bestMinMaxXYIndices.w);
                else if (bestMinMaxXYIndices.x == bestMinMaxXYIndices.w)
                    candidateDistances[0] = math.min(candidateDistances[0], bestAngleDeltas.w);
                else if (bestMinMaxXYIndices.y == bestMinMaxXYIndices.w)
                    candidateDistances[1] = math.min(candidateDistances[1], bestAngleDeltas.w);
                else
                    candidateDistances[2] = math.min(candidateDistances[2], bestAngleDeltas.w);

                //Add additional candidates
                for (int i = 0; i < leftChannelDeltas.Length; i++)
                {
                    if (math.any(i == bestMinMaxXYIndices))
                        continue;

                    float4 delta = leftChannelDeltas[i];
                    bool   added = false;
                    int    c     = candidateDistances.Length;
                    if (math.all(delta.xz < bestAngleDeltas.xz))
                    {
                        candidateChannels.Add(i);
                        candidateDistances.Add(math.length(delta.xz));
                        added = true;
                    }
                    if (delta.y > bestAngleDeltas.y && delta.z < bestAngleDeltas.z)
                    {
                        if (added)
                        {
                            candidateDistances[c] = math.min(candidateDistances[c], math.length(delta.yz));
                        }
                        else
                        {
                            candidateChannels.Add(i);
                            candidateDistances.Add(math.length(delta.yz));
                            added = true;
                        }
                    }
                    if (delta.x < bestAngleDeltas.x && delta.w < bestAngleDeltas.w)
                    {
                        if (added)
                        {
                            candidateDistances[c] = math.min(candidateDistances[c], math.length(delta.xw));
                        }
                        else
                        {
                            candidateChannels.Add(i);
                            candidateDistances.Add(math.length(delta.xw));
                            added = true;
                        }
                    }
                    if (math.all(delta.yw > bestAngleDeltas.yw))
                    {
                        if (added)
                        {
                            candidateDistances[c] = math.min(candidateDistances[c], math.length(delta.yw));
                        }
                        else
                        {
                            candidateChannels.Add(i);
                            candidateDistances.Add(math.length(delta.yw));
                        }
                    }
                }
                for (int i = 0; i < rightChannelDeltas.Length; i++)
                {
                    if (math.any(i == bestMinMaxXYIndices))
                        continue;

                    float4 delta = rightChannelDeltas[i];
                    bool   added = false;
                    int    c     = candidateDistances.Length;
                    if (math.all(delta.xz < bestAngleDeltas.xz))
                    {
                        candidateChannels.Add(i + leftChannelDeltas.Length);
                        candidateDistances.Add(math.length(delta.xz));
                        added = true;
                    }
                    if (delta.y > bestAngleDeltas.y && delta.z < bestAngleDeltas.z)
                    {
                        if (added)
                        {
                            candidateDistances[c] = math.min(candidateDistances[c], math.length(delta.yz));
                        }
                        else
                        {
                            candidateChannels.Add(i + leftChannelDeltas.Length);
                            candidateDistances.Add(math.length(delta.yz));
                            added = true;
                        }
                    }
                    if (delta.x < bestAngleDeltas.x && delta.w < bestAngleDeltas.w)
                    {
                        if (added)
                        {
                            candidateDistances[c] = math.min(candidateDistances[c], math.length(delta.xw));
                        }
                        else
                        {
                            candidateChannels.Add(i + leftChannelDeltas.Length);
                            candidateDistances.Add(math.length(delta.xw));
                            added = true;
                        }
                    }
                    if (math.all(delta.yw > bestAngleDeltas.yw))
                    {
                        if (added)
                        {
                            candidateDistances[c] = math.min(candidateDistances[c], math.length(delta.yw));
                        }
                        else
                        {
                            candidateChannels.Add(i + leftChannelDeltas.Length);
                            candidateDistances.Add(math.length(delta.yw));
                        }
                    }
                }

                //Compute weights
                float maxLength = 0f;
                for (int i = 0; i < candidateDistances.Length; i++)
                {
                    maxLength = math.max(maxLength, candidateDistances[i]);
                }
                float sum = 0f;
                for (int i = 0; i < candidateDistances.Length; i++)
                {
                    candidateDistances[i]  = maxLength - candidateDistances[i];
                    sum                   += candidateDistances[i];
                }
                for (int i = 0; i < candidateDistances.Length; i++)
                {
                    weights.channelWeights[candidateChannels[i]] = candidateDistances[i] / sum;
                }
            }
        }
    }
}

