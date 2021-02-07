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
    public class ShipVsBulletDamageSystem : SubSystem
    {
        int m_frameId = 0;

        protected override void OnUpdate()
        {
            var dcb = latiosWorld.syncPoint.CreateDestroyCommandBuffer().AsParallelWriter();

            int frameId = m_frameId;

            Entities.ForEach((ref BulletFirer firer) =>
            {
                if (!firer.initialized)
                {
                    firer.lastImpactFrame = frameId;
                    firer.initialized     = true;
                }
            }).ScheduleParallel();

            var bulletLayer = sceneBlackboardEntity.GetCollectionComponent<BulletCollisionLayer>(true).layer;

            var processor = new DamageHitShipsAndDestroyBulletProcessor
            {
                bulletDamageCdfe = GetComponentDataFromEntity<Damage>(true),
                bulletFirerCdfe  = GetComponentDataFromEntity<BulletFirer>(),
                shipHealthCdfe   = GetComponentDataFromEntity<ShipHealth>(),
                dcb              = dcb,
                frameId          = frameId
            };

            var backup = Dependency;
            Dependency = default;

            Entities.WithAll<FactionTag>().ForEach((Entity entity, int entityInQueryIndex) =>
            {
                if (entityInQueryIndex == 0)
                    Dependency = backup;

                var shipLayer = EntityManager.GetCollectionComponent<FactionShipsCollisionLayer>(entity, true).layer;
                Dependency    = Physics.FindPairs(bulletLayer, shipLayer, processor).ScheduleParallel(Dependency);
            }).WithoutBurst().Run();

            m_frameId++;
        }

        //Assumes A is bullet and B is ship.
        struct DamageHitShipsAndDestroyBulletProcessor : IFindPairsProcessor
        {
            public PhysicsComponentDataFromEntity<ShipHealth>  shipHealthCdfe;
            public PhysicsComponentDataFromEntity<BulletFirer> bulletFirerCdfe;
            [ReadOnly] public ComponentDataFromEntity<Damage>  bulletDamageCdfe;
            public int                                         frameId;

            public DestroyCommandBuffer.ParallelWriter dcb;

            public void Execute(FindPairsResult result)
            {
                var bulletFirer = bulletFirerCdfe[result.entityA];

                if (bulletFirer.entity == result.entityB)
                {
                    if (((bulletFirer.lastImpactFrame + 2) - frameId) > 0)
                    {
                        bulletFirer.lastImpactFrame     = frameId;
                        bulletFirerCdfe[result.entityA] = bulletFirer;
                        return;
                    }
                }

                if (Physics.DistanceBetween(result.bodyA.collider, result.bodyA.transform, result.bodyB.collider, result.bodyB.transform, 0f, out _))
                {
                    var damage = bulletDamageCdfe[result.entityA];
                    var health = shipHealthCdfe[result.entityB];

                    health.health -= damage.damage;

                    shipHealthCdfe[result.entityB] = health;

                    dcb.Add(result.entityA, result.jobIndex);
                }
            }
        }
    }
}

