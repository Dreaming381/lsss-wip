using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

//Todo: Stream types, caches, scratchlists, and inflations
namespace Latios.PhysicsEngine
{
    public interface IFindPairsProcessor
    {
        void Execute(FindPairsResult result);
    }

    [NativeContainer]
    public struct FindPairsResult
    {
        public ColliderBody bodyA;
        public ColliderBody bodyB;
        public int          bodyAIndex;
        public int          bodyBIndex;
        public int          jobIndex;

        public SafeEntity entityA => new SafeEntity { entity = bodyA.entity };
        public SafeEntity entityB => new SafeEntity { entity = bodyB.entity };
        //Todo: Shorthands for calling narrow phase distance and manifold queries
    }

    public static partial class Physics
    {
        public static FindPairsConfig<T> FindPairs<T>(CollisionLayer layer, T processor) where T : struct, IFindPairsProcessor
        {
            return new FindPairsConfig<T>
            {
                processor    = processor,
                layerA       = layer,
                isLayerLayer = false
            };
        }

        public static FindPairsConfig<T> FindPairs<T>(CollisionLayer layerA, CollisionLayer layerB, T processor) where T : struct, IFindPairsProcessor
        {
            return new FindPairsConfig<T>
            {
                processor    = processor,
                layerA       = layerA,
                layerB       = layerB,
                isLayerLayer = true
            };
        }
    }

    public partial struct FindPairsConfig<T> where T : struct, IFindPairsProcessor
    {
        internal T processor;

        internal CollisionLayer layerA;
        internal CollisionLayer layerB;

        internal bool isLayerLayer;

        #region Schedulers
        public void RunImmediate()
        {
            if (isLayerLayer)
            {
                FindPairsInternal.RunImmediate(layerA, layerB, processor);
            }
            else
            {
                FindPairsInternal.RunImmediate(layerA, processor);
            }
        }

        public void Run()
        {
            if (isLayerLayer)
            {
                new FindPairsInternal.LayerLayerSingle
                {
                    layerA    = layerA,
                    layerB    = layerB,
                    processor = processor
                }.Run();
            }
            else
            {
                new FindPairsInternal.LayerSelfSingle
                {
                    layer     = layerA,
                    processor = processor
                }.Run();
            }
        }

        public JobHandle ScheduleSingle(JobHandle inputDeps = default)
        {
            if (isLayerLayer)
            {
                return new FindPairsInternal.LayerLayerSingle
                {
                    layerA    = layerA,
                    layerB    = layerB,
                    processor = processor
                }.Schedule(inputDeps);
            }
            else
            {
                return new FindPairsInternal.LayerSelfSingle
                {
                    layer     = layerA,
                    processor = processor
                }.Schedule(inputDeps);
            }
        }

        public JobHandle ScheduleParallel(JobHandle inputDeps = default)
        {
            if (isLayerLayer)
            {
                JobHandle jh = new FindPairsInternal.LayerLayerPart1
                {
                    layerA    = layerA,
                    layerB    = layerB,
                    processor = processor
                }.Schedule(layerB.BucketCount, 1, inputDeps);
                jh = new FindPairsInternal.LayerLayerPart2
                {
                    layerA    = layerA,
                    layerB    = layerB,
                    processor = processor
                }.Schedule(2, 1, jh);
                return jh;
            }
            else
            {
                JobHandle jh = new FindPairsInternal.LayerSelfPart1
                {
                    layer     = layerA,
                    processor = processor
                }.Schedule(layerA.BucketCount, 1, inputDeps);
                jh = new FindPairsInternal.LayerSelfPart2
                {
                    layer     = layerA,
                    processor = processor
                }.Schedule(jh);
                return jh;
            }
        }

        public JobHandle ScheduleParallelUnsafe(JobHandle inputDeps = default)
        {
            if (isLayerLayer)
            {
                return new FindPairsInternal.LayerLayerParallelUnsafe
                {
                    layerA    = layerA,
                    layerB    = layerB,
                    processor = processor
                }.ScheduleParallel(3 * layerA.BucketCount - 2, 1, inputDeps);
            }
            else
            {
                return new FindPairsInternal.LayerSelfParallelUnsafe
                {
                    layer     = layerA,
                    processor = processor
                }.ScheduleParallel(2 * layerA.BucketCount - 1, 1, inputDeps);
            }
        }
        #endregion Schedulers
    }
}

