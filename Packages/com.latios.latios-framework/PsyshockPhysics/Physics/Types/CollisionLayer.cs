using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Latios.Psyshock
{
    public struct CollisionLayerSettings
    {
        public Aabb worldAABB;
        public int3 worldSubdivisionsPerAxis;
    }

    public struct CollisionLayer : IDisposable
    {
        internal NativeArray<int2>                                                   bucketStartsAndCounts;
        [NativeDisableParallelForRestriction] internal NativeArray<float>            xmins;
        [NativeDisableParallelForRestriction] internal NativeArray<float>            xmaxs;
        [NativeDisableParallelForRestriction] internal NativeArray<float4>           yzminmaxs;
        [NativeDisableParallelForRestriction] internal NativeArray<IntervalTreeNode> intervalTrees;
        [NativeDisableParallelForRestriction] internal NativeArray<ColliderBody>     bodies;
        internal float3                                                              worldMin;
        internal float3                                                              worldAxisStride;
        internal int3                                                                worldSubdivisionsPerAxis;

        //Todo: World settings?
        internal CollisionLayer(int bodyCount, CollisionLayerSettings settings, AllocatorManager.AllocatorHandle allocator)
        {
            worldMin                 = settings.worldAABB.min;
            worldAxisStride          = (settings.worldAABB.max - worldMin) / settings.worldSubdivisionsPerAxis;
            worldSubdivisionsPerAxis = settings.worldSubdivisionsPerAxis;

            bucketStartsAndCounts = CollectionHelper.CreateNativeArray<int2>(
                settings.worldSubdivisionsPerAxis.x * settings.worldSubdivisionsPerAxis.y * settings.worldSubdivisionsPerAxis.z + 2,
                allocator,
                NativeArrayOptions.UninitializedMemory);
            xmins         = CollectionHelper.CreateNativeArray<float>(bodyCount, allocator, NativeArrayOptions.UninitializedMemory);
            xmaxs         = CollectionHelper.CreateNativeArray<float>(bodyCount, allocator, NativeArrayOptions.UninitializedMemory);
            yzminmaxs     = CollectionHelper.CreateNativeArray<float4>(bodyCount, allocator, NativeArrayOptions.UninitializedMemory);
            intervalTrees = CollectionHelper.CreateNativeArray<IntervalTreeNode>(bodyCount, allocator, NativeArrayOptions.UninitializedMemory);
            bodies        = CollectionHelper.CreateNativeArray<ColliderBody>(bodyCount, allocator, NativeArrayOptions.UninitializedMemory);
        }

        public CollisionLayer(CollisionLayer sourceLayer, AllocatorManager.AllocatorHandle allocator)
        {
            worldMin                 = sourceLayer.worldMin;
            worldAxisStride          = sourceLayer.worldAxisStride;
            worldSubdivisionsPerAxis = sourceLayer.worldSubdivisionsPerAxis;

            bucketStartsAndCounts = CollectionHelper.CreateNativeArray(sourceLayer.bucketStartsAndCounts, allocator);
            xmins                 = CollectionHelper.CreateNativeArray(sourceLayer.xmins, allocator);
            xmaxs                 = CollectionHelper.CreateNativeArray(sourceLayer.xmaxs, allocator);
            yzminmaxs             = CollectionHelper.CreateNativeArray(sourceLayer.yzminmaxs, allocator);
            intervalTrees         = CollectionHelper.CreateNativeArray(sourceLayer.intervalTrees, allocator);
            bodies                = CollectionHelper.CreateNativeArray(sourceLayer.bodies, allocator);
        }

        public void Dispose()
        {
            bucketStartsAndCounts.Dispose();
            xmins.Dispose();
            xmaxs.Dispose();
            yzminmaxs.Dispose();
            intervalTrees.Dispose();
            bodies.Dispose();
        }

        public unsafe JobHandle Dispose(JobHandle inputDeps)
        {
            JobHandle* deps = stackalloc JobHandle[6]
            {
                bucketStartsAndCounts.Dispose(inputDeps),
                xmins.Dispose(inputDeps),
                xmaxs.Dispose(inputDeps),
                yzminmaxs.Dispose(inputDeps),
                intervalTrees.Dispose(inputDeps),
                bodies.Dispose(inputDeps)
            };
            return Unity.Jobs.LowLevel.Unsafe.JobHandleUnsafeUtility.CombineDependencies(deps, 6);
        }

        public int Count => xmins.Length;
        public int BucketCount => bucketStartsAndCounts.Length - 1;  // For algorithmic purposes, we pretend the nan bucket doesn't exist.

        public bool IsCreated => bucketStartsAndCounts.IsCreated;

        internal BucketSlices GetBucketSlices(int bucketIndex)
        {
            int start = bucketStartsAndCounts[bucketIndex].x;
            int count = bucketStartsAndCounts[bucketIndex].y;

            return new BucketSlices
            {
                xmins             = xmins.GetSubArray(start, count),
                xmaxs             = xmaxs.GetSubArray(start, count),
                yzminmaxs         = yzminmaxs.GetSubArray(start, count),
                intervalTree      = intervalTrees.GetSubArray(start, count),
                bodies            = bodies.GetSubArray(start, count),
                bucketIndex       = bucketIndex,
                bucketGlobalStart = start
            };
        }
    }

    internal struct BucketSlices
    {
        public NativeArray<float>            xmins;
        public NativeArray<float>            xmaxs;
        public NativeArray<float4>           yzminmaxs;
        public NativeArray<IntervalTreeNode> intervalTree;
        public NativeArray<ColliderBody>     bodies;
        public int count => xmins.Length;
        public int bucketIndex;
        public int bucketGlobalStart;
    }

    internal struct IntervalTreeNode
    {
        public float xmin;
        public float xmax;
        public float subtreeXmax;
        public int   bucketRelativeBodyIndex;
    }

    /*public struct RayQueryLayer : IDisposable
     *  {
     *   public NativeArray<int2>   bucketRanges;
     *   public NativeArray<float>  xmin;
     *   public NativeArray<float>  xmax;
     *   public NativeArray<float4> yzminmax;
     *   public NativeArray<Entity> entity;
     *   public NativeArray<Ray>    ray;
     *   public float               gridSpacing;
     *   public int                 gridCells1DFromOrigin;
     *
     *   public RayQueryLayer(EntityQuery query, int gridCells1DFromOrigin, float worldHalfExtent, Allocator allocator)
     *   {
     *       this.gridCells1DFromOrigin = gridCells1DFromOrigin;
     *       gridSpacing                = worldHalfExtent / gridCells1DFromOrigin;
     *       int entityCount            = query.CalculateLength();
     *       bucketRanges               = CollectionHelper.CreateNativeArray<int2>(gridCells1DFromOrigin * gridCells1DFromOrigin + 1, allocator, NativeArrayOptions.UninitializedMemory);
     *       xmin                       = CollectionHelper.CreateNativeArray<float>(entityCount, allocator, NativeArrayOptions.UninitializedMemory);
     *       xmax                       = CollectionHelper.CreateNativeArray<float>(entityCount, allocator, NativeArrayOptions.UninitializedMemory);
     *       yzminmax                   = CollectionHelper.CreateNativeArray<float4>(entityCount, allocator, NativeArrayOptions.UninitializedMemory);
     *       entity                     = CollectionHelper.CreateNativeArray<Entity>(entityCount, allocator, NativeArrayOptions.UninitializedMemory);
     *       ray                        = CollectionHelper.CreateNativeArray<Ray>(entityCount, allocator, NativeArrayOptions.UninitializedMemory);
     *   }
     *
     *   public void Dispose()
     *   {
     *       bucketRanges.Dispose();
     *       xmin.Dispose();
     *       xmax.Dispose();
     *       yzminmax.Dispose();
     *       entity.Dispose();
     *       ray.Dispose();
     *   }
     *  }*/
}

