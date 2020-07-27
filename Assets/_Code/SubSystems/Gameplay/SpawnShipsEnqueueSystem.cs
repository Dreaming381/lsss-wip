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
        NativeList<Entity> m_playersWithoutReinforcements;

        protected override void OnCreate()
        {
            m_oldPlayerShipQuery = Fluent.WithAll<ShipTag>(true).WithAll<PlayerTag>().WithAll<FactionMember>().IncludeDisabled().Build();
            m_newPlayerShipQuery = Fluent.WithAll<PlayerTag>(true).WithAll<NewShipTag>(true).IncludeDisabled().Build();
            m_oldShipQuery       = Fluent.WithAll<ShipTag>(true).WithAll<FactionMember>().IncludeDisabled().Build();
            m_newAiShipQuery     = Fluent.WithAll<NewShipTag>(true).Without<PlayerTag>().IncludeDisabled().Build();

            m_entityListCache              = new NativeList<Entity>(Allocator.Persistent);
            m_playersWithoutReinforcements = new NativeList<Entity>(Allocator.Persistent);
        }

        protected override void OnDestroy()
        {
            m_entityListCache.Dispose();
            m_playersWithoutReinforcements.Dispose();
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
                    if (faction.remainingReinforcements <= 0)
                    {
                        m_playersWithoutReinforcements.Add(newPlayerShip);
                    }
                    else
                    {
                        EntityManager.SetEnabled(newPlayerShip, false);
                        unitsToSpawn--;
                        faction.remainingReinforcements--;
                        spawnQueues.playerQueue.Enqueue(newPlayerShip);
                    }
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

            foreach (var player in m_playersWithoutReinforcements)
            {
                if (!FindAndStealAiShip(player, spawnQueues))
                    EntityManager.DestroyEntity(player);
            }
            m_playersWithoutReinforcements.Clear();
        }

        bool FindAndStealAiShip(Entity player, SpawnQueues spawnQueues)
        {
            var factionMember        = EntityManager.GetSharedComponentData<FactionMember>(player);
            var disabledShipsHashSet = new NativeHashSet<Entity>(1024, Allocator.TempJob);
            Entities.WithAll<ShipTag,
                             Disabled>().WithNone<PlayerTag>().WithSharedComponentFilter(factionMember).ForEach((Entity entity) => { disabledShipsHashSet.Add(entity); }).Run();
            if (!disabledShipsHashSet.IsEmpty)
            {
                bool foundShip = false;
                Job.WithCode(() =>
                {
                    var queueArray = spawnQueues.aiQueue.ToArray(Allocator.Temp);
                    for (int i = 0; i < queueArray.Length; i++)
                    {
                        if (disabledShipsHashSet.Contains(queueArray[i]))
                        {
                            foundShip = true;
                            //Todo: Is there a cleaner way to remove a random element from the queue?
                            spawnQueues.aiQueue.Clear();
                            for (int j = 0; j < queueArray.Length; j++)
                            {
                                if (j != i)
                                    spawnQueues.aiQueue.Enqueue(queueArray[j]);
                            }
                            spawnQueues.playerQueue.Enqueue(player);
                            return;
                        }
                    }
                }).Run();
                if (!foundShip)
                {
                    //A spawner might have it.
                    Entity oldEntity = Entity.Null;
                    Entities.WithAll<SpawnPointTag>().ForEach((ref SpawnPayload payload) =>
                    {
                        if (!foundShip && disabledShipsHashSet.Contains(payload.disabledShip))
                        {
                            foundShip            = true;
                            oldEntity            = payload.disabledShip;
                            payload.disabledShip = player;
                        }
                    }).Run();
                    EntityManager.DestroyEntity(oldEntity);
                }
                if (foundShip)
                {
                    EntityManager.SetEnabled(player, false);
                    return true;
                }
            }
            disabledShipsHashSet.Dispose();

            //Todo: We could swap the player with an existing ship, but this could get confusing if the player prefab is not the same model as an AI prefab.

            return false;
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

