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
            m_newPlayerShipQuery = Fluent.WithAll<PlayerTag>(true).WithAll<NewShipTag>(true).Build();
            m_oldShipQuery       = Fluent.WithAll<ShipTag>(true).WithAll<FactionMember>().IncludeDisabled().Build();
            m_newAiShipQuery     = Fluent.WithAll<NewShipTag>(true).Without<PlayerTag>().Build();

            m_entityListCache = new NativeList<Entity>(Allocator.Persistent);
        }

        protected override void OnDestroy()
        {
            m_entityListCache.Dispose();
        }

        protected override void OnUpdate()
        {
            bool needPlayer = m_oldPlayerShipQuery.IsEmptyIgnoreFilter;

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
                    if (!EntityManager.HasComponent<FactionMember>(newPlayerShip))
                        EntityManager.AddSharedComponentData(newPlayerShip, factionFilter);
                    unitsToSpawn--;
                    faction.remainingReinforcements--;
                }

                if (unitsToSpawn > 0)
                {
                    var newShipPrefab = EntityManager.Instantiate(faction.aiPrefab);
                    EntityManager.AddComponent<NewShipTag>(newShipPrefab);
                    AddSharedComponentDataToLinkedGroup(newShipPrefab, factionFilter);
                    if (!EntityManager.HasComponent<FactionMember>(newShipPrefab))
                        EntityManager.AddSharedComponentData(newShipPrefab, factionFilter);
                    EntityManager.Instantiate(newShipPrefab, unitsToSpawn, Allocator.Temp);
                    EntityManager.DestroyEntity(newShipPrefab);
                    faction.remainingReinforcements -= unitsToSpawn;
                }

                m_oldShipQuery.ResetFilter();
            }).Run();

            SpawnQueues spawnQueues = default;
            if (sceneGlobalEntity.HasCollectionComponent<SpawnQueues>())
            {
                spawnQueues = sceneGlobalEntity.GetCollectionComponent<SpawnQueues>();
            }
            else
            {
                spawnQueues.playerQueue = new NativeQueue<Entity>(Allocator.Persistent);
                spawnQueues.aiQueue     = new NativeQueue<Entity>(Allocator.Persistent);
                sceneGlobalEntity.AddCollectionComponent(spawnQueues);
            }

            /*float x = 0f;
               Entities.WithAll<NewShipTag>().ForEach((ref Translation trans) =>
               {
                trans = new Translation { Value  = new float3(x, 0f, 0f) };
                x                               += 5f;
               }).Run();

               EntityManager.RemoveComponent<NewShipTag>(m_newPlayerShipQuery);
               EntityManager.RemoveComponent<NewShipTag>(m_newAiShipQuery);*/

            var newPlayerShips = m_newPlayerShipQuery.ToEntityArray(Allocator.TempJob);
            var newAiShips     = m_newAiShipQuery.ToEntityArray(Allocator.TempJob);

            EntityManager.RemoveComponent<NewShipTag>(m_newAiShipQuery);
            EntityManager.RemoveComponent<NewShipTag>(m_newPlayerShipQuery);

            //NativeQueue API could use EnqueueRange method
            Job.WithCode(() =>
            {
                for (int i = 0; i < newPlayerShips.Length; i++)
                {
                    spawnQueues.playerQueue.Enqueue(newPlayerShips[i]);
                }

                int end = newAiShips.Length;
                for (int i = 0; i < end; i++)
                {
                    spawnQueues.aiQueue.Enqueue(newAiShips[i]);
                    spawnQueues.aiQueue.Enqueue(newAiShips[end - 1]);
                    end--;
                }
            }).Run();

            //SetEnabled badly needs bursted batch API
            foreach (var e in newPlayerShips)
            {
                EntityManager.SetEnabled(e, false);
            }
            foreach(var e in newAiShips)
            {
                EntityManager.SetEnabled(e, false);
            }

            newPlayerShips.Dispose();
            newAiShips.Dispose();
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

