using Latios.Psyshock;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Rendering;

namespace Latios.Kinemation.Systems
{
    // This exists because setting the chunk bounds to an extreme value breaks shadows.
    // Instead we calculate the combined chunk bounds for all skeletons and then write them to all skinned mesh chunk bounds.
    [DisableAutoCreation]
    [BurstCompile]
    public partial struct UpdateSkinnedMeshChunkBoundsSystem : ISystem
    {
        EntityQuery m_exposedMetaQuery;
        EntityQuery m_optimizedMetaQuery;
        EntityQuery m_skinnedMeshMetaQuery;

        public void OnCreate(ref SystemState state)
        {
            m_exposedMetaQuery     = state.Fluent().WithAll<ChunkHeader>(true).WithAll<ChunkBoneWorldBounds>(true).Build();
            m_optimizedMetaQuery   = state.Fluent().WithAll<ChunkHeader>(true).WithAll<ChunkSkeletonWorldBounds>(true).Build();
            m_skinnedMeshMetaQuery = state.Fluent().WithAll<ChunkHeader>(true).WithAll<ChunkWorldRenderBounds>(false)
                                     .WithAny<ChunkComputeDeformMemoryMetadata>(true).WithAny<ChunkLinearBlendSkinningMemoryMetadata>(true).Build();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var combinedBounds   = new NativeReference<Aabb>(state.WorldUnmanaged.UpdateAllocator.ToAllocator);
            combinedBounds.Value = new Aabb(float.MaxValue, float.MinValue);

            state.Dependency = new CombineExposedJob
            {
                handle         = state.GetComponentTypeHandle<ChunkBoneWorldBounds>(true),
                combinedBounds = combinedBounds
            }.Schedule(m_exposedMetaQuery, state.Dependency);

            state.Dependency = new CombineOptimizedJob
            {
                handle         = state.GetComponentTypeHandle<ChunkSkeletonWorldBounds>(true),
                combinedBounds = combinedBounds
            }.Schedule(m_optimizedMetaQuery, state.Dependency);

            state.Dependency = new ApplyChunkBoundsToSkinnedMeshesJob
            {
                handle         = state.GetComponentTypeHandle<ChunkWorldRenderBounds>(),
                combinedBounds = combinedBounds
            }.Schedule(m_skinnedMeshMetaQuery, state.Dependency);
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state) {
        }

        [BurstCompile]
        struct CombineExposedJob : IJobEntityBatch
        {
            [ReadOnly] public ComponentTypeHandle<ChunkBoneWorldBounds> handle;
            public NativeReference<Aabb>                                combinedBounds;

            public void Execute(ArchetypeChunk batchInChunk, int batchIndex)
            {
                Aabb aabb   = new Aabb(float.MaxValue, float.MinValue);
                var  bounds = batchInChunk.GetNativeArray(handle);
                for (int i = 0; i < batchInChunk.Count; i++)
                {
                    var b = bounds[i].chunkBounds;
                    aabb  = Physics.CombineAabb(aabb, new Aabb(b.Min, b.Max));
                }
                combinedBounds.Value = Physics.CombineAabb(combinedBounds.Value, aabb);
            }
        }

        [BurstCompile]
        struct CombineOptimizedJob : IJobEntityBatch
        {
            [ReadOnly] public ComponentTypeHandle<ChunkSkeletonWorldBounds> handle;
            public NativeReference<Aabb>                                    combinedBounds;

            public void Execute(ArchetypeChunk batchInChunk, int batchIndex)
            {
                Aabb aabb   = new Aabb(float.MaxValue, float.MinValue);
                var  bounds = batchInChunk.GetNativeArray(handle);
                for (int i = 0; i < batchInChunk.Count; i++)
                {
                    var b = bounds[i].chunkBounds;
                    aabb  = Physics.CombineAabb(aabb, new Aabb(b.Min, b.Max));
                }
                combinedBounds.Value = Physics.CombineAabb(combinedBounds.Value, aabb);
            }
        }

        [BurstCompile]
        struct ApplyChunkBoundsToSkinnedMeshesJob : IJobEntityBatch
        {
            [ReadOnly] public NativeReference<Aabb>            combinedBounds;
            public ComponentTypeHandle<ChunkWorldRenderBounds> handle;

            public void Execute(ArchetypeChunk batchInChunk, int batchIndex)
            {
                var aabb   = new ChunkWorldRenderBounds { Value = FromAabb(combinedBounds.Value) };
                var bounds                                      = batchInChunk.GetNativeArray(handle);
                for (int i = 0; i < batchInChunk.Count; i++)
                {
                    bounds[i] = aabb;
                }
            }

            public static AABB FromAabb(Aabb aabb)
            {
                Physics.GetCenterExtents(aabb, out float3 center, out float3 extents);
                return new AABB { Center = center, Extents = extents };
            }
        }
    }
}

