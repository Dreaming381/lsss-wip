using Latios;
using Latios.Psyshock;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace Lsss
{
    public partial class BuildFactionShipsCollisionLayersSystem : SubSystem
    {
        private EntityQuery m_query;

        protected override void OnCreate()
        {
            m_query = Fluent.WithAll<ShipTag>(true).WithAll<FactionMember>().PatchQueryForBuildingCollisionLayer().Build();
        }

        protected override void OnUpdate()
        {
            var settings                                                                 = sceneBlackboardEntity.GetComponentData<ArenaCollisionSettings>().settings;
            sceneBlackboardEntity.SetComponentData(new ArenaCollisionSettings { settings = settings });

            var backup = Dependency;
            Dependency = default;

            Entities.WithAll<FactionTag, Faction>().ForEach((Entity factionEntity, int entityInQueryIndex) =>
            {
                if (entityInQueryIndex == 0)
                    Dependency = backup;

                var factionMemberFilter = new FactionMember { factionEntity = factionEntity };
                m_query.SetSharedComponentFilter(factionMemberFilter);
                Dependency = Physics.BuildCollisionLayer(m_query, this).WithSettings(settings).ScheduleParallel(out CollisionLayer layer, Allocator.Persistent, Dependency);

                EntityManager.SetCollectionComponentAndDisposeOld(factionEntity, new FactionShipsCollisionLayer { layer = layer });
                m_query.ResetFilter();
            }).WithoutBurst().Run();
        }
    }

    public partial class DebugDrawFactionShipsCollisionLayersSystem : SubSystem
    {
        protected override void OnUpdate()
        {
            Entities.WithAll<FactionTag, Faction>()
            .ForEach((Entity factionEntity) =>
            {
                var layer = EntityManager.GetCollectionComponent<FactionShipsCollisionLayer>(factionEntity, true);
                CompleteDependency();
                PhysicsDebug.DrawLayer(layer.layer).Run();
            }).WithoutBurst().Run();
        }
    }
}

