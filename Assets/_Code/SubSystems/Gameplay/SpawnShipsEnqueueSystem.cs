using Latios;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace Lsss
{
    public partial class SpawnShipsEnqueueSystem : SubSystem
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

        public override void OnNewScene()
        {
            sceneBlackboardEntity.AddOrSetCollectionComponentAndDisposeOld(new SpawnQueues
            {
                playerQueue               = new NativeQueue<EntityWith<Disabled> >(Allocator.Persistent),
                aiQueue                   = new NativeQueue<EntityWith<Disabled> >(Allocator.Persistent),
                newAiEntitiesToPrioritize = new NativeList<EntityWith<Disabled> >(Allocator.Persistent),
                factionRanges             = new NativeList<SpawnQueues.FactionRanges>(Allocator.Persistent)
            });
        }

        protected override void OnUpdate()
        {
            bool needPlayer  = m_oldPlayerShipQuery.IsEmptyIgnoreFilter;
            var  spawnQueues = sceneBlackboardEntity.GetCollectionComponent<SpawnQueues>();

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
                    spawnQueues.newAiEntitiesToPrioritize.AddRange(newEntities.Reinterpret<EntityWith<Disabled> >());
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
            var factionMember        = EntityManager.GetSharedComponentManaged<FactionMember>(player);
            var disabledShipsHashSet = new NativeParallelHashSet<Entity>(1024, Allocator.TempJob);
            Entities.WithAll<ShipTag,
                             Disabled>().WithNone<PlayerTag>().WithSharedComponentFilter(factionMember).ForEach((Entity entity) => { disabledShipsHashSet.Add(entity); }).Run();
            if (!disabledShipsHashSet.IsEmpty)
            {
                Entity foundShip = Entity.Null;

                Job.WithCode(() =>
                {
                    var queueArray = spawnQueues.aiQueue.ToArray(Allocator.Temp);
                    for (int i = 0; i < queueArray.Length; i++)
                    {
                        if (disabledShipsHashSet.Contains(queueArray[i]))
                        {
                            foundShip = queueArray[i];
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
                if (foundShip == Entity.Null)
                {
                    //A spawner might have it.
                    Entities.WithAll<SpawnPointTag>().ForEach((ref SpawnPayload payload) =>
                    {
                        if (foundShip == Entity.Null && disabledShipsHashSet.Contains(payload.disabledShip))
                        {
                            foundShip            = payload.disabledShip;
                            payload.disabledShip = player;
                        }
                    }).Run();
                }
                if (foundShip != Entity.Null)
                {
                    EntityManager.SetEnabled(player, false);
                    EntityManager.DestroyEntity(foundShip);
                    disabledShipsHashSet.Dispose();
                    return true;
                }
            }
            disabledShipsHashSet.Dispose();

            //Todo: Is there a more robust way to do this?
            float  bestHealth = 0f;
            Entity bestEntity = Entity.Null;
            Entities.WithAll<ShipTag, AiTag>().WithSharedComponentFilter(factionMember).ForEach((Entity entity, in ShipHealth health) =>
            {
                if (health.health > bestHealth)
                {
                    bestHealth = health.health;
                    bestEntity = entity;
                }
            }).Run();

            if (bestEntity != Entity.Null)
            {
                EntityManager.RemoveComponent<AiTag>(bestEntity);
                EntityManager.AddComponent<PlayerTag>(bestEntity);
                return false;
            }

            return false;
        }

        void AddSharedComponentDataToLinkedGroup<T>(Entity root, T sharedComponent) where T : struct, ISharedComponentData
        {
            m_entityListCache.Clear();
            m_entityListCache.AddRange(EntityManager.GetBuffer<LinkedEntityGroup>(root).Reinterpret<Entity>().AsNativeArray());
            foreach (var e in m_entityListCache)
            {
                EntityManager.AddSharedComponentManaged(e, sharedComponent);
            }
            if (!EntityManager.HasComponent<T>(root))
                EntityManager.AddSharedComponentManaged(root, sharedComponent);
        }
    }
}

