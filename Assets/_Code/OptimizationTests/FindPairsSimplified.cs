﻿using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace OptimizationAdventures
{
    [BurstCompile]
    public struct NaiveSweep : IJob
    {
        [ReadOnly] public NativeArray<AabbEntity> aabbs;
        public NativeList<EntityPair>             overlaps;

        public void Execute()
        {
            for (int i = 0; i < aabbs.Length - 1; i++)
            {
                AabbEntity current = aabbs[i];

                for (int j = i + 1; j < aabbs.Length && aabbs[j].aabb.min.x <= current.aabb.max.x; j++)
                {
                    if (!(current.aabb.max.y < aabbs[j].aabb.min.y ||
                          current.aabb.min.y > aabbs[j].aabb.max.y ||
                          current.aabb.max.z < aabbs[j].aabb.min.z ||
                                               current.aabb.min.z > aabbs[j].aabb.max.z))
                    {
                        overlaps.Add(new EntityPair(current.entity, aabbs[j].entity));
                    }
                }
            }
        }
    }

    [BurstCompile]
    public struct Bool4Sweep : IJob
    {
        [ReadOnly] public NativeArray<AabbEntity> aabbs;
        public NativeList<EntityPair>             overlaps;

        public void Execute()
        {
            for (int i = 0; i < aabbs.Length - 1; i++)
            {
                AabbEntity current = aabbs[i];

                for (int j = i + 1; j < aabbs.Length && aabbs[j].aabb.min.x <= current.aabb.max.x; j++)
                {
                    bool4 invalidated;
                    invalidated.x = current.aabb.max.y < aabbs[j].aabb.min.y;
                    invalidated.y = current.aabb.min.y > aabbs[j].aabb.max.y;
                    invalidated.z = current.aabb.max.z < aabbs[j].aabb.min.z;
                    invalidated.w = current.aabb.min.z > aabbs[j].aabb.max.z;
                    if (!math.any(invalidated))
                    {
                        overlaps.Add(new EntityPair(current.entity, aabbs[j].entity));
                    }
                }
            }
        }
    }

    [BurstCompile]
    public struct BoolPierreSweep : IJob
    {
        [ReadOnly] public NativeArray<AabbEntity> aabbs;
        public NativeList<EntityPair>             overlaps;

        public void Execute()
        {
            for (int i = 0; i < aabbs.Length - 1; i++)
            {
                AabbEntity current = aabbs[i];

                for (int j = i + 1; j < aabbs.Length && aabbs[j].aabb.min.x <= current.aabb.max.x; j++)
                {
                    bool x      = current.aabb.max.y < aabbs[j].aabb.min.y;
                    bool y      = current.aabb.min.y > aabbs[j].aabb.max.y;
                    bool z      = current.aabb.max.z < aabbs[j].aabb.min.z;
                    bool w      = current.aabb.min.z > aabbs[j].aabb.max.z;
                    bool result = x | y | z | w;
                    if (!result)
                    {
                        overlaps.Add(new EntityPair(current.entity, aabbs[j].entity));
                    }
                }
            }
        }
    }

    [BurstCompile]
    public struct LessNaiveSweep : IJob
    {
        [ReadOnly] public NativeArray<AabbEntity> aabbs;
        public NativeList<EntityPair>             overlaps;

        public void Execute()
        {
            for (int i = 0; i < aabbs.Length - 1; i++)
            {
                AabbEntity current = aabbs[i];

                for (int j = i + 1; j < aabbs.Length && aabbs[j].aabb.min.x <= current.aabb.max.x; j++)
                {
                    int invalidatedx = math.select(0, 1, current.aabb.max.y < aabbs[j].aabb.min.y);
                    int invalidatedy = math.select(0, 2, current.aabb.min.y > aabbs[j].aabb.max.y);
                    int invalidatedz = math.select(0, 4, current.aabb.max.z < aabbs[j].aabb.min.z);
                    int invalidatedw = math.select(0, 8, current.aabb.min.z > aabbs[j].aabb.max.z);
                    if (0 == (invalidatedx | invalidatedy | invalidatedz | invalidatedw))
                    {
                        overlaps.Add(new EntityPair(current.entity, aabbs[j].entity));
                    }
                }
            }
        }
    }

    [BurstCompile]
    public struct FunnySweep : IJob
    {
        [ReadOnly] public NativeArray<AabbEntity> aabbs;
        public NativeList<EntityPair>             overlaps;

        public void Execute()
        {
            for (int i = 0; i < aabbs.Length - 1; i++)
            {
                AabbEntity current = aabbs[i];

                for (int j = i + 1; j < aabbs.Length && aabbs[j].aabb.min.x <= current.aabb.max.x; j++)
                {
                    bool invalidatedx = current.aabb.max.y < aabbs[j].aabb.min.y;
                    bool invalidatedy = current.aabb.min.y > aabbs[j].aabb.max.y;
                    bool invalidatedz = current.aabb.max.z < aabbs[j].aabb.min.z;
                    bool invalidatedw = current.aabb.min.z > aabbs[j].aabb.max.z;
                    bool xy           = invalidatedx | invalidatedy;
                    bool zw           = invalidatedz | invalidatedw;
                    if (!(xy | zw))
                    {
                        overlaps.Add(new EntityPair(current.entity, aabbs[j].entity));
                    }
                }
            }
        }
    }

    [BurstCompile]
    public struct BetterSweep : IJob
    {
        [ReadOnly] public NativeArray<AabbEntity> aabbs;
        public NativeList<EntityPair>             overlaps;

        public void Execute()
        {
            for (int i = 0; i < aabbs.Length - 1; i++)
            {
                AabbEntity current = aabbs[i];

                for (int j = i + 1; j < aabbs.Length && aabbs[j].aabb.min.x <= current.aabb.max.x; j++)
                {
                    float4 less = new float4(current.aabb.max.y, aabbs[j].aabb.max.y, current.aabb.max.z, aabbs[j].aabb.max.z);
                    float4 more = new float4(aabbs[j].aabb.min.y, current.aabb.min.y, aabbs[j].aabb.min.z, current.aabb.min.z);

                    //bool4 tests = less < more;
                    //if (!math.any(tests))
                    if (!math.any(less < more))
                    {
                        overlaps.Add(new EntityPair(current.entity, aabbs[j].entity));
                    }
                }
            }
        }
    }

    [BurstCompile]
    public struct SimdSweep : IJob
    {
        [ReadOnly] public NativeArray<AabbEntity> aabbs;
        public NativeList<EntityPair>             overlaps;

        public void Execute()
        {
            for (int i = 0; i < aabbs.Length - 1; i++)
            {
                AabbEntity current = aabbs[i];

                for (int j = i + 1; j < aabbs.Length && aabbs[j].aabb.min.x <= current.aabb.max.x; j++)
                {
                    float4 less = new float4(current.aabb.max.y, aabbs[j].aabb.max.y, current.aabb.max.z, aabbs[j].aabb.max.z);
                    float4 more = new float4(aabbs[j].aabb.min.y, current.aabb.min.y, aabbs[j].aabb.min.z, current.aabb.min.z);

                    if (math.bitmask(less < more) == 0)
                    {
                        overlaps.Add(new EntityPair(current.entity, aabbs[j].entity));
                    }
                }
            }
        }
    }

    [BurstCompile]
    public struct RearrangedSweep : IJob
    {
        [ReadOnly] public NativeArray<AabbEntityRearranged> aabbs;
        public NativeList<EntityPair>                       overlaps;

        public void Execute()
        {
            for (int i = 0; i < aabbs.Length - 1; i++)
            {
                AabbEntityRearranged current = aabbs[i];

                for (int j = i + 1; j < aabbs.Length && aabbs[j].minXmaxX.x <= current.minXmaxX.y; j++)
                {
                    float4 less = math.shuffle(current.minYZmaxYZ,
                                               aabbs[j].minYZmaxYZ,
                                               math.ShuffleComponent.LeftZ,
                                               math.ShuffleComponent.RightZ,
                                               math.ShuffleComponent.LeftW,
                                               math.ShuffleComponent.RightW);
                    float4 more = math.shuffle(current.minYZmaxYZ,
                                               aabbs[j].minYZmaxYZ,
                                               math.ShuffleComponent.RightX,
                                               math.ShuffleComponent.LeftX,
                                               math.ShuffleComponent.RightY,
                                               math.ShuffleComponent.LeftY);

                    if (math.bitmask(less < more) == 0)
                    {
                        overlaps.Add(new EntityPair(current.entity, aabbs[j].entity));
                    }
                }
            }
        }
    }

    [BurstCompile]
    public struct SoaSweep : IJob
    {
        [ReadOnly] public NativeArray<float>  xmins;
        [ReadOnly] public NativeArray<float>  xmaxs;
        [ReadOnly] public NativeArray<float4> minYZmaxYZs;
        [ReadOnly] public NativeArray<Entity> entities;
        public NativeList<EntityPair>         overlaps;

        public void Execute()
        {
            for (int i = 0; i < xmins.Length - 1; i++)
            {
                float4 current = minYZmaxYZs[i];

                for (int j = i + 1; j < xmaxs.Length && xmins[j] <= xmaxs[i]; j++)
                {
                    float4 less = new float4(current.z, minYZmaxYZs[j].z, current.w, minYZmaxYZs[j].w);
                    float4 more = new float4(minYZmaxYZs[j].x, current.x, minYZmaxYZs[j].y, current.y);

                    if (math.bitmask(less < more) == 0)
                    {
                        overlaps.Add(new EntityPair(entities[i], entities[j]));
                    }
                }
            }
        }
    }

    [BurstCompile]
    public struct SoaShuffleSweep : IJob
    {
        [ReadOnly] public NativeArray<float>  xmins;
        [ReadOnly] public NativeArray<float>  xmaxs;
        [ReadOnly] public NativeArray<float4> minYZmaxYZs;
        [ReadOnly] public NativeArray<Entity> entities;
        public NativeList<EntityPair>         overlaps;

        public void Execute()
        {
            for (int i = 0; i < xmins.Length - 1; i++)
            {
                float4 current = minYZmaxYZs[i];

                for (int j = i + 1; j < xmaxs.Length && xmins[j] <= xmaxs[i]; j++)
                {
                    float4 less = math.shuffle(current,
                                               minYZmaxYZs[j],
                                               math.ShuffleComponent.LeftZ,
                                               math.ShuffleComponent.RightZ,
                                               math.ShuffleComponent.LeftW,
                                               math.ShuffleComponent.RightW);
                    float4 more = math.shuffle(current,
                                               minYZmaxYZs[j],
                                               math.ShuffleComponent.RightX,
                                               math.ShuffleComponent.LeftX,
                                               math.ShuffleComponent.RightY,
                                               math.ShuffleComponent.LeftY);

                    if (math.bitmask(less < more) == 0)
                    {
                        overlaps.Add(new EntityPair(entities[i], entities[j]));
                    }
                }
            }
        }
    }

    [BurstCompile(OptimizeFor = OptimizeFor.Performance)]
    public struct OptimalOrderSweep : IJob
    {
        [ReadOnly] public NativeArray<float>  xmins;
        [ReadOnly] public NativeArray<float>  xmaxs;
        [ReadOnly] public NativeArray<float4> minYZmaxYZs;
        [ReadOnly] public NativeArray<Entity> entities;
        public NativeList<EntityPair>         overlaps;

        public void Execute()
        {
            Hint.Assume(xmins.Length == xmaxs.Length);

            for (int i = 0; i < xmins.Length - 1; i++)
            {
                float4 current = minYZmaxYZs[i];

                for (int j = i + 1; j < xmaxs.Length && xmins[j] <= xmaxs[i]; j++)
                {
                    float4 less = math.shuffle(current,
                                               minYZmaxYZs[j],
                                               math.ShuffleComponent.RightZ,
                                               math.ShuffleComponent.RightW,
                                               math.ShuffleComponent.LeftZ,
                                               math.ShuffleComponent.LeftW
                                               );
                    float4 more = math.shuffle(current,
                                               minYZmaxYZs[j],
                                               math.ShuffleComponent.LeftX,
                                               math.ShuffleComponent.LeftY,
                                               math.ShuffleComponent.RightX,
                                               math.ShuffleComponent.RightY
                                               );

                    if (math.bitmask(less < more) == 0)
                    {
                        overlaps.Add(new EntityPair(entities[i], entities[j]));
                    }
                }
            }
        }
    }

    [BurstCompile(OptimizeFor = OptimizeFor.Performance)]
    public struct FlippedSweep : IJob
    {
        [ReadOnly] public NativeArray<float>  xmins;
        [ReadOnly] public NativeArray<float>  xmaxs;
        [ReadOnly] public NativeArray<float4> minYZmaxYZsFlipped;
        [ReadOnly] public NativeArray<Entity> entities;
        public NativeList<EntityPair>         overlaps;

        public void Execute()
        {
            //int hitCount            = 0;
            //int innerLoopEnterCount = 0;
            //int innerLoopTestCount  = 0;
            //int innerLoopRunMin     = int.MaxValue;
            //int innerLoopRunMax     = 0;
            //int innerLoopZHits      = 0;

            Hint.Assume(xmins.Length == xmaxs.Length);
            Hint.Assume(xmins.Length == minYZmaxYZsFlipped.Length);

            //int count = 0;
            for (int i = 0; i < xmins.Length - 1; i++)
            {
                float4 current = -minYZmaxYZsFlipped[i].zwxy;

                //int runCount = 0;
                for (int j = i + 1; Hint.Likely(j < xmaxs.Length && xmins[j] <= xmaxs[i]); j++)
                {
                    //runCount++;
                    if (Hint.Unlikely(math.bitmask(current < minYZmaxYZsFlipped[j]) == 0))
                    {
                        overlaps.Add(new EntityPair(entities[i], entities[j]));
                        //count++;
                        //hitCount++;
                    }
                    //if (current.y >= minYZmaxYZsFlipped[j].y && current.w >= minYZmaxYZsFlipped[j].w)
                    //    innerLoopZHits++;
                }

                //if (runCount > 0)
                //    innerLoopEnterCount++;
                //innerLoopTestCount += runCount;
                //innerLoopRunMax     = math.max(innerLoopRunMax, runCount);
                //innerLoopRunMin     = math.min(innerLoopRunMin, runCount);
            }
            //if (count > 100)
            //    overlaps.Add(new EntityPair(entities[0], entities[1]));

            //UnityEngine.Debug.Log(
            //    $"FindPairs Self Sweep stats for layer count {xmins.Length}\nHits: {hitCount}, inner loop enters: {innerLoopEnterCount}, inner loop tests: {innerLoopTestCount}, inner loop run (min, max): ({innerLoopRunMin}, {innerLoopRunMax}), inner loop z hits: {innerLoopZHits}");
        }
    }

    [BurstCompile(OptimizeFor = OptimizeFor.Performance)]
    public struct SentinelSweep : IJob
    {
        [ReadOnly] public NativeArray<float>  xmins;
        [ReadOnly] public NativeArray<float>  xmaxs;
        [ReadOnly] public NativeArray<float4> minYZmaxYZsFlipped;
        [ReadOnly] public NativeArray<Entity> entities;
        public NativeList<EntityPair>         overlaps;

        public void Execute()
        {
            Hint.Assume(xmins.Length == xmaxs.Length);
            Hint.Assume(xmins.Length == minYZmaxYZsFlipped.Length);

            //int count = 0;
            for (int i = 0; i < xmins.Length - 2; i++)
            {
                float4 current = -minYZmaxYZsFlipped[i];

                for (int j = i + 1; Hint.Likely(xmins[j] <= xmaxs[i]); j++)
                {
                    Hint.Assume(j < xmins.Length);

                    if (Hint.Unlikely(math.bitmask(current < minYZmaxYZsFlipped[j]) == 0))
                    {
                        overlaps.Add(new EntityPair(entities[i], entities[j]));
                        //count++;
                    }
                }
            }

            //if (count > 100)
            //    overlaps.Add(new EntityPair(entities[0], entities[1]));
        }
    }

    [BurstCompile]
    public struct MortonNaiveSweep : IJob
    {
        [ReadOnly] public NativeArray<AabbEntityMorton> aabbs;
        public NativeList<EntityPair>                   overlaps;

        public void Execute()
        {
            //int hitCount            = 0;
            //int innerLoopEnterCount = 0;
            //int innerLoopTestCount  = 0;
            //int innerLoopRunMin     = int.MaxValue;
            //int innerLoopRunMax     = 0;

            for (int i = 0; i < aabbs.Length - 1; i++)
            {
                var current = aabbs[i];
                //int runCount = 0;
                for (int j = i + 1; j < aabbs.Length && LessOrEqualMorton(aabbs[j].min, current.max); j++)
                {
                    //runCount++;
                    if (!(math.any(current.aabb.max < aabbs[j].aabb.min) ||
                          math.any(current.aabb.min > aabbs[j].aabb.max)))
                    {
                        overlaps.Add(new EntityPair(current.entity, aabbs[j].entity));
                        //hitCount++;
                    }
                }
                //if (runCount > 0)
                //    innerLoopEnterCount++;
                //innerLoopTestCount += runCount;
                //innerLoopRunMax     = math.max(innerLoopRunMax, runCount);
                //innerLoopRunMin     = math.min(innerLoopRunMin, runCount);
            }

            //UnityEngine.Debug.Log(
            //    $"Morton Self Sweep stats for layer count {aabbs.Length}\nHits: {hitCount}, inner loop enters: {innerLoopEnterCount}, inner loop tests: {innerLoopTestCount}, inner loop run (min, max): ({innerLoopRunMin}, {innerLoopRunMax})");
        }

        bool LessOrEqualMorton(uint3 lhs, uint3 rhs)
        {
            if (lhs.z == rhs.z)
            {
                if (lhs.y == rhs.y)
                {
                    return lhs.x <= rhs.x;
                }
                else
                {
                    return lhs.y <= rhs.y;
                }
            }
            else
            {
                return lhs.z <= rhs.z;
            }
        }
    }

    [BurstCompile]
    public struct BatchSweep : IJob
    {
        [ReadOnly] public NativeArray<float>  xmins;
        [ReadOnly] public NativeArray<float>  xmaxs;
        [ReadOnly] public NativeArray<float4> minYZmaxYZs;
        [ReadOnly] public NativeArray<float4> batchMinYZmaxYZs;
        [ReadOnly] public NativeArray<Entity> entities;
        public NativeList<EntityPair>         overlaps;

        public void Execute()
        {
            Hint.Assume(xmins.Length == xmaxs.Length);

            //int hitCount                = 0;
            //int innerLoopEnterCount     = 0;
            //int innerLoopTestCount      = 0;
            //int innerLoopSkipBatchCount = 0;
            //int innerLoopRunMin         = int.MaxValue;
            //int innerLoopRunMax         = 0;

            for (int i = 0; i < xmins.Length - 1; i++)
            {
                float4 current = minYZmaxYZs[i];

                //int runCount = 0;

                for (int j = i + 1; j < xmaxs.Length && xmins[j] <= xmaxs[i]; j++)
                {
                    float4 batchBox = batchMinYZmaxYZs[j >> 2];

                    float4 less = math.shuffle(current,
                                               batchBox,
                                               math.ShuffleComponent.RightZ,
                                               math.ShuffleComponent.RightW,
                                               math.ShuffleComponent.LeftZ,
                                               math.ShuffleComponent.LeftW
                                               );
                    float4 more = math.shuffle(current,
                                               batchBox,
                                               math.ShuffleComponent.LeftX,
                                               math.ShuffleComponent.LeftY,
                                               math.ShuffleComponent.RightX,
                                               math.ShuffleComponent.RightY
                                               );

                    if (math.bitmask(less < more) == 0)
                    {
                        var termination = math.min(xmaxs.Length, (j & ~0x3) + 4);
                        for (; j < termination && xmins[j] <= xmaxs[i]; j++)
                        {
                            //runCount++;

                            float4 less2 = math.shuffle(current,
                                                        minYZmaxYZs[j],
                                                        math.ShuffleComponent.RightZ,
                                                        math.ShuffleComponent.RightW,
                                                        math.ShuffleComponent.LeftZ,
                                                        math.ShuffleComponent.LeftW
                                                        );
                            float4 more2 = math.shuffle(current,
                                                        minYZmaxYZs[j],
                                                        math.ShuffleComponent.LeftX,
                                                        math.ShuffleComponent.LeftY,
                                                        math.ShuffleComponent.RightX,
                                                        math.ShuffleComponent.RightY
                                                        );

                            if (math.bitmask(less2 < more2) == 0)
                            {
                                overlaps.Add(new EntityPair(entities[i], entities[j]));
                                //hitCount++;
                            }
                        }
                        j--;
                    }
                    else
                    {
                        j |= 0x3;
                        //innerLoopSkipBatchCount++;
                    }
                }
                //if (runCount > 0)
                //    innerLoopEnterCount++;
                //innerLoopTestCount += runCount;
                //innerLoopRunMax     = math.max(innerLoopRunMax, runCount);
                //innerLoopRunMin     = math.min(innerLoopRunMin, runCount);
            }

            //UnityEngine.Debug.Log(
            //    $"Batch Self Sweep stats for layer count {xmins.Length}\nHits: {hitCount}, inner loop enters: {innerLoopEnterCount}, inner loop tests: {innerLoopTestCount}, inner loop run (min, max): ({innerLoopRunMin}, {innerLoopRunMax}), inner loop skips: {innerLoopSkipBatchCount}");
        }
    }

    [BurstCompile(OptimizeFor = OptimizeFor.Performance)]
    public struct DualSweep : IJob
    {
        //[ReadOnly] public NativeArray<float> zs;
        [ReadOnly] public NativeArray<uint> zToXMinsMaxes;
        [ReadOnly] public NativeArray<uint> xs;

        //[ReadOnly] public NativeArray<float>  xmins;
        //[ReadOnly] public NativeArray<float>  xmaxs;
        [ReadOnly] public NativeArray<float4> minYZmaxYZs;
        [ReadOnly] public NativeArray<Entity> entities;
        public NativeList<EntityPair>         overlaps;

        struct ZRange
        {
            public int index;
            public int min;
            public int max;
        }

        public void Execute()
        {
            var zRanges = new NativeList<ZRange>(minYZmaxYZs.Length, Allocator.Temp);
            zRanges.ResizeUninitialized(minYZmaxYZs.Length);

            var zBits = new NativeList<BitField64>(minYZmaxYZs.Length / 64 + 1, Allocator.Temp);
            zBits.Resize(minYZmaxYZs.Length / 64 + 1, NativeArrayOptions.ClearMemory);

            {
                int minBit = 0;
                int index  = 0;
                for (int i = 0; i < zToXMinsMaxes.Length; i++)
                {
                    if (zToXMinsMaxes[i] < minYZmaxYZs.Length)
                    {
                        ref var range    = ref zRanges.ElementAt((int)zToXMinsMaxes[i]);
                        range.index      = index;
                        range.min        = minBit;
                        ref var bitField = ref zBits.ElementAt(index >> 6);
                        bitField.SetBits(index & 0x3f, true);
                        index++;
                    }
                    else
                    {
                        ref var range    = ref zRanges.ElementAt((int)(zToXMinsMaxes[i] - (uint)minYZmaxYZs.Length));
                        range.max        = index;
                        ref var bitField = ref zBits.ElementAt(range.index >> 6);
                        bitField.SetBits(range.index & 0x3f, false);
                        if (range.index == minBit)
                        {
                            while (minBit <= index)
                            {
                                var scanBits = zBits.ElementAt(minBit >> 6);
                                var tzcnt    = scanBits.CountTrailingZeros();
                                if (tzcnt < 64)
                                {
                                    minBit = (minBit & ~0x3f) + tzcnt;
                                    break;
                                }
                                minBit = (minBit & ~0x3f) + 64;
                            }
                            minBit = math.min(minBit, index + 1);
                        }
                    }
                }

                //if (minBit >= minYZmaxYZsFlipped.Length)
                //    return;
            }

            var zToXs = new NativeArray<int>(minYZmaxYZs.Length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

            //int hitCount            = 0;
            //int innerLoopEnterCount = 0;
            //int innerLoopTestCount  = 0;
            //int innerLoopRunMin     = int.MaxValue;
            //int innerLoopRunMax     = 0;
            //int maxRunRangeIndex    = 0;
            //int touchedZeroBitfield = 0;

            for (int i = 0; i < xs.Length; i++)
            {
                if (xs[i] < minYZmaxYZs.Length)
                {
                    //int runCount = 0;

                    var range       = zRanges[(int)xs[i]];
                    int minBitfield = range.min >> 6;
                    int maxBitfield = range.max >> 6;
                    if (minBitfield == maxBitfield)
                    {
                        int minBit   = range.min & 0x3f;
                        int maxBit   = range.max & 0x3f;
                        var bitField = zBits[minBitfield];
                        if (minBit > 0)
                            bitField.SetBits(0, false, minBit);

                        //if (bitField.Value == 0)
                        //    touchedZeroBitfield++;

                        for (var j = bitField.CountTrailingZeros(); j <= maxBit; bitField.SetBits(j, false), j = bitField.CountTrailingZeros())
                        {
                            //runCount++;
                            var currentIndex = (int)xs[i];
                            var otherIndex   = zToXs[j + 64 * minBitfield];

                            float4 less = math.shuffle(minYZmaxYZs[currentIndex],
                                                       minYZmaxYZs[otherIndex],
                                                       math.ShuffleComponent.RightZ,
                                                       math.ShuffleComponent.RightW,
                                                       math.ShuffleComponent.LeftZ,
                                                       math.ShuffleComponent.LeftW
                                                       );
                            float4 more = math.shuffle(minYZmaxYZs[currentIndex],
                                                       minYZmaxYZs[otherIndex],
                                                       math.ShuffleComponent.LeftX,
                                                       math.ShuffleComponent.LeftY,
                                                       math.ShuffleComponent.RightX,
                                                       math.ShuffleComponent.RightY
                                                       );

                            if (math.bitmask(less < more) == 0)
                            {
                                overlaps.Add(new EntityPair(entities[currentIndex], entities[otherIndex]));
                                //hitCount++;
                            }
                        }
                    }
                    else
                    {
                        {
                            int minBit   = range.min & 0x3f;
                            var bitField = zBits[minBitfield];
                            if (minBit > 0)
                                bitField.SetBits(0, false, minBit);

                            //if (bitField.Value == 0)
                            //    touchedZeroBitfield++;

                            for (var j = bitField.CountTrailingZeros(); j < 64; bitField.SetBits(j, false), j = bitField.CountTrailingZeros())
                            {
                                //runCount++;
                                var currentIndex = (int)xs[i];
                                var otherIndex   = zToXs[j + 64 * minBitfield];

                                float4 less = math.shuffle(minYZmaxYZs[currentIndex],
                                                           minYZmaxYZs[otherIndex],
                                                           math.ShuffleComponent.RightZ,
                                                           math.ShuffleComponent.RightW,
                                                           math.ShuffleComponent.LeftZ,
                                                           math.ShuffleComponent.LeftW
                                                           );
                                float4 more = math.shuffle(minYZmaxYZs[currentIndex],
                                                           minYZmaxYZs[otherIndex],
                                                           math.ShuffleComponent.LeftX,
                                                           math.ShuffleComponent.LeftY,
                                                           math.ShuffleComponent.RightX,
                                                           math.ShuffleComponent.RightY
                                                           );

                                if (math.bitmask(less < more) == 0)
                                {
                                    overlaps.Add(new EntityPair(entities[currentIndex], entities[otherIndex]));
                                    //hitCount++;
                                }
                            }
                        }

                        for (int k = minBitfield + 1; k < maxBitfield; k++)
                        {
                            var bitField = zBits[k];

                            //if (bitField.Value == 0)
                            //    touchedZeroBitfield++;

                            for (var j = bitField.CountTrailingZeros(); j < 64; bitField.SetBits(j, false), j = bitField.CountTrailingZeros())
                            {
                                //runCount++;
                                var currentIndex = (int)xs[i];
                                var otherIndex   = zToXs[j + 64 * k];

                                float4 less = math.shuffle(minYZmaxYZs[currentIndex],
                                                           minYZmaxYZs[otherIndex],
                                                           math.ShuffleComponent.RightZ,
                                                           math.ShuffleComponent.RightW,
                                                           math.ShuffleComponent.LeftZ,
                                                           math.ShuffleComponent.LeftW
                                                           );
                                float4 more = math.shuffle(minYZmaxYZs[currentIndex],
                                                           minYZmaxYZs[otherIndex],
                                                           math.ShuffleComponent.LeftX,
                                                           math.ShuffleComponent.LeftY,
                                                           math.ShuffleComponent.RightX,
                                                           math.ShuffleComponent.RightY
                                                           );

                                if (math.bitmask(less < more) == 0)
                                {
                                    overlaps.Add(new EntityPair(entities[currentIndex], entities[otherIndex]));
                                    //hitCount++;
                                }
                            }
                        }

                        {
                            int maxBit   = range.max & 0x3f;
                            var bitField = zBits[maxBitfield];

                            //if (bitField.Value == 0)
                            //    touchedZeroBitfield++;

                            for (var j = bitField.CountTrailingZeros(); j <= maxBit; bitField.SetBits(j, false), j = bitField.CountTrailingZeros())
                            {
                                //runCount++;
                                var currentIndex = (int)xs[i];
                                var otherIndex   = zToXs[j + 64 * maxBitfield];

                                float4 less = math.shuffle(minYZmaxYZs[currentIndex],
                                                           minYZmaxYZs[otherIndex],
                                                           math.ShuffleComponent.RightZ,
                                                           math.ShuffleComponent.RightW,
                                                           math.ShuffleComponent.LeftZ,
                                                           math.ShuffleComponent.LeftW
                                                           );
                                float4 more = math.shuffle(minYZmaxYZs[currentIndex],
                                                           minYZmaxYZs[otherIndex],
                                                           math.ShuffleComponent.LeftX,
                                                           math.ShuffleComponent.LeftY,
                                                           math.ShuffleComponent.RightX,
                                                           math.ShuffleComponent.RightY
                                                           );

                                if (math.bitmask(less < more) == 0)
                                {
                                    overlaps.Add(new EntityPair(entities[currentIndex], entities[otherIndex]));
                                    //hitCount++;
                                }
                            }
                        }
                    }

                    ref var currentBitfield = ref zBits.ElementAt(range.index >> 6);
                    currentBitfield.SetBits(range.index & 0x3f, true);
                    zToXs[range.index] = (int)xs[i];

                    //if (runCount > 0)
                    //    innerLoopEnterCount++;
                    //innerLoopTestCount += runCount;
                    //if (runCount > innerLoopRunMax)
                    //    maxRunRangeIndex = (int)xs[i];
                    //innerLoopRunMax      = math.max(innerLoopRunMax, runCount);
                    //innerLoopRunMin      = math.min(innerLoopRunMin, runCount);
                }
                else
                {
                    var     range           = zRanges[(int)(xs[i] - minYZmaxYZs.Length)];
                    ref var currentBitfield = ref zBits.ElementAt(range.index >> 6);
                    currentBitfield.SetBits(range.index & 0x3f, false);
                }
            }
            //var maxRange = zRanges[maxRunRangeIndex];
            //UnityEngine.Debug.Log(
            //    $"FindPairs Self Sweep stats for layer count {minYZmaxYZsFlipped.Length}\nHits: {hitCount}, inner loop enters: {innerLoopEnterCount}, inner loop tests: {innerLoopTestCount}, inner loop run (min, max): ({innerLoopRunMin}, {innerLoopRunMax}), maxRange: ({maxRange.min}, {maxRange.index}, {maxRange.max}), touched zero bitfields: {touchedZeroBitfield}");
        }
    }

    [BurstCompile(OptimizeFor = OptimizeFor.Performance)]
    public struct DualSweepCondensed : IJob
    {
        [ReadOnly] public NativeArray<uint> zToXMinsMaxes;
        [ReadOnly] public NativeArray<uint> xs;

        [ReadOnly] public NativeArray<float4> minYZmaxYZs;
        [ReadOnly] public NativeArray<Entity> entities;
        public NativeList<EntityPair>         overlaps;

        struct ZRange
        {
            public int index;
            public int min;
            public int max;
        }

        public void Execute()
        {
            Hint.Assume(zToXMinsMaxes.Length == xs.Length);
            Hint.Assume(minYZmaxYZs.Length == entities.Length);
            Hint.Assume(minYZmaxYZs.Length * 2 == xs.Length);

            var zRanges = new NativeList<ZRange>(minYZmaxYZs.Length, Allocator.Temp);
            zRanges.ResizeUninitialized(minYZmaxYZs.Length);

            var zBits = new NativeList<BitField64>(minYZmaxYZs.Length / 64 + 1, Allocator.Temp);
            zBits.Resize(minYZmaxYZs.Length / 64 + 1, NativeArrayOptions.ClearMemory);

            {
                int minBit = 0;
                int index  = 0;
                for (int i = 0; i < zToXMinsMaxes.Length; i++)
                {
                    if (zToXMinsMaxes[i] < minYZmaxYZs.Length)
                    {
                        ref var range    = ref zRanges.ElementAt((int)zToXMinsMaxes[i]);
                        range.index      = index;
                        range.min        = minBit;
                        ref var bitField = ref zBits.ElementAt(index >> 6);
                        bitField.SetBits(index & 0x3f, true);
                        index++;
                    }
                    else
                    {
                        ref var range    = ref zRanges.ElementAt((int)(zToXMinsMaxes[i] - (uint)minYZmaxYZs.Length));
                        range.max        = index;
                        ref var bitField = ref zBits.ElementAt(range.index >> 6);
                        bitField.SetBits(range.index & 0x3f, false);
                        if (range.index == minBit)
                        {
                            while (minBit <= index)
                            {
                                var scanBits = zBits.ElementAt(minBit >> 6);
                                var tzcnt    = scanBits.CountTrailingZeros();
                                if (tzcnt < 64)
                                {
                                    minBit = (minBit & ~0x3f) + tzcnt;
                                    break;
                                }
                                minBit = (minBit & ~0x3f) + 64;
                            }
                            minBit = math.min(minBit, index + 1);
                        }
                    }
                }

                //if (minBit >= minYZmaxYZsFlipped.Length)
                //    return;
            }

            var zToXs = new NativeArray<int>(minYZmaxYZs.Length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

            //int hitCount = 0;
            //int innerLoopEnterCount = 0;
            //int innerLoopTestCount  = 0;
            //int innerLoopRunMin     = int.MaxValue;
            //int innerLoopRunMax     = 0;
            //int maxRunRangeIndex = 0;
            //int touchedZeroBitfield = 0;
            //int touchedBitfieldsCount = 0;
            //int activeBitsCount       = 0;

            for (int i = 0; i < xs.Length; i++)
            {
                if (xs[i] < minYZmaxYZs.Length)
                {
                    //int runCount = 0;

                    var range       = zRanges[(int)xs[i]];
                    int minBitfield = range.min >> 6;
                    int maxBitfield = range.max >> 6;

                    for (int k = minBitfield; k <= maxBitfield; k++)
                    {
                        var bitField = zBits[k];
                        if (k == minBitfield)
                        {
                            int minBit = range.min & 0x3f;
                            if (Hint.Likely(minBit > 0))
                                bitField.SetBits(0, false, minBit);
                        }
                        int maxBit = math.select(63, range.max & 0x3f, k == maxBitfield);

                        //touchedBitfieldsCount++;
                        //activeBitsCount += bitField.CountBits();

                        //if (bitField.Value == 0)
                        //    touchedZeroBitfield++;

                        for (var j = bitField.CountTrailingZeros(); j <= maxBit; )
                        {
                            //runCount++;
                            var currentIndex = (int)xs[i];
                            var otherIndex   = zToXs[j + 64 * k];

                            bitField.SetBits(j, false);
                            j = bitField.CountTrailingZeros();

                            float4 less = math.shuffle(minYZmaxYZs[currentIndex],
                                                       minYZmaxYZs[otherIndex],
                                                       math.ShuffleComponent.RightZ,
                                                       math.ShuffleComponent.RightW,
                                                       math.ShuffleComponent.LeftZ,
                                                       math.ShuffleComponent.LeftW
                                                       );
                            float4 more = math.shuffle(minYZmaxYZs[currentIndex],
                                                       minYZmaxYZs[otherIndex],
                                                       math.ShuffleComponent.LeftX,
                                                       math.ShuffleComponent.LeftY,
                                                       math.ShuffleComponent.RightX,
                                                       math.ShuffleComponent.RightY
                                                       );

                            if (Hint.Unlikely(math.bitmask(less < more) == 0))
                            {
                                overlaps.Add(new EntityPair(entities[currentIndex], entities[otherIndex]));
                                //hitCount++;
                            }
                        }
                    }

                    ref var currentBitfield = ref zBits.ElementAt(range.index >> 6);
                    currentBitfield.SetBits(range.index & 0x3f, true);
                    zToXs[range.index] = (int)xs[i];

                    //if (runCount > 0)
                    //    innerLoopEnterCount++;
                    //innerLoopTestCount += runCount;
                    //if (runCount > innerLoopRunMax)
                    //    maxRunRangeIndex = (int)xs[i];
                    //innerLoopRunMax         = math.max(innerLoopRunMax, runCount);
                    //innerLoopRunMin         = math.min(innerLoopRunMin, runCount);
                }
                else
                {
                    var     range           = zRanges[(int)(xs[i] - minYZmaxYZs.Length)];
                    ref var currentBitfield = ref zBits.ElementAt(range.index >> 6);
                    currentBitfield.SetBits(range.index & 0x3f, false);
                }
            }

            //if (hitCount > 100)
            //    overlaps.Add(new EntityPair(entities[0], entities[1]));
            //var maxRange = zRanges[maxRunRangeIndex];
            //UnityEngine.Debug.Log(
            //    $"FindPairs Self Sweep stats for layer count {minYZmaxYZsFlipped.Length}\nHits: {hitCount}, inner loop enters: {innerLoopEnterCount}, inner loop tests: {innerLoopTestCount}, inner loop run (min, max): ({innerLoopRunMin}, {innerLoopRunMax}), maxRange: ({maxRange.min}, {maxRange.index}, {maxRange.max}), touched zero bitfields: {touchedZeroBitfield}");
            //UnityEngine.Debug.Log($"Touched u64: {touchedBitfieldsCount}, set bits: {activeBitsCount}");
        }
    }

    [BurstCompile(OptimizeFor = OptimizeFor.Performance)]
    public struct DualSweepOptimized : IJob
    {
        // This array stores the sorted orders of z mins and z maxes.
        // The integer at each index represents either of the following:
        // For a z min, it is the index of the AABB sorted by x mins.
        // For a z max, it is the index + AABB_count of the AABB sorted by x mins.
        // Therefore, a z max can be differentiated from a z min by comparing it to
        // the AABB count.
        [ReadOnly] public NativeArray<uint> zToXMinsMaxes;
        // This array stores the sorted orders of x mins and x maxes,
        // using the same convention as the previous array.
        // Integers still correlate to indices of AABBs sorted by x mins.
        [ReadOnly] public NativeArray<uint> xs;

        // This is the array we are used to, sorted by x mins.
        [ReadOnly] public NativeArray<float4> minYZmaxYZs;
        // Same for this.
        [ReadOnly] public NativeArray<Entity> entities;
        // This is our result.
        public NativeList<EntityPair> overlaps;

        struct ZRange
        {
            // This is the index sorted by z mins, not x mins.
            public int index;
            // This is the extreme backwards value using z axis indexing
            public int min;
            // This is the extreme forwards value using z axis indexing
            public int max;
        }

        public void Execute()
        {
            Hint.Assume(zToXMinsMaxes.Length == xs.Length);
            Hint.Assume(minYZmaxYZs.Length == entities.Length);
            Hint.Assume(minYZmaxYZs.Length * 2 == xs.Length);

            var zRanges = new NativeList<ZRange>(minYZmaxYZs.Length, Allocator.Temp);
            zRanges.ResizeUninitialized(minYZmaxYZs.Length);

            var zBits = new NativeList<BitField64>(minYZmaxYZs.Length / 64 + 2, Allocator.Temp);
            zBits.Resize(minYZmaxYZs.Length / 64 + 2, NativeArrayOptions.ClearMemory);

            {
                int minBit           = 0;
                int zminRunningCount = 0;
                for (int i = 0; i < zToXMinsMaxes.Length; i++)
                {
                    if (zToXMinsMaxes[i] < minYZmaxYZs.Length)
                    {
                        ref var range    = ref zRanges.ElementAt((int)zToXMinsMaxes[i]);
                        range.index      = zminRunningCount;
                        range.min        = minBit;
                        ref var bitField = ref zBits.ElementAt(zminRunningCount >> 6);
                        bitField.SetBits(zminRunningCount & 0x3f, true);
                        zminRunningCount++;
                    }
                    else
                    {
                        ref var range    = ref zRanges.ElementAt((int)(zToXMinsMaxes[i] - (uint)minYZmaxYZs.Length));
                        range.max        = zminRunningCount;
                        ref var bitField = ref zBits.ElementAt(range.index >> 6);
                        bitField.SetBits(range.index & 0x3f, false);
                        if (range.index == minBit)
                        {
                            while (minBit <= zminRunningCount)
                            {
                                var scanBits = zBits.ElementAt(minBit >> 6);
                                var tzcnt    = scanBits.CountTrailingZeros();
                                if (tzcnt < 64)
                                {
                                    minBit = (minBit & ~0x3f) + tzcnt;
                                    break;
                                }
                                minBit = (minBit & ~0x3f) + 64;
                            }
                            minBit = math.min(minBit, zminRunningCount + 1);
                        }
                    }
                }

                //if (minBit >= minYZmaxYZsFlipped.Length)
                //    return;
            }

            var zToXs = new NativeArray<int>(minYZmaxYZs.Length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            zToXs[0]  = 0;

            //int hitCount = 0;
            //int innerLoopEnterCount = 0;
            //int innerLoopTestCount  = 0;
            //int innerLoopRunMin     = int.MaxValue;
            //int innerLoopRunMax     = 0;
            //int maxRunRangeIndex = 0;
            //int touchedZeroBitfield = 0;
            //int touchedBitfieldsCount = 0;
            //int activeBitsCount = 0;

            for (int i = 0; i < xs.Length; i++)
            {
                if (xs[i] < minYZmaxYZs.Length)
                {
                    //int runCount = 0;

                    var zBitArray = zBits.AsArray();

                    int currentIndex = (int)xs[i];
                    var currentYZ    = minYZmaxYZs[currentIndex];
                    var range        = zRanges[currentIndex];
                    int minBitfield  = range.min >> 6;

                    int bitfieldIndex  = minBitfield;
                    var bitfield       = zBits[bitfieldIndex];
                    int bitIndex       = range.min & 0x3f;
                    bitfield.Value    &= ~((1ul << bitIndex) - 1);
                    bool advance;

                    bitIndex = bitfield.CountTrailingZeros();
                    while ((bitfieldIndex << 6) + bitIndex <= range.max)
                    {
                        advance         = bitIndex > 63;
                        bitIndex       &= 0x3f;
                        bitfield.Value &= ~(1ul << bitIndex);

                        if (Hint.Unlikely(advance))
                        {
                            bitfieldIndex++;
                            bitfield = zBitArray[bitfieldIndex];
                            bitIndex = bitfield.CountTrailingZeros();
                            continue;
                        }

                        var otherIndex = zToXs[(bitfieldIndex << 6) + bitIndex];

                        // High latency here, so move it in front of simd
                        bitIndex = bitfield.CountTrailingZeros();

                        float4 less = math.shuffle(currentYZ,
                                                   minYZmaxYZs[otherIndex],
                                                   math.ShuffleComponent.RightZ,
                                                   math.ShuffleComponent.RightW,
                                                   math.ShuffleComponent.LeftZ,
                                                   math.ShuffleComponent.LeftW
                                                   );
                        float4 more = math.shuffle(currentYZ,
                                                   minYZmaxYZs[otherIndex],
                                                   math.ShuffleComponent.LeftX,
                                                   math.ShuffleComponent.LeftY,
                                                   math.ShuffleComponent.RightX,
                                                   math.ShuffleComponent.RightY
                                                   );

                        if (Hint.Unlikely(math.bitmask(less < more) == 0))
                        {
                            overlaps.Add(new EntityPair(entities[currentIndex], entities[otherIndex]));
                            //hitCount++;
                        }
                    }

                    ref var currentBitfield = ref zBits.ElementAt(range.index >> 6);
                    currentBitfield.SetBits(range.index & 0x3f, true);
                    zToXs[range.index] = (int)xs[i];

                    //if (runCount > 0)
                    //    innerLoopEnterCount++;
                    //innerLoopTestCount += runCount;
                    //if (runCount > innerLoopRunMax)
                    //    maxRunRangeIndex = (int)xs[i];
                    //innerLoopRunMax         = math.max(innerLoopRunMax, runCount);
                    //innerLoopRunMin         = math.min(innerLoopRunMin, runCount);
                }
                else
                {
                    var     range           = zRanges[(int)(xs[i] - minYZmaxYZs.Length)];
                    ref var currentBitfield = ref zBits.ElementAt(range.index >> 6);
                    currentBitfield.SetBits(range.index & 0x3f, false);
                }
            }

            //if (hitCount > 100)
            //    overlaps.Add(new EntityPair(entities[0], entities[1]));
            //var maxRange = zRanges[maxRunRangeIndex];
            //UnityEngine.Debug.Log(
            //    $"FindPairs Self Sweep stats for layer count {minYZmaxYZsFlipped.Length}\nHits: {hitCount}, inner loop enters: {innerLoopEnterCount}, inner loop tests: {innerLoopTestCount}, inner loop run (min, max): ({innerLoopRunMin}, {innerLoopRunMax}), maxRange: ({maxRange.min}, {maxRange.index}, {maxRange.max}), touched zero bitfields: {touchedZeroBitfield}");
            //UnityEngine.Debug.Log($"Touched u64: {touchedBitfieldsCount}, set bits: {activeBitsCount}");
        }
    }

    [BurstCompile(OptimizeFor = OptimizeFor.Performance)]
    public struct UnrolledSweep : IJob
    {
        [ReadOnly] public NativeArray<float>  xmins;
        [ReadOnly] public NativeArray<float>  xmaxs;
        [ReadOnly] public NativeArray<float4> minYZmaxYZsFlipped;
        [ReadOnly] public NativeArray<Entity> entities;
        public NativeList<EntityPair>         overlaps;

        public unsafe void Execute()
        {
            Hint.Assume(xmins.Length == xmaxs.Length);
            Hint.Assume(xmins.Length == minYZmaxYZsFlipped.Length);

            var   hitCache     = new NativeArray<ulong>(1024, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            ulong nextHitIndex = 0;
            var   hitCachePtr  = (ulong*)hitCache.GetUnsafePtr();
            var   minMaxPtr    = (float4*)minYZmaxYZsFlipped.GetUnsafeReadOnlyPtr();

            for (int i = 0; i < xmins.Length - 1; i++)
            {
                float4 current = -minYZmaxYZsFlipped[i].zwxy;

                float4 currentX = xmaxs[i];

                ulong j = (ulong)i + 1;

                ulong pair  = (((ulong)i) << 32) | j;
                ulong final = (((ulong)i) << 32) | ((uint)xmins.Length);

                while (pair + 3 < final)
                {
                    var nextMins = xmins.ReinterpretLoad<float4>((int)j);
                    if (Hint.Unlikely(math.any(nextMins >= currentX)))
                        break;

                    hitCachePtr[nextHitIndex] = pair;
                    if (math.bitmask(current < minMaxPtr[j]) == 0)
                        nextHitIndex++;
                    pair++;

                    hitCachePtr[nextHitIndex] = pair;
                    if (math.bitmask(current < minMaxPtr[j + 1]) == 0)
                        nextHitIndex++;
                    pair++;

                    hitCachePtr[nextHitIndex] = pair;
                    if (math.bitmask(current < minMaxPtr[j + 2]) == 0)
                        nextHitIndex++;
                    pair++;

                    hitCachePtr[nextHitIndex] = pair;
                    if (math.bitmask(current < minMaxPtr[j + 3]) == 0)
                        nextHitIndex++;
                    pair++;
                    j += 4;

                    if (Hint.Unlikely(nextHitIndex >= 1020))
                    {
                        Drain(hitCache, nextHitIndex);
                        nextHitIndex = 0;
                    }
                }

                while (pair < final && xmins[(int)j] < currentX.x)
                {
                    hitCachePtr[nextHitIndex] = pair;
                    if (math.bitmask(current < minMaxPtr[j]) == 0)
                        nextHitIndex++;
                    pair++;
                    j++;
                }

                if (nextHitIndex >= 1020)
                {
                    Drain(hitCache, nextHitIndex);
                    nextHitIndex = 0;
                }
            }

            Drain(hitCache, nextHitIndex);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        void Drain(NativeArray<ulong> cache, ulong cacheCount)
        {
            for (int i = 0; i < (int)cacheCount; i++)
            {
                ulong pair   = cache[i];
                int   first  = (int)(pair >> 32);
                int   second = (int)(pair & 0xffffffff);

                overlaps.Add(new EntityPair(entities[first], entities[second]));
            }
        }
    }

    [BurstCompile(OptimizeFor = OptimizeFor.Performance)]
    public struct UnrolledSweep2 : IJob
    {
        [ReadOnly] public NativeArray<float>  xmins;
        [ReadOnly] public NativeArray<float>  xmaxs;
        [ReadOnly] public NativeArray<float4> minYZmaxYZsFlipped;
        [ReadOnly] public NativeArray<Entity> entities;
        public NativeList<EntityPair>         overlaps;

        public unsafe void Execute()
        {
            Hint.Assume(xmins.Length == xmaxs.Length);
            Hint.Assume(xmins.Length == minYZmaxYZsFlipped.Length);

            var   hitCache     = new NativeArray<ulong>(1024, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            ulong nextHitIndex = 0;
            var   hitCachePtr  = (ulong*)hitCache.GetUnsafePtr();
            var   minMaxPtr    = (float4*)minYZmaxYZsFlipped.GetUnsafeReadOnlyPtr();

            for (int i = 0; i < xmins.Length - 1; i++)
            {
                float4 current = -minYZmaxYZsFlipped[i].zwxy;

                float currentX = xmaxs[i];

                ulong j = (ulong)i + 1;

                ulong pair  = (((ulong)i) << 32) | j;
                ulong final = (((ulong)i) << 32) | ((uint)xmins.Length);

                while (pair + 3 < final)
                {
                    if (Hint.Unlikely(xmins[(int)(j + 3)] >= currentX))
                        break;

                    hitCachePtr[nextHitIndex] = pair;
                    if (math.bitmask(current < minMaxPtr[j]) == 0)
                        nextHitIndex++;
                    pair++;

                    hitCachePtr[nextHitIndex] = pair;
                    if (math.bitmask(current < minMaxPtr[j + 1]) == 0)
                        nextHitIndex++;
                    pair++;

                    hitCachePtr[nextHitIndex] = pair;
                    if (math.bitmask(current < minMaxPtr[j + 2]) == 0)
                        nextHitIndex++;
                    pair++;

                    hitCachePtr[nextHitIndex] = pair;
                    if (math.bitmask(current < minMaxPtr[j + 3]) == 0)
                        nextHitIndex++;
                    pair++;
                    j += 4;

                    if (Hint.Unlikely(nextHitIndex >= 1020))
                    {
                        Drain(hitCache, nextHitIndex);
                        nextHitIndex = 0;
                    }
                }

                while (pair < final && xmins[(int)j] < currentX)
                {
                    hitCachePtr[nextHitIndex] = pair;
                    if (math.bitmask(current < minMaxPtr[j]) == 0)
                        nextHitIndex++;
                    pair++;
                    j++;
                }

                if (nextHitIndex >= 1020)
                {
                    Drain(hitCache, nextHitIndex);
                    nextHitIndex = 0;
                }
            }

            Drain(hitCache, nextHitIndex);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        void Drain(NativeArray<ulong> cache, ulong cacheCount)
        {
            for (int i = 0; i < (int)cacheCount; i++)
            {
                ulong pair   = cache[i];
                int   first  = (int)(pair >> 32);
                int   second = (int)(pair & 0xffffffff);

                overlaps.Add(new EntityPair(entities[first], entities[second]));
            }
        }
    }

    [BurstCompile(OptimizeFor = OptimizeFor.Performance)]
    public struct UnrolledSweep3 : IJob
    {
        [ReadOnly] public NativeArray<float>  xmins;
        [ReadOnly] public NativeArray<float>  xmaxs;
        [ReadOnly] public NativeArray<float4> minYZmaxYZsFlipped;
        [ReadOnly] public NativeArray<Entity> entities;
        public NativeList<EntityPair>         overlaps;

        public unsafe void Execute()
        {
            Hint.Assume(xmins.Length == xmaxs.Length);
            Hint.Assume(xmins.Length == minYZmaxYZsFlipped.Length);

            var   hitCache     = new NativeArray<ulong>(1024, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            ulong nextHitIndex = 0;
            var   hitCachePtr  = (ulong*)hitCache.GetUnsafePtr();
            var   minMaxPtr    = (float4*)minYZmaxYZsFlipped.GetUnsafeReadOnlyPtr();

            for (int i = 0; i < xmins.Length - 1; i++)
            {
                float4 current = -minYZmaxYZsFlipped[i].zwxy;

                float currentX = xmaxs[i];

                ulong j = (ulong)i + 1;

                ulong pair  = (((ulong)i) << 32) | j;
                ulong final = (((ulong)i) << 32) | ((uint)xmins.Length);

                while (pair + 7 < final)
                {
                    if (Hint.Unlikely(xmins[(int)(j + 7)] >= currentX))
                        break;

                    hitCachePtr[nextHitIndex] = pair;
                    if (math.bitmask(current < minMaxPtr[j]) == 0)
                        nextHitIndex++;
                    pair++;

                    hitCachePtr[nextHitIndex] = pair;
                    if (math.bitmask(current < minMaxPtr[j + 1]) == 0)
                        nextHitIndex++;
                    pair++;

                    hitCachePtr[nextHitIndex] = pair;
                    if (math.bitmask(current < minMaxPtr[j + 2]) == 0)
                        nextHitIndex++;
                    pair++;

                    hitCachePtr[nextHitIndex] = pair;
                    if (math.bitmask(current < minMaxPtr[j + 3]) == 0)
                        nextHitIndex++;
                    pair++;

                    hitCachePtr[nextHitIndex] = pair;
                    if (math.bitmask(current < minMaxPtr[j + 4]) == 0)
                        nextHitIndex++;
                    pair++;

                    hitCachePtr[nextHitIndex] = pair;
                    if (math.bitmask(current < minMaxPtr[j + 5]) == 0)
                        nextHitIndex++;
                    pair++;

                    hitCachePtr[nextHitIndex] = pair;
                    if (math.bitmask(current < minMaxPtr[j + 6]) == 0)
                        nextHitIndex++;
                    pair++;

                    hitCachePtr[nextHitIndex] = pair;
                    if (math.bitmask(current < minMaxPtr[j + 7]) == 0)
                        nextHitIndex++;
                    pair++;
                    j += 8;

                    if (Hint.Unlikely(nextHitIndex >= 1016))
                    {
                        Drain(hitCache, nextHitIndex);
                        nextHitIndex = 0;
                    }
                }

                while (pair < final && xmins[(int)j] < currentX)
                {
                    hitCachePtr[nextHitIndex] = pair;
                    if (math.bitmask(current < minMaxPtr[j]) == 0)
                        nextHitIndex++;
                    pair++;
                    j++;
                }

                if (nextHitIndex >= 1016)
                {
                    Drain(hitCache, nextHitIndex);
                    nextHitIndex = 0;
                }
            }

            Drain(hitCache, nextHitIndex);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        void Drain(NativeArray<ulong> cache, ulong cacheCount)
        {
            for (int i = 0; i < (int)cacheCount; i++)
            {
                ulong pair   = cache[i];
                int   first  = (int)(pair >> 32);
                int   second = (int)(pair & 0xffffffff);

                overlaps.Add(new EntityPair(entities[first], entities[second]));
            }
        }
    }

    [BurstCompile(OptimizeFor = OptimizeFor.Performance)]
    public struct UnrolledSweep4 : IJob
    {
        [ReadOnly] public NativeArray<float>  xmins;
        [ReadOnly] public NativeArray<float>  xmaxs;
        [ReadOnly] public NativeArray<float4> minYZmaxYZsFlipped;
        [ReadOnly] public NativeArray<Entity> entities;
        public NativeList<EntityPair>         overlaps;

        public unsafe void Execute()
        {
            Hint.Assume(xmins.Length == xmaxs.Length);
            Hint.Assume(xmins.Length == minYZmaxYZsFlipped.Length);

            var   hitCache     = new NativeArray<ulong>(1024, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            ulong nextHitIndex = 0;
            var   hitCachePtr  = (ulong*)hitCache.GetUnsafePtr();
            var   minMaxPtr    = (float4*)minYZmaxYZsFlipped.GetUnsafeReadOnlyPtr();

            for (int i = 0; i < xmins.Length - 1; i++)
            {
                float4 current = -minYZmaxYZsFlipped[i].zwxy;

                float currentX = xmaxs[i];

                ulong j = (ulong)i + 1;

                ulong pair  = (((ulong)i) << 32) | j;
                ulong final = (((ulong)i) << 32) | ((uint)xmins.Length);

                while (pair + 15 < final)
                {
                    if (Hint.Unlikely(xmins[(int)(j + 15)] >= currentX))
                        break;

                    hitCachePtr[nextHitIndex] = pair;
                    if (math.bitmask(current < minMaxPtr[j]) == 0)
                        nextHitIndex++;
                    pair++;

                    hitCachePtr[nextHitIndex] = pair;
                    if (math.bitmask(current < minMaxPtr[j + 1]) == 0)
                        nextHitIndex++;
                    pair++;

                    hitCachePtr[nextHitIndex] = pair;
                    if (math.bitmask(current < minMaxPtr[j + 2]) == 0)
                        nextHitIndex++;
                    pair++;

                    hitCachePtr[nextHitIndex] = pair;
                    if (math.bitmask(current < minMaxPtr[j + 3]) == 0)
                        nextHitIndex++;
                    pair++;

                    hitCachePtr[nextHitIndex] = pair;
                    if (math.bitmask(current < minMaxPtr[j + 4]) == 0)
                        nextHitIndex++;
                    pair++;

                    hitCachePtr[nextHitIndex] = pair;
                    if (math.bitmask(current < minMaxPtr[j + 5]) == 0)
                        nextHitIndex++;
                    pair++;

                    hitCachePtr[nextHitIndex] = pair;
                    if (math.bitmask(current < minMaxPtr[j + 6]) == 0)
                        nextHitIndex++;
                    pair++;

                    hitCachePtr[nextHitIndex] = pair;
                    if (math.bitmask(current < minMaxPtr[j + 7]) == 0)
                        nextHitIndex++;
                    pair++;

                    hitCachePtr[nextHitIndex] = pair;
                    if (math.bitmask(current < minMaxPtr[j + 8]) == 0)
                        nextHitIndex++;
                    pair++;

                    hitCachePtr[nextHitIndex] = pair;
                    if (math.bitmask(current < minMaxPtr[j + 9]) == 0)
                        nextHitIndex++;
                    pair++;

                    hitCachePtr[nextHitIndex] = pair;
                    if (math.bitmask(current < minMaxPtr[j + 10]) == 0)
                        nextHitIndex++;
                    pair++;

                    hitCachePtr[nextHitIndex] = pair;
                    if (math.bitmask(current < minMaxPtr[j + 11]) == 0)
                        nextHitIndex++;
                    pair++;

                    hitCachePtr[nextHitIndex] = pair;
                    if (math.bitmask(current < minMaxPtr[j + 12]) == 0)
                        nextHitIndex++;
                    pair++;

                    hitCachePtr[nextHitIndex] = pair;
                    if (math.bitmask(current < minMaxPtr[j + 13]) == 0)
                        nextHitIndex++;
                    pair++;

                    hitCachePtr[nextHitIndex] = pair;
                    if (math.bitmask(current < minMaxPtr[j + 14]) == 0)
                        nextHitIndex++;
                    pair++;

                    hitCachePtr[nextHitIndex] = pair;
                    if (math.bitmask(current < minMaxPtr[j + 15]) == 0)
                        nextHitIndex++;
                    pair++;
                    j += 16;

                    if (Hint.Unlikely(nextHitIndex >= 1008))
                    {
                        Drain(hitCache, nextHitIndex);
                        nextHitIndex = 0;
                    }
                }

                while (pair < final && xmins[(int)j] < currentX)
                {
                    hitCachePtr[nextHitIndex] = pair;
                    if (math.bitmask(current < minMaxPtr[j]) == 0)
                        nextHitIndex++;
                    pair++;
                    j++;
                }

                if (nextHitIndex >= 1008)
                {
                    Drain(hitCache, nextHitIndex);
                    nextHitIndex = 0;
                }
            }

            Drain(hitCache, nextHitIndex);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        void Drain(NativeArray<ulong> cache, ulong cacheCount)
        {
            for (int i = 0; i < (int)cacheCount; i++)
            {
                ulong pair   = cache[i];
                int   first  = (int)(pair >> 32);
                int   second = (int)(pair & 0xffffffff);

                overlaps.Add(new EntityPair(entities[first], entities[second]));
            }
        }
    }

    [BurstCompile(OptimizeFor = OptimizeFor.Performance)]
    public struct UnrolledSweepPoor : IJob
    {
        [ReadOnly] public NativeArray<float>  xmins;
        [ReadOnly] public NativeArray<float>  xmaxs;
        [ReadOnly] public NativeArray<float4> minYZmaxYZsFlipped;
        [ReadOnly] public NativeArray<Entity> entities;
        public NativeList<EntityPair>         overlaps;

        public void Execute()
        {
            Hint.Assume(xmins.Length == xmaxs.Length);
            Hint.Assume(xmins.Length == minYZmaxYZsFlipped.Length);

            var hitCache     = new NativeArray<ulong>(1024, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            int nextHitIndex = 0;

            for (int i = 0; i < xmins.Length - 1; i++)
            {
                float4 current = -minYZmaxYZsFlipped[i].zwxy;

                float4 currentX = xmaxs[i];

                int j = i + 1;

                ulong pair  = (((ulong)i) << 32) | (uint)j;
                ulong final = (((ulong)i) << 32) | ((uint)xmins.Length);

                while (pair + 3 < final)
                {
                    Hint.Assume(j + 3 < xmins.Length && j > 0);
                    Hint.Assume(nextHitIndex >= 0 && nextHitIndex <= 1024);

                    var nextMins = xmins.ReinterpretLoad<float4>((int)j);
                    if (Hint.Unlikely(math.any(nextMins >= currentX)))
                        break;

                    hitCache[nextHitIndex] = pair;
                    if (math.bitmask(current < minYZmaxYZsFlipped[j]) == 0)
                        nextHitIndex++;
                    pair++;

                    hitCache[nextHitIndex] = pair;
                    if (math.bitmask(current < minYZmaxYZsFlipped[j + 1]) == 0)
                        nextHitIndex++;
                    pair++;

                    hitCache[nextHitIndex] = pair;
                    if (math.bitmask(current < minYZmaxYZsFlipped[j + 2]) == 0)
                        nextHitIndex++;
                    pair++;

                    hitCache[nextHitIndex] = pair;
                    if (math.bitmask(current < minYZmaxYZsFlipped[j + 3]) == 0)
                        nextHitIndex++;
                    pair++;
                    j += 4;

                    if (Hint.Unlikely(nextHitIndex >= 1020))
                    {
                        Drain(hitCache, nextHitIndex);
                        nextHitIndex = 0;
                    }
                }

                while (pair < final && xmins[(int)j] < currentX.x)
                {
                    hitCache[nextHitIndex] = pair;
                    if (math.bitmask(current < minYZmaxYZsFlipped[j]) == 0)
                        nextHitIndex++;
                    pair++;
                    j++;
                }

                if (nextHitIndex >= 1020)
                {
                    Drain(hitCache, nextHitIndex);
                    nextHitIndex = 0;
                }
            }

            Drain(hitCache, nextHitIndex);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        void Drain(NativeArray<ulong> cache, int cacheCount)
        {
            for (int i = 0; i < cacheCount; i++)
            {
                ulong pair   = cache[i];
                int   first  = (int)(pair >> 32);
                int   second = (int)(pair & 0xffffffff);

                overlaps.Add(new EntityPair(entities[first], entities[second]));
            }
        }
    }

    [BurstCompile(OptimizeFor = OptimizeFor.Performance)]
    public struct DualSweepBranchless : IJob
    {
        // This array stores the sorted orders of z mins and z maxes.
        // The integer at each index represents either of the following:
        // For a z min, it is the index of the AABB sorted by x mins.
        // For a z max, it is the index + AABB_count of the AABB sorted by x mins.
        // Therefore, a z max can be differentiated from a z min by comparing it to
        // the AABB count.
        [ReadOnly] public NativeArray<uint> zToXMinsMaxes;
        // This array stores the sorted orders of x mins and x maxes,
        // using the same convention as the previous array.
        // Integers still correlate to indices of AABBs sorted by x mins.
        [ReadOnly] public NativeArray<uint> xs;

        // This is the array we are used to, sorted by x mins.
        [ReadOnly] public NativeArray<float4> minYZmaxYZsFlipped;
        // Same for this.
        [ReadOnly] public NativeArray<Entity> entities;
        // This is our result.
        public NativeList<EntityPair> overlaps;

        struct ZRange
        {
            // This is the index sorted by z mins, not x mins.
            public ushort index;
            // This is the extreme backwards value using z axis indexing
            public ushort min;
            // This is the extreme forwards value using z axis indexing
            public ushort max;
        }

        struct BitCacheCounters
        {
            public uint zIndexOffset;
            public uint nextBitfieldThreshold;
        }

        public unsafe void Execute()
        {
            if (minYZmaxYZsFlipped.Length > short.MaxValue)
                return;

            Hint.Assume(zToXMinsMaxes.Length == xs.Length);
            Hint.Assume(minYZmaxYZsFlipped.Length == entities.Length);
            Hint.Assume(minYZmaxYZsFlipped.Length * 2 == xs.Length);

            var zRanges = new NativeList<ZRange>(minYZmaxYZsFlipped.Length, Allocator.Temp);
            zRanges.ResizeUninitialized(minYZmaxYZsFlipped.Length);

            var zBits = new NativeList<BitField64>(minYZmaxYZsFlipped.Length / 64 + 2, Allocator.Temp);
            zBits.Resize(minYZmaxYZsFlipped.Length / 64 + 2, NativeArrayOptions.ClearMemory);

            {
                ushort minBit           = 0;
                ushort zminRunningCount = 0;
                for (int i = 0; i < zToXMinsMaxes.Length; i++)
                {
                    if (zToXMinsMaxes[i] < minYZmaxYZsFlipped.Length)
                    {
                        ref var range    = ref zRanges.ElementAt((int)zToXMinsMaxes[i]);
                        range.index      = zminRunningCount;
                        range.min        = minBit;
                        ref var bitField = ref zBits.ElementAt(zminRunningCount >> 6);
                        bitField.SetBits(zminRunningCount & 0x3f, true);
                        zminRunningCount++;
                    }
                    else
                    {
                        ref var range    = ref zRanges.ElementAt((int)(zToXMinsMaxes[i] - (uint)minYZmaxYZsFlipped.Length));
                        range.max        = zminRunningCount;
                        ref var bitField = ref zBits.ElementAt(range.index >> 6);
                        bitField.SetBits(range.index & 0x3f, false);
                        if (range.index == minBit)
                        {
                            while (minBit <= zminRunningCount)
                            {
                                var scanBits = zBits.ElementAt(minBit >> 6);
                                var tzcnt    = (ushort)scanBits.CountTrailingZeros();
                                if (tzcnt < 64)
                                {
                                    minBit = (ushort)((minBit & ~0x3f) + tzcnt);
                                    break;
                                }
                                minBit = (ushort)((minBit & ~0x3f) + 64);
                            }
                            minBit = (ushort)math.min(minBit, zminRunningCount + 1);
                        }
                    }
                }

                //if (minBit >= minYZmaxYZsFlipped.Length)
                //    return;
            }

            var   zToXs                 = new NativeArray<uint>(minYZmaxYZsFlipped.Length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            var   bitCacheBitfieldArray = new NativeArray<BitField64>(zBits.Length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            var   bitCacheCounterArray  = new NativeArray<BitCacheCounters>(zBits.Length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            var   hitCache              = new NativeArray<ulong>(1024, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            ulong nextHitIndex          = 0;

            for (int i = 0; i < xs.Length; i++)
            {
                if (xs[i] < minYZmaxYZsFlipped.Length)
                {
                    int currentIndex = (int)xs[i];
                    var currentYZ    = -minYZmaxYZsFlipped[currentIndex].zwxy;
                    var range        = zRanges[currentIndex];

                    ulong pairBase = ((ulong)currentIndex) << 32;

                    nextHitIndex = ProcessRange(nextHitIndex, currentYZ, range.min, range.max, pairBase,
                                                (BitCacheCounters*)bitCacheCounterArray.GetUnsafePtr(),
                                                (BitField64*)bitCacheBitfieldArray.GetUnsafePtr(),
                                                (BitField64*)zBits.GetUnsafeReadOnlyPtr(),
                                                (uint*)zToXs.GetUnsafeReadOnlyPtr(),
                                                (float4*)minYZmaxYZsFlipped.GetUnsafeReadOnlyPtr(),
                                                (ulong*)hitCache.GetUnsafePtr());

                    ref var currentBitfield = ref zBits.ElementAt(range.index >> 6);
                    currentBitfield.SetBits(range.index & 0x3f, true);
                    zToXs[range.index] = xs[i] << 4;
                }
                else
                {
                    var     range           = zRanges[(int)(xs[i] - minYZmaxYZsFlipped.Length)];
                    ref var currentBitfield = ref zBits.ElementAt(range.index >> 6);
                    currentBitfield.SetBits(range.index & 0x3f, false);
                }
            }

            Drain((ulong*)hitCache.GetUnsafePtr(), nextHitIndex);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        unsafe void Drain(ulong* cache, ulong cacheCount)
        {
            for (ulong i = 0; i < cacheCount; i++)
            {
                ulong pair   = cache[i];
                int   first  = (int)(pair >> 32);
                int   second = (int)((pair & 0xffffffff) >> 4);

                overlaps.Add(new EntityPair(entities[first], entities[second]));
            }
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        unsafe ulong ProcessRange(ulong nextHitIndex,
                                  float4 currentYZ,
                                  uint rangeMin,
                                  uint rangeMax,
                                  ulong pairBase,
                                  BitCacheCounters* bitCacheCountersArrayPtr,
                                  BitField64*       bitCacheBitfieldArrayPtr,
                                  BitField64*       zBitsArrayPtr,
                                  uint*             zToXsArrayPtr,
                                  float4*           minYZmaxYZsArrayPtr,
                                  ulong*            hitCacheArrayPtr)
        {
            ulong bitCacheIndex = 0;
            uint  bitCount      = 0;

            uint minBitfield = rangeMin >> 6;
            uint maxBitfield = rangeMax >> 6;

            {
                var maskedBitfield                                             = zBitsArrayPtr[minBitfield];
                var firstBitIndex                                              = rangeMin & 0x3f;
                maskedBitfield.Value                                          &= ~((1ul << (int)firstBitIndex) - 1);
                uint newBits                                                   = (uint)maskedBitfield.CountBits();
                bitCount                                                      += newBits;
                bitCacheBitfieldArrayPtr[bitCacheIndex]                        = maskedBitfield;
                bitCacheCountersArrayPtr[bitCacheIndex].zIndexOffset           = minBitfield << 6;
                bitCacheCountersArrayPtr[bitCacheIndex].nextBitfieldThreshold  = bitCount;

                if (newBits != 0)
                    bitCacheIndex++;
            }

            for (uint bitfieldSrcIndex = minBitfield + 1; bitfieldSrcIndex < maxBitfield; bitfieldSrcIndex++)
            {
                uint newBits                                                   = (uint)zBitsArrayPtr[bitfieldSrcIndex].CountBits();
                bitCount                                                      += newBits;
                bitCacheBitfieldArrayPtr[bitCacheIndex]                        = zBitsArrayPtr[bitfieldSrcIndex];
                bitCacheCountersArrayPtr[bitCacheIndex].zIndexOffset           = bitfieldSrcIndex << 6;
                bitCacheCountersArrayPtr[bitCacheIndex].nextBitfieldThreshold  = bitCount;

                if (newBits != 0)
                    bitCacheIndex++;
            }

            {
                var maskedBitfield = zBitsArrayPtr[maxBitfield];

                if (minBitfield == maxBitfield)
                {
                    bitCacheIndex  = 0;
                    maskedBitfield = bitCacheBitfieldArrayPtr[0];
                    bitCount       = 0;
                }

                var lastBitIndex                                               = rangeMax & 0x3f;
                maskedBitfield.Value                                          &= (((1ul << (int)lastBitIndex) - 1) | (1ul << (int)lastBitIndex));
                uint newBits                                                   = (uint)maskedBitfield.CountBits();
                bitCount                                                      += newBits;
                bitCacheBitfieldArrayPtr[bitCacheIndex]                        = maskedBitfield;
                bitCacheCountersArrayPtr[bitCacheIndex].zIndexOffset           = maxBitfield << 6;
                bitCacheCountersArrayPtr[bitCacheIndex].nextBitfieldThreshold  = bitCount;

                if (newBits != 0)
                    bitCacheIndex++;
            }

            if (bitCount == 0f)
                return nextHitIndex;

            var bitCacheFinalIndex = bitCacheIndex;
            bitCacheIndex          = 0;
            uint setBit            = 1;  // Instead of == compare, we use > compare for better codegen

            uint tzcnt = (uint)bitCacheBitfieldArrayPtr[bitCacheIndex].CountTrailingZeros();

            for (; setBit <= bitCount;)
            {
                setBit++;

                //bitCacheArrayPtr[bitCacheIndex].bitfield.SetBits((int)tzcnt, false);
                bitCacheBitfieldArrayPtr[bitCacheIndex].Value &= ~(1ul << (int)tzcnt);
                uint otherZ                                    = tzcnt + bitCacheCountersArrayPtr[bitCacheIndex].zIndexOffset;
                if (bitCacheCountersArrayPtr[bitCacheIndex].nextBitfieldThreshold < setBit)
                    bitCacheIndex++;
                tzcnt = (uint)bitCacheBitfieldArrayPtr[bitCacheIndex].CountTrailingZeros();

                uint otherIndex = zToXsArrayPtr[otherZ];
                Hint.Assume(otherIndex % 16 == 0);
                hitCacheArrayPtr[nextHitIndex] = pairBase + otherIndex;

                if (math.bitmask(currentYZ < minYZmaxYZsArrayPtr[otherIndex / 16]) == 0)
                    nextHitIndex++;

                if (Hint.Unlikely(nextHitIndex >= 1024))
                {
                    Drain(hitCacheArrayPtr, 1024);
                    nextHitIndex = 0;
                }
            }

            return nextHitIndex;
        }
    }

    [BurstCompile(OptimizeFor = OptimizeFor.Performance)]
    public struct DualSweepLinked : IJob
    {
        // This array stores the sorted orders of z mins and z maxes.
        // The integer at each index represents either of the following:
        // For a z min, it is the index of the AABB sorted by x mins.
        // For a z max, it is the index + AABB_count of the AABB sorted by x mins.
        // Therefore, a z max can be differentiated from a z min by comparing it to
        // the AABB count.
        [ReadOnly] public NativeArray<uint> zToXMinsMaxes;
        // This array stores the sorted orders of x mins and x maxes,
        // using the same convention as the previous array.
        // Integers still correlate to indices of AABBs sorted by x mins.
        [ReadOnly] public NativeArray<uint> xs;

        // This is the array we are used to, sorted by x mins.
        [ReadOnly] public NativeArray<float4> minYZmaxYZsFlipped;
        // Same for this.
        [ReadOnly] public NativeArray<Entity> entities;
        // This is our result.
        public NativeList<EntityPair> overlaps;

        struct ZRange
        {
            // This is the index sorted by z mins, not x mins.
            public int index;
            // This is the extreme backwards value using z axis indexing
            public int min;
            // This is the extreme forwards value using z axis indexing
            public int max;
        }

        public void Execute()
        {
            Hint.Assume(zToXMinsMaxes.Length == xs.Length);
            Hint.Assume(minYZmaxYZsFlipped.Length == entities.Length);
            Hint.Assume(minYZmaxYZsFlipped.Length * 2 == xs.Length);

            var zRanges = new NativeList<ZRange>(minYZmaxYZsFlipped.Length, Allocator.Temp);
            zRanges.ResizeUninitialized(minYZmaxYZsFlipped.Length);

            var zBits = new NativeList<BitField64>(minYZmaxYZsFlipped.Length / 64 + 2, Allocator.Temp);
            zBits.Resize(minYZmaxYZsFlipped.Length / 64 + 2, NativeArrayOptions.ClearMemory);

            {
                int minBit           = 0;
                int zminRunningCount = 0;
                for (int i = 0; i < zToXMinsMaxes.Length; i++)
                {
                    if (zToXMinsMaxes[i] < minYZmaxYZsFlipped.Length)
                    {
                        ref var range    = ref zRanges.ElementAt((int)zToXMinsMaxes[i]);
                        range.index      = zminRunningCount;
                        range.min        = minBit;
                        ref var bitField = ref zBits.ElementAt(zminRunningCount >> 6);
                        bitField.SetBits(zminRunningCount & 0x3f, true);
                        zminRunningCount++;
                    }
                    else
                    {
                        ref var range    = ref zRanges.ElementAt((int)(zToXMinsMaxes[i] - (uint)minYZmaxYZsFlipped.Length));
                        range.max        = zminRunningCount;
                        ref var bitField = ref zBits.ElementAt(range.index >> 6);
                        bitField.SetBits(range.index & 0x3f, false);
                        if (range.index == minBit)
                        {
                            while (minBit <= zminRunningCount)
                            {
                                var scanBits = zBits.ElementAt(minBit >> 6);
                                var tzcnt    = scanBits.CountTrailingZeros();
                                if (tzcnt < 64)
                                {
                                    minBit = (minBit & ~0x3f) + tzcnt;
                                    break;
                                }
                                minBit = (minBit & ~0x3f) + 64;
                            }
                            minBit = math.min(minBit, zminRunningCount + 1);
                        }
                    }
                }

                //if (minBit >= minYZmaxYZsFlipped.Length)
                //    return;
            }

            var intervalSet = new IntervalSet(zBits.AsArray(), minYZmaxYZsFlipped.Length);

            for (int i = 0; i < xs.Length; i++)
            {
                if (xs[i] < minYZmaxYZsFlipped.Length)
                {
                    int currentIndex = (int)xs[i];
                    var currentYZ    = -minYZmaxYZsFlipped[currentIndex].zwxy;
                    var range        = zRanges[currentIndex];
                    var enumerator   = new IntervalSet.Enumerator(in intervalSet, range.min, range.max - 1);

                    while (enumerator.IsValid())
                    {
                        var otherIndex = enumerator.Current;
                        Hint.Assume(otherIndex >= 0);
                        if (Hint.Unlikely(math.bitmask(currentYZ < minYZmaxYZsFlipped[otherIndex]) == 0))
                        {
                            overlaps.Add(new EntityPair(entities[currentIndex], entities[otherIndex]));
                        }
                        enumerator.Advance();
                    }

                    intervalSet.Add(range.index, currentIndex);
                }
                else
                {
                    var range = zRanges[(int)(xs[i] - minYZmaxYZsFlipped.Length)];
                    intervalSet.Remove(range.index);
                }
            }
        }

        struct IntervalSet
        {
            NativeArray<BitField64> zBits;
            NativeArray<BitField64> zBitBatches;
            NativeArray<Link>       links;

            int zHeadIndex;

            private struct Link
            {
                public int xIndex;
                public int nextZ;
            }

            public IntervalSet(NativeArray<BitField64> zBits, int count)
            {
                this.zBits  = zBits;
                zBitBatches = new NativeArray<BitField64>(zBits.Length / 64 + 1, Allocator.Temp, NativeArrayOptions.ClearMemory);
                links       = new NativeArray<Link>(count, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                zHeadIndex  = count;
            }

            public void Add(int zIndex, int xIndex)
            {
                Hint.Assume(zIndex >= 0 && xIndex >= 0);

                var batchIndex      = zIndex >> 6;
                var batchFieldIndex = batchIndex >> 6;

                if (zHeadIndex == links.Length)
                {
                    links[zIndex] = new Link { nextZ = links.Length, xIndex = xIndex };
                    zHeadIndex                                              = zIndex;
                    var bits                                                = zBits[batchIndex];
                    bits.SetBits(zIndex & 0x3f, true);
                    zBits[batchIndex] = bits;
                    var batchBits     = zBitBatches[batchFieldIndex];
                    batchBits.SetBits(batchIndex & 0x3f, true);
                    zBitBatches[batchFieldIndex] = batchBits;
                    return;
                }

                var previousLinkIndex = FindFirstBefore(zIndex, links.Length);
                if (previousLinkIndex < zHeadIndex || previousLinkIndex == links.Length)
                {
                    // This is first in the chain
                    links[zIndex] = new Link { nextZ = zHeadIndex, xIndex = xIndex };
                    zHeadIndex                                            = zIndex;
                    var bits                                              = zBits[batchIndex];
                    bits.SetBits(zIndex & 0x3f, true);
                    zBits[batchIndex] = bits;
                    var batchBits     = zBitBatches[batchFieldIndex];
                    batchBits.SetBits(batchIndex & 0x3f, true);
                    zBitBatches[batchFieldIndex] = batchBits;
                }
                else
                {
                    var previousLink         = links[previousLinkIndex];
                    links[zIndex]            = new Link { nextZ = previousLink.nextZ, xIndex = xIndex };
                    previousLink.nextZ       = zIndex;
                    links[previousLinkIndex] = previousLink;
                    var bits                 = zBits[batchIndex];
                    bits.SetBits(zIndex & 0x3f, true);
                    zBits[batchIndex] = bits;
                    var batchBits     = zBitBatches[batchFieldIndex];
                    batchBits.SetBits(batchIndex & 0x3f, true);
                    zBitBatches[batchFieldIndex] = batchBits;
                }
            }

            public void Remove(int zIndex)
            {
                Hint.Assume(zIndex >= 0);
                var batchIndex      = zIndex >> 6;
                var batchFieldIndex = batchIndex >> 6;
                var bits            = zBits[batchIndex];
                bits.SetBits(zIndex & 0x3f, false);
                zBits[batchIndex] = bits;
                if (bits.Value == 0)
                {
                    var batchBits = zBitBatches[batchFieldIndex];
                    batchBits.SetBits(batchIndex & 0x3f, false);
                    zBitBatches[batchFieldIndex] = batchBits;
                }

                if (zHeadIndex == zIndex)
                {
                    zHeadIndex = links[zHeadIndex].nextZ;
                    return;
                }
                else
                {
                    // Find previous link
                    var previousLinkIndex    = FindFirstBefore(zIndex, links.Length);
                    var previousLink         = links[previousLinkIndex];
                    var link                 = links[zIndex];
                    previousLink.nextZ       = link.nextZ;
                    links[previousLinkIndex] = previousLink;
                    return;
                }
            }

            int FindFirstBefore(int searchBefore, int noFoundValue)
            {
                Hint.Assume(searchBefore >= 0 && noFoundValue >= 0);
                var batchIndex = searchBefore >> 6;
                if (zBitBatches[batchIndex >> 6].IsSet(batchIndex & 0x3f))
                {
                    var lz = math.lzcnt(zBits[batchIndex].Value & ((2ul << (searchBefore & 0x3f)) - 1));
                    if (lz != 64)
                        return (batchIndex << 6) + 63 - lz;
                    batchIndex--;
                    if (batchIndex < 0)
                        return noFoundValue;
                }
                {
                    var batchFieldIndex = batchIndex >> 6;
                    var lz              = math.lzcnt(zBitBatches[batchFieldIndex].Value & ((2ul << (batchIndex & 0x3f)) - 1));
                    while (lz == 64 && batchFieldIndex > 0)
                    {
                        batchFieldIndex--;
                        lz = zBitBatches[batchFieldIndex].CountLeadingZeros();
                    }

                    if (lz == 64)
                        return noFoundValue;
                    batchIndex = (batchFieldIndex << 6) + 63 - lz;
                    lz         = zBits[batchIndex].CountLeadingZeros();
                    return (batchIndex << 6) + 63 - lz;
                }
            }

            public struct Enumerator
            {
                NativeArray<Link> links;
                int               linkIndex;
                int               maxZIndex;

                public Enumerator(in IntervalSet set, int zStart, int zEnd)
                {
                    links     = set.links;
                    maxZIndex = math.min(zEnd, set.links.Length - 1);

                    if (set.zHeadIndex > zEnd)
                        linkIndex = set.links.Length;
                    else
                    {
                        // Find next link
                        var startBatchIndex = zStart >> 6;
                        var batchIndex      = startBatchIndex;
                        var batchFieldIndex = batchIndex >> 6;

                        var tz = math.tzcnt(set.zBitBatches[batchFieldIndex].Value & ~((1ul << (batchIndex & 0x3f)) - 1));
                        while (tz == 64 && batchFieldIndex + 1 < set.zBitBatches.Length)
                        {
                            batchFieldIndex++;
                            tz = set.zBitBatches[batchFieldIndex].CountTrailingZeros();
                        }

                        if (tz == 64)
                        {
                            // The full chain is before this zStart
                            linkIndex = set.links.Length;
                            return;
                        }

                        batchIndex = (batchFieldIndex << 6) + tz;
                        if (startBatchIndex == batchIndex)
                        {
                            tz = math.tzcnt(set.zBits[batchIndex].Value & ~((1ul << (zStart & 0x3f)) - 1));
                            if (tz == 64)
                            {
                                var newMin = (batchIndex << 6) + tz;
                                if (newMin > zEnd)
                                {
                                    // The full chain is before this zStart
                                    linkIndex = set.links.Length;
                                    return;
                                }
                                else
                                {
                                    // Need to find a new result
                                    linkIndex = new Enumerator(in set, newMin, zEnd).linkIndex;
                                    return;
                                }
                            }
                        }
                        else
                            tz    = set.zBits[batchIndex].CountTrailingZeros();
                        linkIndex = (batchIndex << 6) + tz;
                    }
                }

                public bool IsValid()
                {
                    Hint.Assume(linkIndex >= 0);
                    return linkIndex <= maxZIndex;
                }
                public void Advance()
                {
                    Hint.Assume(linkIndex >= 0);
                    linkIndex = links[linkIndex].nextZ;
                    Hint.Assume(linkIndex >= 0);
                }
                public int Current => links[linkIndex].xIndex;
            }
        }
    }

    [BurstCompile(OptimizeFor = OptimizeFor.Performance)]
    public struct DualSweepLinkedX : IJob
    {
        // This array stores the sorted orders of z mins and z maxes.
        // The integer at each index represents either of the following:
        // For a z min, it is the index of the AABB sorted by x mins.
        // For a z max, it is the index + AABB_count of the AABB sorted by x mins.
        // Therefore, a z max can be differentiated from a z min by comparing it to
        // the AABB count.
        [ReadOnly] public NativeArray<uint> zToXMinsMaxes;
        // This array stores the sorted orders of x mins and x maxes,
        // using the same convention as the previous array.
        // Integers still correlate to indices of AABBs sorted by x mins.
        [ReadOnly] public NativeArray<uint> xs;

        // This is the array we are used to, sorted by x mins.
        [ReadOnly] public NativeArray<float4> minYZmaxYZsFlipped;
        // Same for this.
        [ReadOnly] public NativeArray<Entity> entities;
        // This is our result.
        public NativeList<EntityPair> overlaps;

        struct ZRange
        {
            // This is the index sorted by z mins, not x mins.
            public int index;
            // This is the extreme backwards value using z axis indexing
            public int min;
            // This is the extreme forwards value using z axis indexing
            public int max;
        }

        public unsafe void Execute()
        {
            Hint.Assume(zToXMinsMaxes.Length == xs.Length);
            Hint.Assume(minYZmaxYZsFlipped.Length == entities.Length);
            Hint.Assume(minYZmaxYZsFlipped.Length * 2 == xs.Length);

            var zRanges = new NativeList<ZRange>(minYZmaxYZsFlipped.Length, Allocator.Temp);
            zRanges.ResizeUninitialized(minYZmaxYZsFlipped.Length);

            var zBits = new NativeList<BitField64>(minYZmaxYZsFlipped.Length / 64 + 2, Allocator.Temp);
            zBits.Resize(minYZmaxYZsFlipped.Length / 64 + 2, NativeArrayOptions.ClearMemory);

            var zToXs = new NativeArray<int>(minYZmaxYZsFlipped.Length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

            {
                int minBit           = 0;
                int zminRunningCount = 0;
                for (int i = 0; i < zToXMinsMaxes.Length; i++)
                {
                    if (zToXMinsMaxes[i] < minYZmaxYZsFlipped.Length)
                    {
                        zToXs[zminRunningCount] = (int)zToXMinsMaxes[i];
                        ref var range           = ref zRanges.ElementAt((int)zToXMinsMaxes[i]);
                        range.index             = zminRunningCount;
                        range.min               = minBit;
                        ref var bitField        = ref zBits.ElementAt(zminRunningCount >> 6);
                        bitField.SetBits(zminRunningCount & 0x3f, true);
                        zminRunningCount++;
                    }
                    else
                    {
                        ref var range    = ref zRanges.ElementAt((int)(zToXMinsMaxes[i] - (uint)minYZmaxYZsFlipped.Length));
                        range.max        = zminRunningCount;
                        ref var bitField = ref zBits.ElementAt(range.index >> 6);
                        bitField.SetBits(range.index & 0x3f, false);
                        if (range.index == minBit)
                        {
                            while (minBit <= zminRunningCount)
                            {
                                var scanBits = zBits.ElementAt(minBit >> 6);
                                var tzcnt    = scanBits.CountTrailingZeros();
                                if (tzcnt < 64)
                                {
                                    minBit = (minBit & ~0x3f) + tzcnt;
                                    break;
                                }
                                minBit = (minBit & ~0x3f) + 64;
                            }
                            minBit = math.min(minBit, zminRunningCount + 1);
                        }
                    }
                }

                //if (minBit >= minYZmaxYZsFlipped.Length)
                //    return;
            }

            var intervalSet = new IntervalSet(zBits.AsArray(), zToXs);

            for (int i = 0; i < xs.Length; i++)
            {
                if (xs[i] < minYZmaxYZsFlipped.Length)
                {
                    int currentIndex = (int)xs[i];
                    var currentYZ    = -minYZmaxYZsFlipped[currentIndex].zwxy;
                    var range        = zRanges[currentIndex];
                    var enumerator   = new IntervalSet.Enumerator(in intervalSet, range.min, range.max - 1);

                    while (enumerator.IsValid())
                    {
                        var otherIndex = enumerator.CurrentX2;
                        EnsureDoubledIndexIsValid(otherIndex, minYZmaxYZsFlipped.Length);
                        var flippedPtr = (float4*)((byte*)minYZmaxYZsFlipped.GetUnsafeReadOnlyPtr() + otherIndex * 8);
                        if (Hint.Unlikely(math.bitmask(currentYZ < *flippedPtr) == 0))
                        {
                            var entitiesPtr = (Entity*)((byte*)entities.GetUnsafeReadOnlyPtr() + otherIndex * 4);
                            overlaps.Add(new EntityPair(entities[currentIndex], entities[otherIndex / 2]));
                        }
                        enumerator.Advance();
                    }

                    intervalSet.Add(range.index, currentIndex);
                }
                else
                {
                    var range = zRanges[(int)(xs[i] - minYZmaxYZsFlipped.Length)];
                    intervalSet.Remove(range.index, in zRanges);
                }
            }
        }

        struct IntervalSet
        {
            NativeArray<BitField64> zBits;
            NativeArray<BitField64> zBitBatches;
            NativeArray<int>        zToX;
            NativeArray<int>        linksX2;

            int zHeadIndex;

            private struct Link
            {
                public int xIndex;
                public int nextZ;
            }

            public IntervalSet(NativeArray<BitField64> zBits, NativeArray<int> zToX)
            {
                this.zBits  = zBits;
                zBitBatches = new NativeArray<BitField64>(zBits.Length / 64 + 1, Allocator.Temp, NativeArrayOptions.ClearMemory);
                this.zToX   = zToX;
                linksX2     = new NativeArray<int>(zToX.Length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                zHeadIndex  = zToX.Length;
            }

            public void Add(int zIndex, int xIndex)
            {
                Hint.Assume(zIndex >= 0 && xIndex >= 0);

                var batchIndex      = zIndex >> 6;
                var batchFieldIndex = batchIndex >> 6;

                if (zHeadIndex == linksX2.Length)
                {
                    linksX2[xIndex] = linksX2.Length * 2;
                    zHeadIndex      = zIndex;
                    var bits        = zBits[batchIndex];
                    bits.SetBits(zIndex & 0x3f, true);
                    zBits[batchIndex] = bits;
                    var batchBits     = zBitBatches[batchFieldIndex];
                    batchBits.SetBits(batchIndex & 0x3f, true);
                    zBitBatches[batchFieldIndex] = batchBits;
                    return;
                }

                var previousLinkZIndex = FindFirstBeforeZ(zIndex, linksX2.Length);
                if (previousLinkZIndex < zHeadIndex || previousLinkZIndex == linksX2.Length)
                {
                    // This is first in the chain
                    linksX2[xIndex] = 2 * (zHeadIndex == linksX2.Length ? linksX2.Length : zToX[zHeadIndex]);
                    zHeadIndex      = zIndex;
                    var bits        = zBits[batchIndex];
                    bits.SetBits(zIndex & 0x3f, true);
                    zBits[batchIndex] = bits;
                    var batchBits     = zBitBatches[batchFieldIndex];
                    batchBits.SetBits(batchIndex & 0x3f, true);
                    zBitBatches[batchFieldIndex] = batchBits;
                }
                else
                {
                    var previousLinkXIndex      = zToX[previousLinkZIndex];
                    var previousLink            = linksX2[previousLinkXIndex];
                    linksX2[xIndex]             = previousLink;
                    linksX2[previousLinkXIndex] = xIndex * 2;
                    var bits                    = zBits[batchIndex];
                    bits.SetBits(zIndex & 0x3f, true);
                    zBits[batchIndex] = bits;
                    var batchBits     = zBitBatches[batchFieldIndex];
                    batchBits.SetBits(batchIndex & 0x3f, true);
                    zBitBatches[batchFieldIndex] = batchBits;
                }
            }

            public void Remove(int zIndex, in NativeList<ZRange> ranges)
            {
                Hint.Assume(zIndex >= 0);
                var batchIndex      = zIndex >> 6;
                var batchFieldIndex = batchIndex >> 6;
                var bits            = zBits[batchIndex];
                bits.SetBits(zIndex & 0x3f, false);
                zBits[batchIndex] = bits;
                if (bits.Value == 0)
                {
                    var batchBits = zBitBatches[batchFieldIndex];
                    batchBits.SetBits(batchIndex & 0x3f, false);
                    zBitBatches[batchFieldIndex] = batchBits;
                }

                if (zHeadIndex == zIndex)
                {
                    var xHeadIndex = linksX2[zToX[zIndex]] / 2;
                    if (xHeadIndex == linksX2.Length)
                        zHeadIndex = xHeadIndex;
                    else
                        zHeadIndex = ranges[xHeadIndex].index;
                    return;
                }
                else
                {
                    // Find previous link
                    var z                      = FindFirstBeforeZ(zIndex, linksX2.Length);
                    var previousLinkIndex      = zToX[z];
                    linksX2[previousLinkIndex] = linksX2[zToX[zIndex]];
                    return;
                }
            }

            int FindFirstBeforeZ(int searchBefore, int noFoundValue)
            {
                Hint.Assume(searchBefore >= 0 && noFoundValue >= 0);
                var batchIndex = searchBefore >> 6;
                if (zBitBatches[batchIndex >> 6].IsSet(batchIndex & 0x3f))
                {
                    var lz = math.lzcnt(zBits[batchIndex].Value & ((2ul << (searchBefore & 0x3f)) - 1));
                    if (lz != 64)
                        return (batchIndex << 6) + 63 - lz;
                    batchIndex--;
                    if (batchIndex < 0)
                        return noFoundValue;
                }
                {
                    var batchFieldIndex = batchIndex >> 6;
                    var lz              = math.lzcnt(zBitBatches[batchFieldIndex].Value & ((2ul << (batchIndex & 0x3f)) - 1));
                    while (lz == 64 && batchFieldIndex > 0)
                    {
                        batchFieldIndex--;
                        lz = zBitBatches[batchFieldIndex].CountLeadingZeros();
                    }

                    if (lz == 64)
                        return noFoundValue;
                    batchIndex = (batchFieldIndex << 6) + 63 - lz;
                    lz         = zBits[batchIndex].CountLeadingZeros();
                    return (batchIndex << 6) + 63 - lz;
                }
            }

            public struct Enumerator
            {
                NativeArray<int> linksX2;
                int              linkIndexX2;
                int              maxXIndexX2;

                public Enumerator(in IntervalSet set, int zStart, int zEnd)
                {
                    linksX2 = set.linksX2;

                    var firstZBefore = set.FindFirstBeforeZ(zEnd + 1, set.linksX2.Length);
                    if (firstZBefore == set.linksX2.Length || firstZBefore < zStart)
                    {
                        maxXIndexX2 = 0;
                        linkIndexX2 = 0;
                    }
                    else
                    {
                        // Find next link
                        var startBatchIndex = zStart >> 6;
                        var batchIndex      = startBatchIndex;
                        var batchFieldIndex = batchIndex >> 6;

                        var tz = math.tzcnt(set.zBitBatches[batchFieldIndex].Value & ~((1ul << (batchIndex & 0x3f)) - 1));
                        while (tz == 64)
                        {
                            batchFieldIndex++;
                            tz = set.zBitBatches[batchFieldIndex].CountTrailingZeros();
                        }

                        batchIndex = (batchFieldIndex << 6) + tz;
                        if (startBatchIndex == batchIndex)
                        {
                            tz = math.tzcnt(set.zBits[batchIndex].Value & ~((1ul << (zStart & 0x3f)) - 1));
                            if (tz == 64)
                            {
                                var temp    = new Enumerator(in set, (batchIndex << 6) + 64, firstZBefore);
                                maxXIndexX2 = temp.maxXIndexX2;
                                linkIndexX2 = temp.linkIndexX2;
                                return;
                            }
                        }
                        else
                            tz      = set.zBits[batchIndex].CountTrailingZeros();
                        linkIndexX2 = set.zToX[(batchIndex << 6) + tz] * 2;
                        maxXIndexX2 = linksX2[set.zToX[firstZBefore]];
                    }
                }

                public bool IsValid()
                {
                    Hint.Assume(linkIndexX2 >= 0 && linkIndexX2 % 2 == 0);
                    return linkIndexX2 != maxXIndexX2;
                }
                public unsafe void Advance()
                {
                    EnsureDoubledIndexIsValid(linkIndexX2, linksX2.Length);
                    Hint.Assume(linkIndexX2 >= 0 && linkIndexX2 % 2 == 0);
                    var ptr     = (int*)((byte*)linksX2.GetUnsafeReadOnlyPtr() + linkIndexX2 * 2);
                    linkIndexX2 = *ptr;
                    //linkIndexX2 = linksX2[linkIndexX2 / 2];
                    Hint.Assume(linkIndexX2 >= 0 && linkIndexX2 % 2 == 0);
                }
                public int CurrentX2 => linkIndexX2;
            }
        }
        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        static void EnsureDoubledIndexIsValid(int indexX2, int length)
        {
            if (indexX2 >= length * 2)
                throw new System.IndexOutOfRangeException($"Violated index out of range for doubled index {indexX2} and length {length}");
        }
    }

    [BurstCompile(OptimizeFor = OptimizeFor.Performance)]
    public struct FlippedSweep2 : IJob
    {
        [ReadOnly] public NativeArray<float>  xmins;
        [ReadOnly] public NativeArray<float>  xmaxs;
        [ReadOnly] public NativeArray<float4> minYZmaxYZsFlipped;
        [ReadOnly] public NativeArray<Entity> entities;
        public NativeList<EntityPair>         overlaps;

        public void Execute()
        {
            Hint.Assume(xmins.Length == xmaxs.Length);
            Hint.Assume(xmins.Length == minYZmaxYZsFlipped.Length);

            for (int i = 0; i < xmins.Length - 1; i++)
            {
                float4 current = -minYZmaxYZsFlipped[i].zwxy;
                var    xmax    = xmaxs[i];

                for (int j = i + 1; Hint.Likely(j < xmaxs.Length && xmins[j] <= xmax); j++)
                {
                    if (Hint.Unlikely(math.bitmask(current < minYZmaxYZsFlipped[j]) == 0))
                    {
                        overlaps.Add(new EntityPair(entities[i], entities[j]));
                    }
                }
            }
        }
    }

    [BurstCompile(OptimizeFor = OptimizeFor.Performance)]
    public struct AVXSweep : IJob
    {
        [ReadOnly] public NativeArray<float>  xmins;
        [ReadOnly] public NativeArray<float>  xmaxs;
        [ReadOnly] public NativeArray<float4> minYZmaxYZsFlipped;
        [ReadOnly] public NativeArray<Entity> entities;
        public NativeList<EntityPair>         overlaps;

        public unsafe void Execute()
        {
            Hint.Assume(xmins.Length == xmaxs.Length);
            Hint.Assume(xmins.Length == minYZmaxYZsFlipped.Length);

            if (!X86.Avx.IsAvxSupported)
                return;

            for (int i = 0; i < xmins.Length - 1; i++)
            {
                float4 current     = -minYZmaxYZsFlipped[i].zwxy;
                v256   current256  = new v256(current.x, current.y, current.z, current.w, current.x, current.y, current.z, current.w);
                float  xmax        = xmaxs[i];
                var    xminsPtr    = (byte*)xmins.GetUnsafeReadOnlyPtr() + 4 * i + 4;
                var    flippedPtr  = (byte*)minYZmaxYZsFlipped.GetUnsafeReadOnlyPtr() + 16 * i + 16;
                var    entitiesPtr = (byte*)entities.GetUnsafeReadOnlyPtr() + 8 * i;
                var    count       = 4 * (ulong)(xmaxs.Length - (i + 1));

                ulong j = 0;
                for (; Hint.Likely(j < (count & ~0x7ul) && *(float*)(xminsPtr + j + 4) <= xmax); j += 8)
                {
                    v256 otherPairs = X86.Avx.mm256_loadu_ps(flippedPtr + 4 * j);
                    var  cmpBools   = X86.Avx.mm256_cmp_ps(current256, otherPairs, (int)X86.Avx.CMP.LT_OQ);
                    var  cmpResult  = X86.Avx.mm256_movemask_ps(cmpBools);
                    if (Hint.Unlikely((cmpResult & 0xf) == 0))
                    {
                        overlaps.Add(new EntityPair(*(Entity*)entitiesPtr, *(Entity*)(entitiesPtr + 2 * j + 8)));
                    }
                    if (Hint.Unlikely((cmpResult & 0xf0) == 0))
                    {
                        overlaps.Add(new EntityPair(*(Entity*)entitiesPtr, *(Entity*)(entitiesPtr + 2 * j + 16)));
                    }
                }
                if (j < count && *(float*)(xminsPtr + j) <= xmax)
                {
                    if (Hint.Unlikely(math.bitmask(current < *(float4*)(flippedPtr + 4 * j)) == 0))
                    {
                        overlaps.Add(new EntityPair(*(Entity*)entitiesPtr, *(Entity*)(entitiesPtr + 2 * j + 8)));
                    }
                }
            }
        }
    }
}

