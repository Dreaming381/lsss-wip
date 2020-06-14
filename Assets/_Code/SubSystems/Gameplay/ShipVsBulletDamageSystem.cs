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
    public class ShipVsBulletDamageSystem : SubSystem
    {
        BeginInitializationEntityCommandBufferSystem m_ecbSystem;

        protected override void OnCreate()
        {
            m_ecbSystem = World.GetExistingSystem<BeginInitializationEntityCommandBufferSystem>();
        }

        protected override void OnUpdate()
        {
            var ecbPackage = m_ecbSystem.CreateCommandBuffer();
            var ecb        = ecbPackage.ToConcurrent();

            var bulletLayer = sceneGlobalEntity.GetCollectionComponent<BulletCollisionLayer>(true).layer;

            var processor = new DamageHitShipsAndDestroyBulletProcessor
            {
                bulletDamageCdfe = GetComponentDataFromEntity<Damage>(true),
                shipHealthCdfe   = this.GetPhysicsComponentDataFromEntity<ShipHealth>(),
                ecb              = ecb
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

            m_ecbSystem.AddJobHandleForProducer(Dependency);
        }

        //Assumes A is bullet and B is ship.
        struct DamageHitShipsAndDestroyBulletProcessor : IFindPairsProcessor
        {
            public PhysicsComponentDataFromEntity<ShipHealth> shipHealthCdfe;
            [ReadOnly] public ComponentDataFromEntity<Damage> bulletDamageCdfe;

            public EntityCommandBuffer.Concurrent ecb;

            public void Execute(FindPairsResult result)
            {
                if (Physics.DistanceBetween(result.bodyA.collider, result.bodyA.transform, result.bodyB.collider, result.bodyB.transform, 0f, out _))
                {
                    var damage = bulletDamageCdfe[result.entityA];
                    var health = shipHealthCdfe[result.entityB];

                    health.health -= damage.damage;

                    shipHealthCdfe[result.entityB] = health;

                    ecb.DestroyEntity(result.jobIndex, result.entityA);
                }
            }
        }
    }
}

