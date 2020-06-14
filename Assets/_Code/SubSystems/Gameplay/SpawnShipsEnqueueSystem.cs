using Latios;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace Lsss
{
    [AlwaysUpdateSystem]
    public class SpawnShipsEnqueueSystem : SubSystem
    {
        private struct NewShipTag : IComponentData { }

        private EntityQuery m_oldPlayerShipQuery;
        private EntityQuery m_newPlayerShipQuery;
        private EntityQuery m_oldShipQuery;
        private EntityQuery m_newAiShipQuery;

        NativeList<Entity> m_entityListCache;

        protected override void OnCreate()
        {
            m_oldPlayerShipQuery = Fluent.WithAll<ShipTag>(true).WithAll<PlayerTag>().WithAll<FactionMember>().IncludeDisabled().Build();
            m_newPlayerShipQuery = Fluent.WithAll<PlayerTag>(true).WithAll<NewShipTag>(true).IncludeDisabled().Build();
            m_oldShipQuery       = Fluent.WithAll<ShipTag>(true).WithAll<FactionMember>().IncludeDisabled().Build();
            m_newAiShipQuery     = Fluent.WithAll<NewShipTag>(true).Without<PlayerTag>().IncludeDisabled().Build();

            m_entityListCache = new NativeList<Entity>(Allocator.Persistent);
        }

        protected override void OnDestroy()
        {
            m_entityListCache.Dispose();
        }

        protected override void OnUpdate()
        {
            bool needPlayer = m_oldPlayerShipQuery.IsEmptyIgnoreFilter;

            SpawnQueues spawnQueues = default;
            if (sceneGlobalEntity.HasCollectionComponent<SpawnQueues>())
            {
                spawnQueues = sceneGlobalEntity.GetCollectionComponent<SpawnQueues>();
            }
            else
            {
                spawnQueues.playerQueue               = new NativeQueue<Entity>(Allocator.Persistent);
                spawnQueues.aiQueue                   = new NativeQueue<Entity>(Allocator.Persistent);
                spawnQueues.newAiEntitiesToPrioritize = new NativeList<Entity>(Allocator.Persistent);
                spawnQueues.factionRanges             = new NativeList<SpawnQueues.FactionRanges>(Allocator.Persistent);
                sceneGlobalEntity.AddCollectionComponent(spawnQueues);
            }

            Entities.WithStructuralChanges().WithAll<FactionTag>().ForEach((Entity entity, ref Faction faction) =>
            {
                var factionFilter = new FactionMember { factionEntity = entity };
                m_oldShipQuery.SetSharedComponentFilter(factionFilter);
                int unitsToSpawn = faction.maxFieldUnits - m_oldShipQuery.CalculateEntityCount();
                unitsToSpawn     = math.min(unitsToSpawn, faction.remainingReinforcements);

                if (needPlayer && faction.playerPrefab != Entity.Null)
                {
                    var newPlayerShip = EntityManager.Instantiate(faction.playerPrefab);
                    EntityManager.AddComponent<NewShipTag>(newPlayerShip);
                    AddSharedComponentDataToLinkedGroup(newPlayerShip, factionFilter);
                    EntityManager.SetEnabled(newPlayerShip, false);
                    unitsToSpawn--;
                    faction.remainingReinforcements--;
                    spawnQueues.playerQueue.Enqueue(newPlayerShip);
                }

                if (unitsToSpawn > 0)
                {
                    var newShipPrefab = EntityManager.Instantiate(faction.aiPrefab);
                    EntityManager.AddComponent<NewShipTag>(newShipPrefab);
                    AddSharedComponentDataToLinkedGroup(newShipPrefab, factionFilter);
                    EntityManager.SetEnabled(newShipPrefab, false);
                    var newEntities = EntityManager.Instantiate(newShipPrefab, unitsToSpawn, Allocator.TempJob);
                    int start       = spawnQueues.newAiEntitiesToPrioritize.Length;
                    spawnQueues.newAiEntitiesToPrioritize.AddRange(newEntities);
                    spawnQueues.factionRanges.Add(new SpawnQueues.FactionRanges
                    {
                        start  = start,
                        count  = newEntities.Length,
                        weight = faction.spawnWeightInverse / newEntities.Length
                    });
                    newEntities.Dispose();
                    EntityManager.DestroyEntity(newShipPrefab);
                    faction.remainingReinforcements -= unitsToSpawn;
                }

                m_oldShipQuery.ResetFilter();
            }).Run();

            EntityManager.RemoveComponent<NewShipTag>(m_newAiShipQuery);
            EntityManager.RemoveComponent<NewShipTag>(m_newPlayerShipQuery);
        }

        void AddSharedComponentDataToLinkedGroup<T>(Entity root, T sharedComponent) where T : struct, ISharedComponentData
        {
            m_entityListCache.Clear();
            m_entityListCache.AddRange(EntityManager.GetBuffer<LinkedEntityGroup>(root).Reinterpret<Entity>().AsNativeArray());
            foreach (var e in m_entityListCache)
            {
                EntityManager.AddSharedComponentData(e, sharedComponent);
            }
            if (!EntityManager.HasComponent<T>(root))
                EntityManager.AddSharedComponentData(root, sharedComponent);
        }
    }
}

