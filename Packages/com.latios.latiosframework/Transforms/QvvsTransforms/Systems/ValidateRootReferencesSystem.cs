#if !LATIOS_TRANSFORMS_UNITY
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace Latios.Transforms.Systems
{
    [RequireMatchingQueriesForUpdate]
    [DisableAutoCreation]
    [BurstCompile]
    public partial struct ValidateRootReferencesSystem : ISystem, ILatiosApi
    {
        EntityQuery m_query;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            this.OnCreateForLatios(ref state);
            m_query = state.Fluent().With<RootReference>(true).IncludePrefabs().IncludeDisabledEntities().Build();
            m_query.SetOrderVersionFilter();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var api = this.GetApi(ref state);
            new Job().Inject(api).ScheduleParallel(m_query);
        }

        [BurstCompile]
        [WithOptions(EntityQueryOptions.IncludePrefab | EntityQueryOptions.IncludeDisabledEntities)]
        partial struct Job : IJobEntity, IInjectable
        {
            [ReadOnly, Inject] public BufferLookup<EntityInHierarchy>        hierarchyLookup;
            [ReadOnly, Inject] public BufferLookup<EntityInHierarchyCleanup> cleanupLookup;

            public void Execute(Entity entity, in RootReference rootReference)
            {
                var handle = rootReference.ToHandle(ref hierarchyLookup, ref cleanupLookup);
                if (handle.entity != entity)
                    throw new System.InvalidOperationException(
                        $"{entity.ToFixedString()} contains a RootReference referencing a hierarchy this entity does not belong to. This usually means incorrect setup after instantiation occurred.");
            }
        }
    }
}
#endif

