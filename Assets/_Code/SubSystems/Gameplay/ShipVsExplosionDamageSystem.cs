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
    public partial class ShipVsExplosionDamageSystem : SubSystem
    {
        protected override void OnUpdate()
        {
            var explosionLayer = sceneBlackboardEntity.GetCollectionComponent<ExplosionCollisionLayer>(true).layer;

            var processor = new DamageHitShipsProcessor
            {
                explosionDamageLookup = GetComponentLookup<Damage>(true),
                shipHealthLookup      = GetComponentLookup<ShipHealth>(),
            };

            var backup = Dependency;
            Dependency = default;
            Entities.WithAll<FactionTag>().ForEach((Entity entity, int entityInQueryIndex) =>
            {
                if (entityInQueryIndex == 0)
                    Dependency = backup;

                var shipLayer = EntityManager.GetCollectionComponent<FactionShipsCollisionLayer>(entity, true).layer;
                Dependency    = Physics.FindPairs(explosionLayer, shipLayer, processor).ScheduleParallel(Dependency);
            }).WithoutBurst().Run();
        }

        //Assumes A is explosion and B is ship.
        struct DamageHitShipsProcessor : IFindPairsProcessor
        {
            public PhysicsComponentLookup<ShipHealth> shipHealthLookup;
            [ReadOnly] public ComponentLookup<Damage> explosionDamageLookup;

            public void Execute(in FindPairsResult result)
            {
                if (Physics.DistanceBetween(result.bodyA.collider, result.bodyA.transform, result.bodyB.collider, result.bodyB.transform, 0f, out _))
                {
                    var damage = explosionDamageLookup[result.entityA];
                    var health = shipHealthLookup[result.entityB];

                    health.health -= damage.damage;

                    shipHealthLookup[result.entityB] = health;
                }
            }
        }
    }
}

