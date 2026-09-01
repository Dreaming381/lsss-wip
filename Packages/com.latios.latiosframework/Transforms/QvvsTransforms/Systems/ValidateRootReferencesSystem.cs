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
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            var api = this.OnCreateForLatios(ref state);
            api.GetDefaultQuery<Job>().SetOrderVersionFilter();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var api = this.GetApi(ref state);
            new Job().ScheduleParallel(api);
        }

        [BurstCompile]
        [IncludeDisabledEntities, IncludePrefabs]
        partial struct Job : IJobEach
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

