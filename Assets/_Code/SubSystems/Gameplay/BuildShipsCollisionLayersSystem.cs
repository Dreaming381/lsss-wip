using Latios;
using Latios.Psyshock;
using Latios.Transforms;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

using static Unity.Entities.SystemAPI;

namespace Lsss
{
    [BurstCompile]
    public partial struct BuildShipsCollisionLayersSystem : ISystem, ISystemNewScene
    {
        private EntityQuery                    m_query;
        private BuildCollisionLayerTypeHandles m_typeHandles;

        LatiosWorldUnmanaged latiosWorld;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            m_query       = state.Fluent().With<ShipTag>(true).With<FactionMember>().PatchQueryForBuildingCollisionLayer().Build();
            m_typeHandles = new BuildCollisionLayerTypeHandles(ref state);

            latiosWorld = state.GetLatiosWorldUnmanaged();
        }

        public void OnNewScene(ref SystemState state) => latiosWorld.sceneBlackboardEntity.AddOrSetCollectionComponentAndDisposeOld(new ShipsCollisionLayer());

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            m_typeHandles.Update(ref state);

            var settings = latiosWorld.sceneBlackboardEntity.GetComponentData<ArenaCollisionSettings>().settings;

            state.Dependency = Physics.BuildCollisionLayer(m_query, in m_typeHandles).WithSettings(settings)
                               .ScheduleParallel(out CollisionLayer layer, Allocator.Persistent, state.Dependency);
            latiosWorld.sceneBlackboardEntity.SetCollectionComponentAndDisposeOld(new ShipsCollisionLayer { layer = layer });
        }
    }

    //public partial class DebugDrawFactionShipsCollisionLayersSystem : SubSystem
    //{
    //    protected override void OnUpdate()
    //    {
    //        Entities.WithAll<FactionTag, Faction>()
    //        .ForEach((Entity factionEntity) =>
    //        {
    //            var layer = EntityManager.GetCollectionComponent<FactionShipsCollisionLayer>(factionEntity, true);
    //            CompleteDependency();
    //            PhysicsDebug.DrawLayer(layer.layer).Run();
    //        }).WithoutBurst().Run();
    //    }
    //}

    //public partial class DebugDrawFactionShipsCollidersSystem : SubSystem
    //{
    //    protected override void OnUpdate()
    //    {
    //        Entities.WithAll<ShipTag>().ForEach((in Collider collider, in WorldTransform worldTransform) =>
    //        {
    //            PhysicsDebug.DrawCollider(in collider, in worldTransform.worldTransform, UnityEngine.Color.green);
    //            if (collider.type == ColliderType.Box)
    //                UnityEngine.Debug.Log("Box collider");
    //            else if (collider.type == ColliderType.Compound)
    //            {
    //                CompoundCollider compound = collider;
    //                if (compound.compoundColliderBlob.Value.colliders[0].type == ColliderType.Box)
    //                {
    //                    UnityEngine.Debug.Log($"Compound with {compound.compoundColliderBlob.Value.colliders.Length} colliders and scale {compound.scale}");
    //                }
    //            }
    //        }).Schedule();
    //    }
    //}
}

