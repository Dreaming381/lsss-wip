using Latios;
using Latios.Psyshock;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace Lsss
{
    public partial class ShipVsWallDamageSystem : SubSystem
    {
        protected override void OnUpdate()
        {
            var wallLayer = sceneBlackboardEntity.GetCollectionComponent<WallCollisionLayer>(true).layer;

            var processor = new DamageHitShipsProcessor
            {
                wallDamageCdfe = GetComponentLookup<Damage>(true),
                shipHealthCdfe = GetComponentLookup<ShipHealth>(),
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
            public PhysicsComponentLookup<ShipHealth> shipHealthCdfe;
            [ReadOnly] public ComponentLookup<Damage> wallDamageCdfe;

            public void Execute(in FindPairsResult result)
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

