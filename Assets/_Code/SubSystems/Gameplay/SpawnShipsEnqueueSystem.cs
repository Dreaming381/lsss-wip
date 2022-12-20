using Latios;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

using static Unity.Entities.SystemAPI;

namespace Lsss
{
    [BurstCompile]
    public partial struct SpawnShipsEnqueueSystem : ISystem, ISystemNewScene
    {
        private struct NewShipTag : IComponentData { }

        private EntityQuery m_oldPlayerShipQuery;
        private EntityQuery m_newPlayerShipQuery;
        private EntityQuery m_oldShipQuery;
        private EntityQuery m_newAiShipQuery;

        NativeList<Entity> m_entityListCache;
        NativeList<Entity> m_playersWithoutReinforcements;

        LatiosWorldUnmanaged latiosWorld;

        public void OnCreate(ref SystemState state)
        {
            m_oldPlayerShipQuery = state.Fluent().WithAll<ShipTag>(true).WithAll<PlayerTag>().WithAll<FactionMember>().IncludeDisabledEntities().Build();
            m_newPlayerShipQuery = state.Fluent().WithAll<PlayerTag>(true).WithAll<NewShipTag>(true).IncludeDisabledEntities().Build();
            m_oldShipQuery       = state.Fluent().WithAll<ShipTag>(true).WithAll<FactionMember>().IncludeDisabledEntities().Build();
            m_newAiShipQuery     = state.Fluent().WithAll<NewShipTag>(true).Without<PlayerTag>().IncludeDisabledEntities().Build();

            m_entityListCache              = new NativeList<Entity>(Allocator.Persistent);
            m_playersWithoutReinforcements = new NativeList<Entity>(Allocator.Persistent);

            latiosWorld = state.GetLatiosWorldUnmanaged();
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            m_entityListCache.Dispose();
            m_playersWithoutReinforcements.Dispose();
        }

        public void OnNewScene(ref SystemState state)
        {
            latiosWorld.sceneBlackboardEntity.AddOrSetCollectionComponentAndDisposeOld(new SpawnQueues
            {
                playerQueue               = new NativeQueue<EntityWith<Disabled> >(Allocator.Persistent),
                aiQueue                   = new NativeQueue<EntityWith<Disabled> >(Allocator.Persistent),
                newAiEntitiesToPrioritize = new NativeList<EntityWith<Disabled> >(Allocator.Persistent),
                factionRanges             = new NativeList<SpawnQueues.FactionRanges>(Allocator.Persistent)
            });
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            bool needPlayer  = m_oldPlayerShipQuery.IsEmptyIgnoreFilter;
            var  spawnQueues = latiosWorld.sceneBlackboardEntity.GetCollectionComponent<SpawnQueues>();

            var factionQuery    = QueryBuilder().WithAllRW<Faction>().WithAll<FactionTag>().Build();
            var factionEntities = factionQuery.ToEntityArray(Allocator.Temp);
            foreach (var entity in factionEntities)
            {
                var faction = GetComponent<Faction>(entity);

                var factionFilter = new FactionMember { factionEntity = entity };
                m_oldShipQuery.SetSharedComponentFilter(factionFilter);
                int unitsToSpawn = faction.maxFieldUnits - m_oldShipQuery.CalculateEntityCount();
                unitsToSpawn     = math.min(unitsToSpawn, faction.remainingReinforcements);

                if (needPlayer && faction.playerPrefab != Entity.Null)
                {
                    var newPlayerShip = state.EntityManager.Instantiate(faction.playerPrefab);
                    state.EntityManager.AddComponent<NewShipTag>(newPlayerShip);
                    AddSharedComponentDataToLinkedGroup(ref state, newPlayerShip, factionFilter);
                    if (faction.remainingReinforcements <= 0)
                    {
                        m_playersWithoutReinforcements.Add(newPlayerShip);
                    }
                    else
                    {
                        state.EntityManager.SetEnabled(newPlayerShip, false);
                        unitsToSpawn--;
                        faction.remainingReinforcements--;
                        spawnQueues.playerQueue.Enqueue(newPlayerShip);
                    }
                }

                if (unitsToSpawn > 0)
                {
                    var newShipPrefab = state.EntityManager.Instantiate(faction.aiPrefab);
                    state.EntityManager.AddComponent<NewShipTag>(newShipPrefab);
                    AddSharedComponentDataToLinkedGroup(ref state, newShipPrefab, factionFilter);
                    state.EntityManager.SetEnabled(newShipPrefab, false);
                    var newEntities = state.EntityManager.Instantiate(newShipPrefab, unitsToSpawn, Allocator.TempJob);
                    int start       = spawnQueues.newAiEntitiesToPrioritize.Length;
                    spawnQueues.newAiEntitiesToPrioritize.AddRange(newEntities.Reinterpret<EntityWith<Disabled> >());
                    spawnQueues.factionRanges.Add(new SpawnQueues.FactionRanges
                    {
                        start  = start,
                        count  = newEntities.Length,
                        weight = faction.spawnWeightInverse / newEntities.Length
                    });
                    newEntities.Dispose();
                    state.EntityManager.DestroyEntity(newShipPrefab);
                    faction.remainingReinforcements -= unitsToSpawn;
                }

                m_oldShipQuery.ResetFilter();

                SetComponent(entity, faction);
            }

            state.EntityManager.RemoveComponent<NewShipTag>(m_newAiShipQuery);
            state.EntityManager.RemoveComponent<NewShipTag>(m_newPlayerShipQuery);

            foreach (var player in m_playersWithoutReinforcements)
            {
                if (!FindAndStealAiShip(ref state, player, spawnQueues))
                    state.EntityManager.DestroyEntity(player);
            }
            m_playersWithoutReinforcements.Clear();
        }

        bool FindAndStealAiShip(ref SystemState state, Entity player, SpawnQueues spawnQueues)
        {
            var factionMember = state.EntityManager.GetSharedComponent<FactionMember>(player);

            var disabledShipQuery = QueryBuilder().WithAll<ShipTag, Disabled, FactionMember>().WithNone<PlayerTag>().Build();
            disabledShipQuery.SetSharedComponentFilter(factionMember);
            var disabledShipEntities = disabledShipQuery.ToEntityArray(Allocator.Temp);
            if (disabledShipEntities.Length > 0)
            {
                var disabledShipsHashSet = new NativeHashSet<Entity>(1024, Allocator.Temp);
                foreach (var entity in disabledShipEntities)
                {
                    disabledShipsHashSet.Add(entity);
                }
                Entity foundShip = Entity.Null;

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
                        break;
                    }
                }
                if (foundShip == Entity.Null)
                {
                    //A spawner might have it.
                    foreach(var payload in Query<RefRW<SpawnPayload> >().WithAll<SpawnPointTag>())
                    {
                        if (foundShip == Entity.Null && disabledShipsHashSet.Contains(payload.ValueRW.disabledShip))
                        {
                            foundShip                    = payload.ValueRW.disabledShip;
                            payload.ValueRW.disabledShip = player;
                        }
                    }
                }
                if (foundShip != Entity.Null)
                {
                    state.EntityManager.SetEnabled(player, false);
                    state.EntityManager.DestroyEntity(foundShip);
                    disabledShipsHashSet.Dispose();
                    return true;
                }
            }

            //Todo: Is there a more robust way to do this?
            float  bestHealth = 0f;
            Entity bestEntity = Entity.Null;
            foreach ((var health, var entity) in Query<RefRO<ShipHealth> >().WithEntityAccess().WithSharedComponentFilter(factionMember).WithAll<ShipTag, AiTag>())
            {
                if (health.ValueRO.health > bestHealth)
                {
                    bestHealth = health.ValueRO.health;
                    bestEntity = entity;
                }
            }

            if (bestEntity != Entity.Null)
            {
                state.EntityManager.RemoveComponent<AiTag>(bestEntity);
                state.EntityManager.AddComponent<PlayerTag>(bestEntity);
                return false;
            }

            return false;
        }

        void AddSharedComponentDataToLinkedGroup<T>(ref SystemState state, Entity root, T sharedComponent) where T : unmanaged, ISharedComponentData
        {
            m_entityListCache.Clear();
            m_entityListCache.AddRange(state.EntityManager.GetBuffer<LinkedEntityGroup>(root).Reinterpret<Entity>().AsNativeArray());
            state.EntityManager.AddSharedComponent(m_entityListCache.AsArray(), sharedComponent);

            if (!state.EntityManager.HasComponent<T>(root))
                state.EntityManager.AddSharedComponent(root, sharedComponent);
        }
    }
}

