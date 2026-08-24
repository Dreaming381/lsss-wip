using System;
using System.Collections.Generic;
using System.Diagnostics;
using Unity.Burst;
using Unity.Mathematics;

namespace Latios.Calci
{
    public static partial class BezierMath
    {
        /// <summary>
        /// Evaluates a sorted sequence of KeyedCurve segments at many timepoints at once. Timepoints
        /// outside the sequence's range are clamped to its endpoints.
        /// </summary>
        /// <remarks>
        /// Prefer this over calling the single-time overload repeatedly in a loop. This version
        /// caches segment calculations and has vectorized fast-paths.
        ///
        /// The three spans must not overlap each other in memory. Violations will trigger a safety check.
        /// </remarks>
        /// <param name="segments">The curve's segments, sorted ascending by time.</param>
        /// <param name="times">The timepoints to sample at, in any order.</param>
        /// <param name="results">Receives one result per time. Must be at least as long as times.</param>
        public static unsafe void Evaluate(ReadOnlySpan<KeyedCurve> segments, ReadOnlySpan<float> times, Span<float> results)
        {
            CheckResultsLongEnough(times.Length, results.Length);
            if (times.Length == 0)
                return;

            fixed (KeyedCurve* segmentsPtr = segments)
            fixed (float*      timesPtr    = times)
            fixed (float*      resultsPtr  = results)
            {
                Evaluate(segmentsPtr, segments.Length, timesPtr, resultsPtr, times.Length);
            }
        }

        /// <summary>
        /// Evaluates a sorted sequence of KeyedCurve segments at many timepoints at once. Timepoints
        /// outside the sequence's range are clamped to its endpoints. This is a pointer-based version
        /// of the Span API above, with the same rules and restrictions.
        /// </summary>
        /// <param name="segments">The curve's segments, sorted ascending by time.</param>
        /// <param name="segmentCount">The number of segments. A curve of N keyframes has N-1.</param>
        /// <param name="times">The times to sample at, in any order.</param>
        /// <param name="results">Receives one result per time.</param>
        /// <param name="count">The number of times to sample.</param>
        public static unsafe void Evaluate(KeyedCurve* segments, int segmentCount, float* times, float* results, int count)
        {
            CheckNoOverlap(segments, segmentCount, times, results, count);
            EvaluateBatchNoAlias(segments, segmentCount, times, results, count);
        }

        // The cubic a segment describes, expressed in time offset from the segment's start so that
        // evaluation is one subtract and three fused multiply-adds with no division.
        struct HermiteSegmentCubic
        {
            public float leftTime;
            public float a;
            public float b;
            public float c;
            public float d;

            // Exact for an unweighted segment, which genuinely is a cubic polynomial in time. The
            // 1/dx factors that normalization would apply per sample are folded in here instead,
            // which is only worth doing because this runs once per segment rather than once per sample.
            public static HermiteSegmentCubic FromHermite(in KeyedCurve curve)
            {
                var dx = curve.rightTime - curve.leftTime;
                var p0 = curve.leftValue;
                var m0 = curve.leftTangentSlope * dx;
                var p1 = curve.rightValue;
                var m1 = curve.rightTangentSlope * dx;

                var a = 2f * p0 + m0 - 2f * p1 + m1;
                var b = -3f * p0 - 2f * m0 + 3f * p1 - m1;

                var inverse  = 1f / dx;
                var inverse2 = inverse * inverse;
                return new HermiteSegmentCubic
                {
                    leftTime = curve.leftTime,
                    a        = a * inverse2 * inverse,
                    b        = b * inverse2,
                    c        = curve.leftTangentSlope,
                    d        = p0,
                };
            }
        }

        static unsafe void EvaluateBatchNoAlias([NoAlias] KeyedCurve* segments,
                                                int segmentCount,
                                                [NoAlias] float*      times,
                                                [NoAlias] float*      results,
                                                int count)
        {
            if (count <= 0)
                return;
            if (segmentCount <= 0)
            {
                for (int i = 0; i < count; i++)
                    results[i] = 0f;
                return;
            }

            var lowTime  = segments[0].leftTime;
            var highTime = segments[segmentCount - 1].rightTime;

            // Todo: It is believed that beyond 4 segments, the select tree costs more than it saves.
            // But this was a guess, not proven. EvaluatePerSample falls back to the per-timepoint
            // public API. If any segment has weighted tangents, we use that, since vectorizing the
            // cubic solve path hasn't been figured out yet (it branches way too much and uses too
            // many division operations).
            if (segmentCount > 4 || !AllHermite(segments, segmentCount))
            {
                EvaluatePerSample(segments, segmentCount, times, results, count, lowTime, highTime);
                return;
            }

            switch (segmentCount)
            {
                case 1:
                    EvaluateHermite1(segments, times, results, count, lowTime, highTime);
                    break;
                case 2:
                    EvaluateHermite2(segments, times, results, count, lowTime, highTime);
                    break;
                case 3:
                    EvaluateHermite3(segments, times, results, count, lowTime, highTime);
                    break;
                default:
                    EvaluateHermite4(segments, times, results, count, lowTime, highTime);
                    break;
            }
        }

        static unsafe bool AllHermite([NoAlias] KeyedCurve* segments, int segmentCount)
        {
            for (int i = 0; i < segmentCount; i++)
            {
                if (segments[i].leftTangentWeight != Keyframe.kHermite || segments[i].rightTangentWeight != Keyframe.kHermite)
                    return false;
            }
            return true;
        }

        static float EvaluateCubic(float time, float leftTime, float a, float b, float c, float d)
        {
            var x = time - leftTime;
            return x * (x * (a * x + b) + c) + d;
        }

        static unsafe void EvaluateHermite1([NoAlias] KeyedCurve* segments,
                                            [NoAlias] float*      times,
                                            [NoAlias] float*      results,
                                            int count,
                                            float lowTime,
                                            float highTime)
        {
            var s0 = HermiteSegmentCubic.FromHermite(in segments[0]);
            for (int i = 0; i < count; i++)
            {
                var time   = math.clamp(times[i], lowTime, highTime);
                results[i] = EvaluateCubic(time, s0.leftTime, s0.a, s0.b, s0.c, s0.d);
            }
        }

        static unsafe void EvaluateHermite2([NoAlias] KeyedCurve* segments,
                                            [NoAlias] float*      times,
                                            [NoAlias] float*      results,
                                            int count,
                                            float lowTime,
                                            float highTime)
        {
            var s0 = HermiteSegmentCubic.FromHermite(in segments[0]);
            var s1 = HermiteSegmentCubic.FromHermite(in segments[1]);

            for (int i = 0; i < count; i++)
            {
                var time = math.clamp(times[i], lowTime, highTime);
                var ge1  = time >= s1.leftTime;

                var leftTime = math.select(s0.leftTime, s1.leftTime, ge1);
                var a        = math.select(s0.a, s1.a, ge1);
                var b        = math.select(s0.b, s1.b, ge1);
                var c        = math.select(s0.c, s1.c, ge1);
                var d        = math.select(s0.d, s1.d, ge1);

                results[i] = EvaluateCubic(time, leftTime, a, b, c, d);
            }
        }

        static unsafe void EvaluateHermite3([NoAlias] KeyedCurve* segments,
                                            [NoAlias] float*      times,
                                            [NoAlias] float*      results,
                                            int count,
                                            float lowTime,
                                            float highTime)
        {
            var s0 = HermiteSegmentCubic.FromHermite(in segments[0]);
            var s1 = HermiteSegmentCubic.FromHermite(in segments[1]);
            var s2 = HermiteSegmentCubic.FromHermite(in segments[2]);

            for (int i = 0; i < count; i++)
            {
                var  time = math.clamp(times[i], lowTime, highTime);
                bool ge1  = time >= s1.leftTime, ge2 = time >= s2.leftTime;

                var leftTime = math.select(math.select(s0.leftTime, s1.leftTime, ge1), s2.leftTime, ge2);
                var a        = math.select(math.select(s0.a, s1.a, ge1), s2.a, ge2);
                var b        = math.select(math.select(s0.b, s1.b, ge1), s2.b, ge2);
                var c        = math.select(math.select(s0.c, s1.c, ge1), s2.c, ge2);
                var d        = math.select(math.select(s0.d, s1.d, ge1), s2.d, ge2);

                results[i] = EvaluateCubic(time, leftTime, a, b, c, d);
            }
        }

        static unsafe void EvaluateHermite4([NoAlias] KeyedCurve* segments,
                                            [NoAlias] float*      times,
                                            [NoAlias] float*      results,
                                            int count,
                                            float lowTime,
                                            float highTime)
        {
            var s0 = HermiteSegmentCubic.FromHermite(in segments[0]);
            var s1 = HermiteSegmentCubic.FromHermite(in segments[1]);
            var s2 = HermiteSegmentCubic.FromHermite(in segments[2]);
            var s3 = HermiteSegmentCubic.FromHermite(in segments[3]);

            for (int i = 0; i < count; i++)
            {
                var  time = math.clamp(times[i], lowTime, highTime);
                bool ge1  = time >= s1.leftTime, ge2 = time >= s2.leftTime, ge3 = time >= s3.leftTime;

                var leftTime = math.select(math.select(s0.leftTime, s1.leftTime, ge1), math.select(s2.leftTime, s3.leftTime, ge3), ge2);
                var a        = math.select(math.select(s0.a, s1.a, ge1), math.select(s2.a, s3.a, ge3), ge2);
                var b        = math.select(math.select(s0.b, s1.b, ge1), math.select(s2.b, s3.b, ge3), ge2);
                var c        = math.select(math.select(s0.c, s1.c, ge1), math.select(s2.c, s3.c, ge3), ge2);
                var d        = math.select(math.select(s0.d, s1.d, ge1), math.select(s2.d, s3.d, ge3), ge2);

                results[i] = EvaluateCubic(time, leftTime, a, b, c, d);
            }
        }

        // The unspecialized path, for sequences that are too long for a select tree or contain a
        // weighted segment. Defers to the single-time overload per sample.
        static unsafe void EvaluatePerSample([NoAlias] KeyedCurve* segments,
                                             int segmentCount,
                                             [NoAlias] float*      times,
                                             [NoAlias] float*      results,
                                             int count,
                                             float lowTime,
                                             float highTime)
        {
            for (int i = 0; i < count; i++)
            {
                var time   = math.clamp(times[i], lowTime, highTime);
                var index  = FindSegment(segments, segmentCount, time);
                results[i] = Evaluate(in segments[index], time);
            }
        }

        // Orders segments by their right endpoint, which is what a time lookup searches against.
        struct RightTimeComparer : IComparer<KeyedCurve>
        {
            public int Compare(KeyedCurve x, KeyedCurve y) => x.rightTime.CompareTo(y.rightTime);
        }

        // Returns the index of the segment covering the given time, which must already be clamped to the sequence's range.
        // This path only runs for sequences too long for a select tree, or containing a weighted segment. Long sequences
        // are exactly where a linear scan is expensive, so this binary searches.
        static unsafe int FindSegment([NoAlias] KeyedCurve* segments, int segmentCount, float time)
        {
            if (segmentCount <= 1)
                return 0;

            var index = BinarySearch.FirstGreaterOrEqual(segments,
                                                         segmentCount,
                                                         new KeyedCurve { rightTime = time },
                                                         new RightTimeComparer());
            return math.min(index, segmentCount - 1);
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS"), Conditional("UNITY_DOTS_DEBUG")]
        static void CheckResultsLongEnough(int timesLength, int resultsLength)
        {
            if (resultsLength < timesLength)
                throw new ArgumentException($"results is {resultsLength} elements but must hold at least {timesLength}.");
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS"), Conditional("UNITY_DOTS_DEBUG")]
        static unsafe void CheckNoOverlap(KeyedCurve* segments, int segmentCount, float* times, float* results, int count)
        {
            var segmentsStart = (byte*)segments;
            var segmentsEnd   = (byte*)(segments + math.max(segmentCount, 0));
            var timesStart    = (byte*)times;
            var timesEnd      = (byte*)(times + math.max(count, 0));
            var resultsStart  = (byte*)results;
            var resultsEnd    = (byte*)(results + math.max(count, 0));

            if (Overlaps(timesStart, timesEnd, resultsStart, resultsEnd))
                throw new ArgumentException("times and results overlap. This API requires distinct buffers.");
            if (Overlaps(segmentsStart, segmentsEnd, resultsStart, resultsEnd))
                throw new ArgumentException("segments and results overlap. This API requires distinct buffers.");
        }

        static unsafe bool Overlaps(byte* aStart, byte* aEnd, byte* bStart, byte* bEnd) => aStart < bEnd && bStart < aEnd;
    }
}

