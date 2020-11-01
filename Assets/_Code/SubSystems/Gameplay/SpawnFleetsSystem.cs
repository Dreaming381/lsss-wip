using Debug = UnityEngine.Debug;
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
    public class SpawnFleetsSystem : SubSystem
    {
        private struct NewFleetTag : IComponentData { }

        EntityQuery m_playerQuery;
        EntityQuery m_aiQuery;

        NativeList<Entity> m_entityListCache;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_entityListCache = new NativeList<Entity>(Allocator.Persistent);
        }

        protected override void OnDestroy()
        {
            m_entityListCache.Dispose();
        }

        public override bool ShouldUpdateSystem()
        {
            var currentScene = worldGlobalEntity.GetComponentData<CurrentScene>();
            return currentScene.isFirstFrame;
        }

        protected override void OnUpdate()
        {
            Entities.WithStructuralChanges().WithAll<FactionTag>().ForEach((Entity entity, ref Faction faction) =>
            {
                var factionMember = new FactionMember { factionEntity = entity };
                m_aiQuery.SetSharedComponentFilter(factionMember);
                m_playerQuery.SetSharedComponentFilter(factionMember);

                if (faction.playerPrefab != Entity.Null && !m_playerQuery.IsEmpty)
                {
                    var newPlayerShip = EntityManager.Instantiate(faction.playerPrefab);
                    AddSharedComponentDataToLinkedGroup(newPlayerShip, factionMember);
                    SpawnPlayer(newPlayerShip);
                    faction.remainingReinforcements--;
                }

                {
                    int spawnCount    = m_aiQuery.CalculateEntityCount();
                    var newShipPrefab = EntityManager.Instantiate(faction.aiPrefab);
                    EntityManager.AddComponent<NewFleetTag>(newShipPrefab);
                    AddSharedComponentDataToLinkedGroup(newShipPrefab, factionMember);
                    var newShips = EntityManager.Instantiate(newShipPrefab, spawnCount, Allocator.TempJob);
                    EntityManager.DestroyEntity(newShipPrefab);
                    SpawnAi(newShips);
                    newShips.Dispose();
                    faction.remainingReinforcements -= spawnCount;
                }

                m_aiQuery.ResetFilter();
                m_playerQuery.ResetFilter();
            }).Run();

            EntityManager.RemoveComponent<NewFleetTag>(m_aiQuery);
        }

        void SpawnPlayer(Entity newPlayerShip)
        {
            Entities.WithAll<FactionMember>().WithAll<FleetSpawnSlotTag, FleetSpawnPlayerSlotTag>().WithStoreEntityQueryInField(ref m_playerQuery)
            .ForEach((int entityInQueryIndex, in LocalToWorld ltw) =>
            {
                if (entityInQueryIndex == 0)
                {
                    var rotation                                        = quaternion.LookRotationSafe(ltw.Forward, ltw.Up);
                    SetComponent(newPlayerShip, new Rotation { Value    = rotation });
                    SetComponent(newPlayerShip, new Translation { Value = ltw.Position });
                }
            }).Run();
        }

        void SpawnAi(NativeArray<Entity> newShips)
        {
            Entities.WithAll<FactionMember>().WithAll<FleetSpawnSlotTag>().WithNone<FleetSpawnPlayerSlotTag>().WithStoreEntityQueryInField(ref m_aiQuery)
            .ForEach((int entityInQueryIndex, in LocalToWorld ltw) =>
            {
                var ship                                   = newShips[entityInQueryIndex];
                var rotation                               = quaternion.LookRotationSafe(ltw.Forward, new float3(0f, 1f, 0f));
                SetComponent(ship, new Rotation { Value    = rotation });
                SetComponent(ship, new Translation { Value = ltw.Position });
            }).Run();
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

