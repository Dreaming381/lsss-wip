using Latios.Transforms.Abstract;
using Unity.Burst;
using Unity.Entities;
using Unity.Rendering;

namespace Latios.Kinemation.Systems
{
    [UpdateInGroup(typeof(StructuralChangePresentationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.EntitySceneOptimizations | WorldSystemFilterFlags.Editor)]
    [RequireMatchingQueriesForUpdate]
    [DisableAutoCreation]
    [BurstCompile]
    public partial struct LatiosAddWorldAndChunkRenderBoundsSystem : ISystem
    {
        EntityQuery m_MissingWorldRenderBounds;
        EntityQuery m_MissingWorldChunkRenderBounds;
        EntityQuery m_deadMeshesQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            m_MissingWorldRenderBounds =
                state.Fluent().With<RenderBounds>(true).WithWorldTransformReadOnly().Without<WorldRenderBounds>().IncludePrefabs().IncludeDisabledEntities().Build();

            m_MissingWorldChunkRenderBounds =
                state.Fluent().With<RenderBounds>(true).WithWorldTransformReadOnly().Without<ChunkWorldRenderBounds>(true).IncludePrefabs().IncludeDisabledEntities().
                Build();

            m_deadMeshesQuery = state.Fluent().With<ChunkWorldRenderBounds>(true, true).Without<WorldRenderBounds>().IncludePrefabs().IncludeDisabledEntities().Build();
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.EntityManager.AddComponent(m_MissingWorldRenderBounds,      ComponentType.ReadWrite<WorldRenderBounds>());
            state.EntityManager.AddComponent(m_MissingWorldChunkRenderBounds, ComponentType.ChunkComponent<ChunkWorldRenderBounds>());
            state.EntityManager.RemoveChunkComponentData<ChunkWorldRenderBounds>(m_deadMeshesQuery);
        }
    }
}

