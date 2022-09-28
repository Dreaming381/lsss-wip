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
    public partial class ShipVsBulletDamageSystem : SubSystem
    {
        int m_frameId = 0;

        protected override void OnUpdate()
        {
            var bulletLayer = sceneBlackboardEntity.GetCollectionComponent<BulletCollisionLayer>(true).layer;

            var dcb = latiosWorld.syncPoint.CreateDestroyCommandBuffer().AsParallelWriter();
            var icb = latiosWorld.syncPoint.CreateInstantiateCommandBuffer<Rotation, Translation>().AsParallelWriter();

            int frameId = m_frameId;

            Entities.ForEach((ref BulletFirer firer) =>
            {
                if (!firer.initialized)
                {
                    firer.lastImpactFrame = frameId;
                    firer.initialized     = true;
                }
            }).ScheduleParallel();

            var processor = new DamageHitShipsAndDestroyBulletProcessor
            {
                bulletDamageCdfe        = GetComponentLookup<Damage>(true),
                bulletFirerCdfe         = GetComponentLookup<BulletFirer>(),
                shipHealthCdfe          = GetComponentLookup<ShipHealth>(),
                shipHitEffectPrefabCdfe = GetComponentLookup<ShipHitEffectPrefab>(true),
                dcb                     = dcb,
                icb                     = icb,
                frameId                 = frameId
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
            public PhysicsComponentLookup<ShipHealth>              shipHealthCdfe;
            public PhysicsComponentLookup<BulletFirer>             bulletFirerCdfe;
            [ReadOnly] public ComponentLookup<Damage>              bulletDamageCdfe;
            [ReadOnly] public ComponentLookup<ShipHitEffectPrefab> shipHitEffectPrefabCdfe;
            public int                                             frameId;

            public DestroyCommandBuffer.ParallelWriter                            dcb;
            public InstantiateCommandBuffer<Rotation, Translation>.ParallelWriter icb;

            public void Execute(in FindPairsResult result)
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

                if (Physics.DistanceBetween(result.bodyA.collider, result.bodyA.transform, result.bodyB.collider, result.bodyB.transform, 0f, out var hitData))
                {
                    var damage = bulletDamageCdfe[result.entityA];
                    var health = shipHealthCdfe[result.entityB];

                    health.health -= damage.damage;

                    shipHealthCdfe[result.entityB] = health;

                    dcb.Add(result.entityA, result.jobIndex);

                    var hitPrefab = shipHitEffectPrefabCdfe[result.entityB];
                    if (hitPrefab.hitEffectPrefab != Entity.Null)
                    {
                        float3 upDir                                                         = math.select(math.up(), math.forward(), math.abs(hitData.normalB.y) == 1f);
                        var    rotation                                                      = new Rotation { Value = quaternion.LookRotationSafe(hitData.normalB, upDir) };
                        icb.Add(hitPrefab.hitEffectPrefab, rotation, new Translation { Value = hitData.hitpointB }, result.jobIndex);
                    }
                }
            }
        }
    }
}

