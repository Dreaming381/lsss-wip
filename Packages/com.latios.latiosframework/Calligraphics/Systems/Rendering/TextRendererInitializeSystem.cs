using static Unity.Entities.SystemAPI;
using Unity.Burst;
using Unity.Entities;
using Unity.Rendering;

namespace Latios.Calligraphics.Systems
{
    [DisableAutoCreation]
    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
    [UpdateBefore(typeof(GenerateGlyphsSystem))]
    [RequireMatchingQueriesForUpdate]
    [BurstCompile]
    public partial struct TextRendererInitializeSystem : ISystem
    {
        EntityQuery m_newGlyphsQuery;
        EntityQuery m_deadMmiQuery;
        EntityQuery m_deadRenderBoundsQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            m_newGlyphsQuery =
                QueryBuilder().WithPresent<MaterialMeshInfo, RenderBounds>().WithAny<RenderGlyph, AnimatedRenderGlyph>().WithAbsent<PreviousRenderGlyph>().WithOptions(
                    EntityQueryOptions.IgnoreComponentEnabledState).Build();
            m_deadMmiQuery          = QueryBuilder().WithPresent<PreviousRenderGlyph>().WithAbsent<MaterialMeshInfo>().Build();
            m_deadRenderBoundsQuery = QueryBuilder().WithPresent<PreviousRenderGlyph>().WithAbsent<RenderBounds>().Build();
            state.EntityManager.CreateSingleton<NewEntitiesArrays>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            SetSingleton(new NewEntitiesArrays
            {
                newGlyphEntities               = m_newGlyphsQuery.ToEntityArray(state.WorldUpdateAllocator),
                lastTouchedGlobalSystemVersion = state.GlobalSystemVersion
            });

            var renderingComponents = new ComponentTypeSet(ComponentType.ReadWrite<PreviousRenderGlyph>(), ComponentType.ReadWrite<GpuState>(),
                                                           ComponentType.ReadWrite<ResidentRange>());
            state.EntityManager.AddComponent(m_newGlyphsQuery, in renderingComponents);
            var glyphComponents = new ComponentTypeSet(ComponentType.ReadWrite<RenderGlyph>(), ComponentType.ReadWrite<AnimatedRenderGlyph>());
            state.EntityManager.RemoveComponent(m_deadMmiQuery,          in glyphComponents);
            state.EntityManager.RemoveComponent(m_deadRenderBoundsQuery, in glyphComponents);
        }
    }
}

