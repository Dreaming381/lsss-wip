using Latios;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

using static Unity.Entities.SystemAPI;

namespace Lsss
{
    [RequireMatchingQueriesForUpdate]
    [BurstCompile]
    public partial struct EvaluateMissionSystem : ISystem
    {
        struct MissionStatus
        {
            public enum Options
            {
                InProgress,
                Complete,
                Failed
            }

            public Options status;
        }

        EntityQuery m_query;

        LatiosWorldUnmanaged latiosWorld;

        public void OnCreate(ref SystemState state)
        {
            latiosWorld = state.GetLatiosWorldUnmanaged();

            m_query = state.Fluent().WithAll<ShipTag>(true).WithAll<FactionMember>().IncludeDisabledEntities().Build();
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            MissionStatus status = new MissionStatus { status = MissionStatus.Options.Complete };

            var factionEntities              = QueryBuilder().WithAll<Faction, FactionTag>().Build().ToEntityArray(Allocator.Temp);
            var factionEntitiesWithLastAlive = QueryBuilder().WithAll<Faction, FactionTag>().WithAll<LastAliveObjectiveTag>().Build().ToEntityArray(Allocator.Temp);
            foreach (var entity in factionEntitiesWithLastAlive)
            {
                status = GetDestroyAllMissionStatus(entity, factionEntities);

                m_query.SetSharedComponentFilter(new FactionMember { factionEntity = entity });
                bool alive                                                         = !m_query.IsEmpty;
                if (!alive)
                    status.status = MissionStatus.Options.Failed;
            }

            var factionEntitiesWithTarget = QueryBuilder().WithAll<Faction, FactionTag, DestroyFactionObjective>().Build().ToEntityArray(Allocator.Temp);
            foreach (var entity in factionEntitiesWithTarget)
            {
                var dfo = GetBuffer<DestroyFactionObjective>(entity);

                for (int i = 0; i < dfo.Length; i++)
                {
                    m_query.SetSharedComponentFilter(new FactionMember { factionEntity = dfo[i].factionToDestroy });
                    bool alive                                                         = !m_query.IsEmpty;
                    if (alive)
                        status.status = MissionStatus.Options.InProgress;
                }
                {
                    m_query.SetSharedComponentFilter(new FactionMember { factionEntity = entity });
                    bool alive                                                         = !m_query.IsEmpty;
                    if (!alive)
                        status.status = MissionStatus.Options.Failed;
                }
            }

            if (status.status == MissionStatus.Options.Failed)
            {
                latiosWorld.syncPoint.CreateEntityCommandBuffer().AddComponent(latiosWorld.sceneBlackboardEntity, new RequestLoadScene { newScene = "Mission Failed" });
            }
            else if (status.status == MissionStatus.Options.Complete)
            {
                latiosWorld.syncPoint.CreateEntityCommandBuffer().AddComponent(latiosWorld.sceneBlackboardEntity, new RequestLoadScene { newScene = "Mission Complete" });
            }
        }

        MissionStatus GetDestroyAllMissionStatus(Entity missionFaction, NativeArray<Entity> factionEntities)
        {
            MissionStatus status = new MissionStatus { status = MissionStatus.Options.Complete };
            foreach (var entity in factionEntities)
            {
                if (entity == missionFaction)
                    continue;
                m_query.SetSharedComponentFilter(new FactionMember { factionEntity = entity });
                bool alive                                                         = !m_query.IsEmpty;
                if (alive)
                    status.status = MissionStatus.Options.InProgress;
            }
            return status;
        }
    }
}

