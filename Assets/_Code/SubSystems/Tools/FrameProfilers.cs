using System;
using System.Diagnostics;
using Latios;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace Lsss.Tools
{
    public struct ProfilingData : ICollectionComponent
    {
        public Stopwatch            frameStopwatch;
        public Stopwatch            gpuStopwatch;
        public NativeArray<float>   cpuShort;
        public NativeArray<float>   cpuMin;
        public NativeArray<float>   cpuAverage;
        public NativeArray<float>   cpuMax;
        public NativeArray<float>   gpuShort;
        public NativeArray<float>   gpuMin;
        public NativeArray<float>   gpuAverage;
        public NativeArray<float>   gpuMax;
        public NativeArray<Color32> image;
        public NativeArray<float>   barValues;

        public Type AssociatedComponentType => typeof(ProfilingDataTag);

        public JobHandle Dispose(JobHandle inputDeps)
        {
            var handles = new NativeArray<JobHandle>(10, Allocator.TempJob);
            handles[0]  = cpuShort.Dispose(inputDeps);
            handles[1]  = cpuMin.Dispose(inputDeps);
            handles[2]  = cpuAverage.Dispose(inputDeps);
            handles[3]  = cpuMax.Dispose(inputDeps);
            handles[4]  = gpuShort.Dispose(inputDeps);
            handles[5]  = gpuMin.Dispose(inputDeps);
            handles[6]  = gpuAverage.Dispose(inputDeps);
            handles[7]  = gpuMax.Dispose(inputDeps);
            handles[8]  = image.Dispose(inputDeps);
            handles[9]  = barValues.Dispose(inputDeps);
            var result  = JobHandle.CombineDependencies(handles);
            handles.Dispose();
            return result;
        }
    }

    public struct ProfilingDataTag : IComponentData { }

    public partial class BeginFrameProfilingSystem : SubSystem
    {
        private const int kFramesPerStat = 60;
        private const int kBarsPerGraph  = 3;

        private int m_sampleCounter = 0;

        protected override void OnCreate()
        {
            var profilingData = new ProfilingData
            {
                frameStopwatch = new Stopwatch(),
                gpuStopwatch   = new Stopwatch(),
                cpuShort       = new NativeArray<float>(256, Allocator.Persistent),
                cpuMin         = new NativeArray<float>(256, Allocator.Persistent),
                cpuAverage     = new NativeArray<float>(256, Allocator.Persistent),
                cpuMax         = new NativeArray<float>(256, Allocator.Persistent),
                gpuShort       = new NativeArray<float>(256, Allocator.Persistent),
                gpuMin         = new NativeArray<float>(256, Allocator.Persistent),
                gpuAverage     = new NativeArray<float>(256, Allocator.Persistent),
                gpuMax         = new NativeArray<float>(256, Allocator.Persistent),
                image          = new NativeArray<Color32>(256 * 256, Allocator.Persistent),
                barValues      = new NativeArray<float>(kBarsPerGraph * 2, Allocator.Persistent)
            };
            worldBlackboardEntity.AddCollectionComponent(profilingData);
        }

        bool m_firstFrame = true;

        protected override void OnUpdate()
        {
            bool needsStatsUpdate = false;
            while (m_sampleCounter >= kFramesPerStat)
            {
                m_sampleCounter  -= kFramesPerStat;
                needsStatsUpdate  = true;
            }
            m_sampleCounter++;

            var profilingData = worldBlackboardEntity.GetCollectionComponent<ProfilingData>(false);
            CompleteDependency();
            if (m_firstFrame)
            {
                m_firstFrame = false;
                profilingData.frameStopwatch.Start();
                return;
            }

            profilingData.frameStopwatch.Stop();
#if UNITY_EDITOR
            if (!m_firstFrame)
                profilingData.gpuStopwatch.Stop();
#endif

            var frameTime = profilingData.frameStopwatch.Elapsed;
            profilingData.frameStopwatch.Reset();
            profilingData.frameStopwatch.Start();

            float frameDuration = (float)((double)frameTime.Ticks / TimeSpan.TicksPerMillisecond);

            var   gpuTime     = profilingData.gpuStopwatch.Elapsed;
            float gpuDuration = (float)((double)gpuTime.Ticks / TimeSpan.TicksPerMillisecond);

            var jh = new AppendStatAndShiftJob
            {
                array       = profilingData.cpuShort,
                appendValue = frameDuration - gpuDuration
            }.Schedule();
            jh = new AppendStatAndShiftJob
            {
                array       = profilingData.gpuShort,
                appendValue = frameDuration
            }.Schedule(jh);

            if (Keyboard.current.leftBracketKey.wasPressedThisFrame)
            {
                jh = new ResetJob
                {
                    cpuMin     = profilingData.cpuMin,
                    cpuAverage = profilingData.cpuAverage,
                    cpuMax     = profilingData.cpuMax,
                    gpuMin     = profilingData.gpuMin,
                    gpuAverage = profilingData.gpuAverage,
                    gpuMax     = profilingData.gpuMax,
                }.Schedule(jh);
            }

            if (needsStatsUpdate)
            {
                jh = new ComputeStatsJob
                {
                    array    = profilingData.cpuShort,
                    minArray = profilingData.cpuMin,
                    avgArray = profilingData.cpuAverage,
                    maxArray = profilingData.cpuMax
                }.Schedule(jh);

                jh = new ComputeStatsJob
                {
                    array    = profilingData.gpuShort,
                    minArray = profilingData.gpuMin,
                    avgArray = profilingData.gpuAverage,
                    maxArray = profilingData.gpuMax
                }.Schedule(jh);
            }

            jh = new GenerateImageJob
            {
                cpuShort   = profilingData.cpuShort,
                cpuMin     = profilingData.cpuMin,
                cpuAverage = profilingData.cpuAverage,
                cpuMax     = profilingData.cpuMax,
                gpuShort   = profilingData.gpuShort,
                gpuMin     = profilingData.gpuMin,
                gpuAverage = profilingData.gpuAverage,
                gpuMax     = profilingData.gpuMax,
                image      = profilingData.image,
                barValues  = profilingData.barValues,
            }.Schedule(jh);

            worldBlackboardEntity.UpdateJobDependency<ProfilingData>(jh, false);
        }

        [BurstCompile]
        struct AppendStatAndShiftJob : IJob
        {
            public NativeArray<float> array;
            public float              appendValue;

            public void Execute()
            {
                for (int i = 0; i < array.Length - 1; i++)
                {
                    array[i] = array[i + 1];
                }
                array[array.Length - 1] = appendValue;
            }
        }

        [BurstCompile]
        struct ComputeStatsJob : IJob
        {
            [ReadOnly] public NativeArray<float> array;
            public NativeArray<float>            minArray;
            public NativeArray<float>            avgArray;
            public NativeArray<float>            maxArray;

            public void Execute()
            {
                float min = float.MaxValue;
                float max = 0f;
                float sum = 0f;
                for (int i = array.Length - kFramesPerStat; i < array.Length; i++)
                {
                    float v  = array[i];
                    min      = math.min(min, v);
                    max      = math.max(max, v);
                    sum     += v;
                }
                float avg = sum / kFramesPerStat;

                new AppendStatAndShiftJob
                {
                    array       = minArray,
                    appendValue = min
                }.Execute();

                new AppendStatAndShiftJob
                {
                    array       = avgArray,
                    appendValue = avg
                }.Execute();

                new AppendStatAndShiftJob
                {
                    array       = maxArray,
                    appendValue = max
                }.Execute();
            }
        }

        [BurstCompile]
        struct GenerateImageJob : IJob
        {
            [ReadOnly] public NativeArray<float> cpuShort;
            [ReadOnly] public NativeArray<float> cpuMin;
            [ReadOnly] public NativeArray<float> cpuAverage;
            [ReadOnly] public NativeArray<float> cpuMax;
            [ReadOnly] public NativeArray<float> gpuShort;
            [ReadOnly] public NativeArray<float> gpuMin;
            [ReadOnly] public NativeArray<float> gpuAverage;
            [ReadOnly] public NativeArray<float> gpuMax;
            public NativeArray<Color32>          image;
            public NativeArray<float>            barValues;

            public void Execute()
            {
                //Clear image
                for (int i = 0; i < image.Length; i++)
                {
                    image[i] = new Color32(0, 0, 0, 0);
                }

                //Short graph
                float min = float.MaxValue;
                float max = 0f;
                for (int i = 0; i < 256; i++)
                {
                    min = math.min(math.min(cpuShort[i], gpuShort[i]), min);
                    max = math.max(math.max(cpuShort[i], gpuShort[i]), max);
                }
                float goalFrame = 1000f / 60f;
                if (max < goalFrame)
                {
                    float barInc = goalFrame / (kBarsPerGraph - 1);
                    for (int i = 0; i < kBarsPerGraph; i++)
                    {
                        barValues[kBarsPerGraph - 1 - i] = i * barInc;
                    }
                }
                else
                {
                    int low  = (int)math.floor(min / goalFrame);
                    low      = math.min(low, (int)math.exp2(math.floorlog2(low)));
                    int high = (int)math.floor(max / goalFrame);
                    int inc  = (int)math.ceil((high - low) / (kBarsPerGraph - 1f));
                    for (int i = 0; i < kBarsPerGraph; i++)
                    {
                        barValues[kBarsPerGraph - 1 - i] = (low + i * inc) * goalFrame;
                    }
                }

                for (int y = 0; y < 120; y++)
                {
                    float yValue = math.lerp(barValues[kBarsPerGraph - 1], barValues[0], math.unlerp(0, 120, 119 - y));
                    for (int x = 0; x < 256; x++)
                    {
                        if (cpuShort[x] > yValue && cpuShort[x] < gpuShort[x])
                            image[256 * y + x] = new Color32(128, 128, 0, 192);
                        else if (gpuShort[x] > yValue)
                            image[256 * y + x] = new Color32(0, 128, 0, 192);
                    }
                }

                //Stats
                min = float.MaxValue;
                max = 0f;
                for (int i = 0; i < 256; i++)
                {
                    min = math.min(math.min(cpuMin[i], gpuMin[i]), min);
                    max = math.max(math.max(cpuMax[i], gpuMax[i]), max);
                }
                if (max < goalFrame)
                {
                    float barInc = goalFrame / (kBarsPerGraph - 1);
                    for (int i = 0; i < kBarsPerGraph; i++)
                    {
                        barValues[kBarsPerGraph - 1 - i + kBarsPerGraph] = i * barInc;
                    }
                }
                else
                {
                    int low  = (int)math.floor(min / goalFrame);
                    low      = math.min(low, (int)math.exp2(math.floorlog2(low)));
                    int high = (int)math.floor(max / goalFrame);
                    int inc  = (int)math.ceil((high - low) / (kBarsPerGraph - 1f));
                    for (int i = 0; i < kBarsPerGraph; i++)
                    {
                        barValues[kBarsPerGraph - 1 - i + kBarsPerGraph] = (low + i * inc) * goalFrame;
                    }
                }

                int statGraphBase = 256 * 136;
                for (int y = 0; y < 120; y++)
                {
                    float yValue = math.lerp(barValues[2 * kBarsPerGraph - 1], barValues[kBarsPerGraph], math.unlerp(0, 120, 119 - y));
                    for (int x = 0; x < 256; x++)
                    {
                        int   lowestValid = 0;
                        float lowestValue = float.MaxValue;
                        if (cpuMin[x] > yValue && cpuMin[x] < lowestValue)
                        {
                            lowestValid = 1;
                            lowestValue = cpuMin[x];
                        }
                        if (cpuAverage[x] > yValue && cpuAverage[x] < lowestValue)
                        {
                            lowestValid = 2;
                            lowestValue = cpuAverage[x];
                        }
                        if (cpuMax[x] > yValue && cpuMax[x] < lowestValue)
                        {
                            lowestValid = 3;
                            lowestValue = cpuMax[x];
                        }
                        if (gpuMin[x] > yValue && gpuMin[x] < lowestValue)
                        {
                            lowestValid = 4;
                            lowestValue = gpuMin[x];
                        }
                        if (gpuAverage[x] > yValue && gpuAverage[x] < lowestValue)
                        {
                            lowestValid = 5;
                            lowestValue = gpuAverage[x];
                        }
                        if (gpuMax[x] > yValue && gpuMax[x] < lowestValue)
                        {
                            lowestValid = 6;
                            lowestValue = gpuMax[x];
                        }
                        switch (lowestValid)
                        {
                            case 0: break;
                            case 1: image[statGraphBase + 256 * y + x] = new Color32(192, 192, 0, 192); break;
                            case 2: image[statGraphBase + 256 * y + x] = new Color32(128, 128, 0, 192); break;
                            case 3: image[statGraphBase + 256 * y + x] = new Color32(64, 64, 0, 192); break;
                            case 4: image[statGraphBase + 256 * y + x] = new Color32(0, 192, 0, 192); break;
                            case 5: image[statGraphBase + 256 * y + x] = new Color32(0, 128, 0, 192); break;
                            case 6: image[statGraphBase + 256 * y + x] = new Color32(0, 64, 0, 192); break;
                            default: break;
                        }
                    }
                }

                //Box and bars
                int barSpacing = 119 / (kBarsPerGraph - 1);
                for (int i = barSpacing; i < 119; i += barSpacing)
                {
                    DrawBar(i);
                    DrawBar(i + 136);
                }
                DrawBar(0);
                DrawBar(120);
                DrawBar(136);
                DrawBar(255);

                for (int i = 0; i < 256; i++)
                {
                    image[256 * i]       = new Color(127, 127, 127, 255);
                    image[256 * i + 255] = new Color(127, 127, 127, 255);
                }
            }

            void DrawBar(int yOffset)
            {
                int b = yOffset * 256;
                for (int i = 0; i < 256; i++)
                {
                    image[b + i] = new Color(127, 127, 127, 192);
                }
            }
        }

        [BurstCompile]
        struct ResetJob : IJob
        {
            public NativeArray<float> cpuMin;
            public NativeArray<float> cpuAverage;
            public NativeArray<float> cpuMax;
            public NativeArray<float> gpuMin;
            public NativeArray<float> gpuAverage;
            public NativeArray<float> gpuMax;

            public void Execute()
            {
                Reset(cpuMin);
                Reset(cpuAverage);
                Reset(cpuMax);
                Reset(gpuMin);
                Reset(gpuAverage);
                Reset(gpuMax);
            }

            void Reset(NativeArray<float> array)
            {
                for (int i = 0; i < array.Length; i++)
                    array[i] = 0f;
            }
        }
    }

    public partial class BeginGpuWaitProfilingSystem : SubSystem
    {
        protected override void OnUpdate()
        {
            var profilingData = worldBlackboardEntity.GetCollectionComponent<ProfilingData>(false);
            CompleteDependency();

            profilingData.gpuStopwatch.Reset();
            profilingData.gpuStopwatch.Start();
        }
    }

    //Only exists in builds
    public partial class EndGpuWaitProfilingSystem : SubSystem
    {
#if !UNITY_EDITOR
        protected override void OnCreate()
        {
            RenderPipelineManager.beginFrameRendering += RenderPipelineManager_beginFrameRendering;
        }

        protected override void OnDestroy()
        {
            RenderPipelineManager.beginFrameRendering -= RenderPipelineManager_beginFrameRendering;
        }

        private void RenderPipelineManager_beginFrameRendering(ScriptableRenderContext arg1, UnityEngine.Camera[] arg2)
        {
            Update();
        }
#endif

        protected override void OnUpdate()
        {
            var profilingData = worldBlackboardEntity.GetCollectionComponent<ProfilingData>(false);
            CompleteDependency();

            profilingData.gpuStopwatch.Stop();
        }
    }
}

