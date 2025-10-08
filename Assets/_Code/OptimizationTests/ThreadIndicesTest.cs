using Latios.Calci;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.PerformanceTesting;

namespace OptimizationTests
{
    [BurstCompile]
    public static class BurstMethodsTests
    {
        [BurstCompile(FloatMode = FloatMode.Deterministic)]
        public static int TestComparisonGT(in float4 a, in float4 b)
        {
            return math.bitmask(a > b);
        }
    }

    public class ThreadIndicesTest
    {
        [BurstCompile]
        struct InjectedIndexJob : IJob
        {
            public NativeArray<int> writeZone;
            public Rng              writeScatterer;

            public int iterations;

            [NativeSetThreadIndex]
            int m_threadIndex;

            public void Execute()
            {
                var rng = writeScatterer.GetSequence(m_threadIndex);
                for (int i = 0; i < iterations; i++)
                {
                    writeZone[rng.NextInt(0, writeZone.Length)] += m_threadIndex;
                }
            }
        }

        [BurstCompile]
        struct QueriedIndexJob : IJob
        {
            public NativeArray<int> writeZone;
            public Rng              writeScatterer;

            public int iterations;

            public void Execute()
            {
                var rng = writeScatterer.GetSequence(Unity.Jobs.LowLevel.Unsafe.JobsUtility.ThreadIndex);
                for (int i = 0; i < iterations; i++)
                {
                    writeZone[rng.NextInt(0, writeZone.Length)] += Unity.Jobs.LowLevel.Unsafe.JobsUtility.ThreadIndex;
                }
            }
        }

        [Test, Performance]
        public void CompareIndicesAccess()
        {
            var arrayInjected = new NativeArray<int>(100, Allocator.TempJob);
            var arrayQueried  = new NativeArray<int>(100, Allocator.TempJob);

            var iterations = 10000000;
            var rng        = new Rng("ThreadIndicesTest");

            var injectedJob = new InjectedIndexJob
            {
                writeZone      = arrayInjected,
                iterations     = 10,
                writeScatterer = rng
            };

            var queriedJob = new QueriedIndexJob
            {
                writeZone      = arrayQueried,
                iterations     = 10,
                writeScatterer = rng
            };

            injectedJob.Schedule().Complete();
            queriedJob.Schedule().Complete();
            injectedJob.iterations = iterations;
            queriedJob.iterations  = iterations;

            Measure.Method(() => { injectedJob.Schedule().Complete(); })
            .SampleGroup(new SampleGroup("Injected", SampleUnit.Millisecond))
            .WarmupCount(0)
            .MeasurementCount(1)
            .Run();

            Measure.Method(() => { queriedJob.Schedule().Complete(); })
            .SampleGroup(new SampleGroup("Queried", SampleUnit.Millisecond))
            .WarmupCount(0)
            .MeasurementCount(1)
            .Run();
        }
    }
}

