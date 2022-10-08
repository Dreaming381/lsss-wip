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
                bulletDamageLookup        = GetComponentLookup<Damage>(true),
                bulletFirerLookup         = GetComponentLookup<BulletFirer>(),
                shipHealthLookup          = GetComponentLookup<ShipHealth>(),
                shipHitEffectPrefabLookup = GetComponentLookup<ShipHitEffectPrefab>(true),
                dcb                       = dcb,
                icb                       = icb,
                frameId                   = frameId
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
            public PhysicsComponentLookup<ShipHealth>              shipHealthLookup;
            public PhysicsComponentLookup<BulletFirer>             bulletFirerLookup;
            [ReadOnly] public ComponentLookup<Damage>              bulletDamageLookup;
            [ReadOnly] public ComponentLookup<ShipHitEffectPrefab> shipHitEffectPrefabLookup;
            public int                                             frameId;

            public DestroyCommandBuffer.ParallelWriter                            dcb;
            public InstantiateCommandBuffer<Rotation, Translation>.ParallelWriter icb;

            public void Execute(in FindPairsResult result)
            {
                var bulletFirer = bulletFirerLookup[result.entityA];

                if (bulletFirer.entity == result.entityB)
                {
                    if (((bulletFirer.lastImpactFrame + 2) - frameId) > 0)
                    {
                        bulletFirer.lastImpactFrame       = frameId;
                        bulletFirerLookup[result.entityA] = bulletFirer;
                        return;
                    }
                }

                if (Physics.DistanceBetween(result.bodyA.collider, result.bodyA.transform, result.bodyB.collider, result.bodyB.transform, 0f, out var hitData))
                {
                    var damage = bulletDamageLookup[result.entityA];
                    var health = shipHealthLookup[result.entityB];

                    health.health -= damage.damage;

                    shipHealthLookup[result.entityB] = health;

                    dcb.Add(result.entityA, result.jobIndex);

                    var hitPrefab = shipHitEffectPrefabLookup[result.entityB];
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

