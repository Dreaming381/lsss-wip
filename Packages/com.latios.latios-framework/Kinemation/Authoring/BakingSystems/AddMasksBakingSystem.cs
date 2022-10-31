using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;

using static Unity.Entities.SystemAPI;

namespace Latios.Kinemation.Authoring.Systems
{
    [RequireMatchingQueriesForUpdate]
    [WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
    [UpdateAfter(typeof(RenderMeshPostProcessSystem))]
    [DisableAutoCreation]
    [BurstCompile]
    public partial struct AddMasksBakingSystem : ISystem
    {
        EntityQuery m_addQuery;
        EntityQuery m_removeQuery;

        public void OnCreate(ref SystemState state)
        {
            m_addQuery    = state.Fluent().WithAll<MaterialMeshInfo>(true).Without<ChunkPerFrameCullingMask>(true).IncludePrefabs().IncludeDisabled().Build();
            m_removeQuery = state.Fluent().Without<MaterialMeshInfo>(true).WithAll<ChunkPerFrameCullingMask>(true).IncludePrefabs().IncludeDisabled().Build();
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var typeset = new ComponentTypeSet(ComponentType.ChunkComponent<ChunkPerCameraCullingMask>(),
                                               ComponentType.ChunkComponent<ChunkPerCameraCullingSplitsMask>(),
                                               ComponentType.ChunkComponent<ChunkPerFrameCullingMask>(),
                                               ComponentType.ChunkComponent<ChunkMaterialPropertyDirtyMask>());
            state.EntityManager.AddComponent(m_addQuery, typeset);
            state.EntityManager.RemoveComponent(m_removeQuery, typeset);
        }
    }
}

