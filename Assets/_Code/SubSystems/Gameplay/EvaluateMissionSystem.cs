using Latios;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Lsss
{
    public class EvaluateMissionSystem : SubSystem
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

        protected override void OnCreate()
        {
            m_query = Fluent.WithAll<ShipTag>(true).WithAll<FactionMember>().IncludeDisabled().Build();
        }

        protected override void OnUpdate()
        {
            MissionStatus status = new MissionStatus { status = MissionStatus.Options.Complete };

            Entities.WithAll<FactionTag, LastAliveObjectiveTag>().ForEach((Entity entity) =>
            {
                status = GetDestroyAllMissionStatus(entity);

                m_query.SetSharedComponentFilter(new FactionMember { factionEntity = entity });
                bool alive                                                         = !m_query.IsEmpty;
                if (!alive)
                    status.status = MissionStatus.Options.Failed;
            }).WithoutBurst().Run();

            Entities.WithAll<FactionTag>().ForEach((Entity entity, in DynamicBuffer<DestroyFactionObjective> dfo) =>
            {
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
            }).WithoutBurst().Run();

            if (status.status == MissionStatus.Options.Failed)
            {
                latiosWorld.SyncPoint.CreateEntityCommandBuffer().AddComponent(sceneBlackboardEntity, new RequestLoadScene { newScene = "Mission Failed" });
            }
            else if (status.status == MissionStatus.Options.Complete)
            {
                latiosWorld.SyncPoint.CreateEntityCommandBuffer().AddComponent(sceneBlackboardEntity, new RequestLoadScene { newScene = "Mission Complete" });
            }
        }

        MissionStatus GetDestroyAllMissionStatus(Entity missionFaction)
        {
            MissionStatus status = new MissionStatus { status = MissionStatus.Options.Complete };
            Entities.WithAll<FactionTag>().ForEach((Entity entity) =>
            {
                if (entity == missionFaction)
                    return;
                m_query.SetSharedComponentFilter(new FactionMember { factionEntity = entity });
                bool alive                                                         = !m_query.IsEmpty;
                if (alive)
                    status.status = MissionStatus.Options.InProgress;
            }).WithoutBurst().Run();
            return status;
        }
    }
}

