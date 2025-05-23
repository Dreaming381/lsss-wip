using Latios;
using Latios.Transforms.Abstract;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Rendering;

namespace Latios.Kinemation.Systems
{
    // Todo: What is this below TODO talking about? Regardless, this always needs to update to check the RecreateAllBatchesFlag.
    //@TODO: Updating always necessary due to empty component group. When Component group and archetype chunks are unified, [RequireMatchingQueriesForUpdate] can be added.
    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
    //[UpdateInGroup(typeof(StructuralChangePresentationSystemGroup))]
    [DisableAutoCreation]
    [BurstCompile]
    public partial struct LatiosUpdateEntitiesGraphicsChunkStructureSystem : ISystem
    {
        private EntityQuery m_MissingHybridChunkInfo;
        private EntityQuery m_DisabledRenderingQuery;
        private EntityQuery m_destroyedChunkInfoQuery;
        private EntityQuery m_destroyedTransformQuery;
#if UNITY_EDITOR
        private EntityQuery m_HasHybridChunkInfo;
#endif
        public void OnCreate(ref SystemState state)
        {
            m_MissingHybridChunkInfo = state.Fluent().With<ChunkWorldRenderBounds>(true, true).With<WorldRenderBounds>(true).WithWorldTransformReadOnly()
                                       .With<MaterialMeshInfo>(true).Without<EntitiesGraphicsChunkInfo>(true).Without<DisableRendering>().IncludePrefabs().Build();

            m_DisabledRenderingQuery = state.Fluent().With<EntitiesGraphicsChunkInfo>(true, true)
                                       .WithAnyEnabled<Disabled, DisableRendering>(true).IncludeDisabledEntities().IncludePrefabs().Build();

            m_destroyedChunkInfoQuery = state.Fluent().With<EntitiesGraphicsChunkInfo>(true, true).Without<MaterialMeshInfo>().Build();
            m_destroyedTransformQuery = state.Fluent().With<EntitiesGraphicsChunkInfo>(true, true).WithoutWorldTransform().IncludeDisabledEntities().IncludePrefabs().Build();

#if UNITY_EDITOR
            m_HasHybridChunkInfo = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ChunkComponentReadOnly<EntitiesGraphicsChunkInfo>(),
                },
            });
#endif
        }

        //[BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
#if UNITY_EDITOR
            if (EntitiesGraphicsEditorTools.DebugSettings.RecreateAllBatches)
            {
                UnityEngine.Debug.Log("Recreating all batches");
                state.EntityManager.RemoveChunkComponentData<EntitiesGraphicsChunkInfo>(m_HasHybridChunkInfo);
            }
#endif

            DoChanges(ref state, ref this);
        }

        [BurstCompile]
        static void DoChanges(ref SystemState state, ref LatiosUpdateEntitiesGraphicsChunkStructureSystem system)
        {
            state.EntityManager.AddComponent(system.m_MissingHybridChunkInfo, ComponentType.ChunkComponent<EntitiesGraphicsChunkInfo>());
            state.EntityManager.RemoveChunkComponentData<EntitiesGraphicsChunkInfo>(system.m_DisabledRenderingQuery);
            state.EntityManager.RemoveChunkComponentData<EntitiesGraphicsChunkInfo>(system.m_destroyedChunkInfoQuery);
            state.EntityManager.RemoveChunkComponentData<EntitiesGraphicsChunkInfo>(system.m_destroyedTransformQuery);
        }
    }
}

