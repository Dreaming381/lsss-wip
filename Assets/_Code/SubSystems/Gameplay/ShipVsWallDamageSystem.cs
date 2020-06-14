using Latios;
using Latios.PhysicsEngine;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace Lsss
{
    public class ShipVsWallDamageSystem : SubSystem
    {
        protected override void OnUpdate()
        {
            var wallLayer = sceneGlobalEntity.GetCollectionComponent<WallCollisionLayer>(true).layer;

            var processor = new DamageHitShipsProcessor
            {
                wallDamageCdfe = GetComponentDataFromEntity<Damage>(true),
                shipHealthCdfe = this.GetPhysicsComponentDataFromEntity<ShipHealth>(),
            };

            var backup = Dependency;
            Dependency = default;

            Entities.WithAll<FactionTag>().ForEach((Entity entity, int entityInQueryIndex) =>
            {
                if (entityInQueryIndex == 0)
                    Dependency = backup;

                var shipLayer = EntityManager.GetCollectionComponent<FactionShipsCollisionLayer>(entity, true).layer;
                Dependency    = Physics.FindPairs(wallLayer, shipLayer, processor).ScheduleParallel(Dependency);
            }).WithoutBurst().Run();
        }

        //Assumes A is wall and B is ship.
        struct DamageHitShipsProcessor : IFindPairsProcessor
        {
            public PhysicsComponentDataFromEntity<ShipHealth> shipHealthCdfe;
            [ReadOnly] public ComponentDataFromEntity<Damage> wallDamageCdfe;

            public void Execute(FindPairsResult result)
            {
                if (Physics.DistanceBetween(result.bodyA.collider, result.bodyA.transform, result.bodyB.collider, result.bodyB.transform, 0f, out _))
                {
                    var damage = wallDamageCdfe[result.entityA];
                    var health = shipHealthCdfe[result.entityB];

                    health.health -= damage.damage;

                    shipHealthCdfe[result.entityB] = health;
                }
            }
        }
    }
}

