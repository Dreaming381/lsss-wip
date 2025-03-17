using System;
using System.Diagnostics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace Latios.Psyshock
{
    public struct CollisionWorld
    {
        internal CollisionLayer               layer;
        internal NativeList<short>            archetypeIndicesByBody;
        internal NativeList<EntityArchetype>  archetypesInLayer;
        internal NativeList<int2>             archetypeStartsAndCountsByBucket;  // Archetype is inner array
        internal NativeList<int>              archetypeBodyIndicesByBucket;  // Relative
        internal NativeList<IntervalTreeNode> archetypeIntervalTreesByBucket;
        internal byte                         worldIndex;

        internal CollisionWorld(CollisionLayerSettings settings, AllocatorManager.AllocatorHandle allocator, byte worldIndex)
        {
            layer                            = new CollisionLayer(settings, allocator);
            archetypeIndicesByBody           = new NativeList<short>(allocator);
            archetypesInLayer                = new NativeList<EntityArchetype>(allocator);
            archetypeStartsAndCountsByBucket = new NativeList<int2>(allocator);
            archetypeBodyIndicesByBucket     = new NativeList<int>(allocator);
            archetypeIntervalTreesByBucket   = new NativeList<IntervalTreeNode>(allocator);
            this.worldIndex                  = worldIndex;
        }

        /// <summary>
        /// Creates an empty CollisionWorld. This is useful when you just need an empty world in order to reuse some other codepath.
        /// However, if you need a normal world, you should use Physics.BuildCollisionWorld() instead.
        /// </summary>
        /// <param name="settings">The settings to use for the world. You typically want to match this with other layers when using FindPairs.</param>
        /// <param name="allocator">The Allocator to use for this world. Despite being empty, this world is still allocated and may require disposal.</param>
        /// <param name="worldIndex">An index allocated to the world which may be stored in a CollisionWorldIndex component on an entity</param>
        /// <returns>A CollisionWorld with zero bodies, but with the bucket distribution matching the specified settings</returns>
        public static CollisionWorld CreateEmptyCollisionLayer(CollisionLayerSettings settings, AllocatorManager.AllocatorHandle allocator, byte worldIndex = 1)
        {
            CheckWorldIndexIsValid(worldIndex);

            return new CollisionWorld
            {
                layer                            = CollisionLayer.CreateEmptyCollisionLayer(settings, allocator),
                archetypeIndicesByBody           = new NativeList<short>(allocator),
                archetypesInLayer                = new NativeList<EntityArchetype>(allocator),
                archetypeStartsAndCountsByBucket = new NativeList<int2>(allocator),
                archetypeBodyIndicesByBucket     = new NativeList<int>(allocator),
                archetypeIntervalTreesByBucket   = new NativeList<IntervalTreeNode>(allocator),
                worldIndex                       = worldIndex
            };
        }

        /// <summary>
        /// Disposes the layer immediately
        /// </summary>
        public void Dispose()
        {
            layer.Dispose();
            archetypeIndicesByBody.Dispose();
            archetypesInLayer.Dispose();
            archetypeStartsAndCountsByBucket.Dispose();
            archetypeBodyIndicesByBucket.Dispose();
            archetypeIntervalTreesByBucket.Dispose();
        }

        /// <summary>
        /// Disposes the layer using jobs
        /// </summary>
        /// <param name="inputDeps">A JobHandle to wait upon before disposing</param>
        /// <returns>The final jobHandle of the disposed layers</returns>
        public JobHandle Dispose(JobHandle inputDeps)
        {
            layer.worldSubdivisionsPerAxis = 0;
            return CollectionsExtensions.CombineDependencies(stackalloc JobHandle[]
            {
                layer.bucketStartsAndCounts.Dispose(inputDeps),
                layer.xmins.Dispose(inputDeps),
                layer.xmaxs.Dispose(inputDeps),
                layer.yzminmaxs.Dispose(inputDeps),
                layer.intervalTrees.Dispose(inputDeps),
                layer.bodies.Dispose(inputDeps),
                layer.srcIndices.Dispose(inputDeps),
                archetypeIndicesByBody.Dispose(inputDeps),
                archetypesInLayer.Dispose(inputDeps),
                archetypeStartsAndCountsByBucket.Dispose(inputDeps),
                archetypeBodyIndicesByBucket.Dispose(inputDeps),
                archetypeIntervalTreesByBucket.Dispose(inputDeps)
            });
        }

        /// <summary>
        /// The number of elements in the layer
        /// </summary>
        public int count => layer.xmins.Length;
        /// <summary>
        /// The number of cells in the layer, including the "catch-all" cell but ignoring the NaN cell
        /// </summary>
        public int bucketCount => layer.bucketCount;
        /// <summary>
        /// True if the CollisionLayer has been created
        /// </summary>
        public bool IsCreated => worldIndex != 0;
        /// <summary>
        /// Read-Only access to the collider bodiesArray stored in the CollisionLayer ordered by bodyIndex
        /// </summary>
        public NativeArray<ColliderBody>.ReadOnly colliderBodies => layer.colliderBodies;
        /// <summary>
        /// Read-Only access to the source indices corresponding to each bodyIndex. CollisionLayers
        /// reorder bodiesArray for better performance. The source indices specify the original index of
        /// each body in an EntityQuery or NativeArray of ColliderBody.
        /// </summary>
        public NativeArray<int>.ReadOnly sourceIndices => layer.sourceIndices;
        /// <summary>
        /// Gets an Aabb for an associated index in the collision layer ordered by bodyIndex
        /// </summary>
        public Aabb GetAabb(int index) => layer.GetAabb(index);

        [Conditional("ENABLE_UNITY_COLLECTION_CHECKS")]
        internal static void CheckWorldIndexIsValid(byte worldIndex)
        {
            if (worldIndex == 0)
                throw new ArgumentOutOfRangeException("The worldIndex must be greater than 0 in a CollisionWorld");
        }
    }
}

