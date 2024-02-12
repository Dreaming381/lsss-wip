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
    public partial struct ShipVsBulletDamageSystem : ISystem
    {
        int m_frameId;

        LatiosWorldUnmanaged latiosWorld;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            latiosWorld = state.GetLatiosWorldUnmanaged();
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var shipLayer   = latiosWorld.sceneBlackboardEntity.GetCollectionComponent<ShipsCollisionLayer>(true).layer;
            var bulletLayer = latiosWorld.sceneBlackboardEntity.GetCollectionComponent<BulletCollisionLayer>(true).layer;

            var dcb = latiosWorld.syncPoint.CreateDestroyCommandBuffer().AsParallelWriter();
            var icb = latiosWorld.syncPoint.CreateInstantiateCommandBuffer<WorldTransform>().AsParallelWriter();

            new BulletFirerJob { frameId = m_frameId }.ScheduleParallel();

            var processor = new DamageHitShipsAndDestroyBulletProcessor
            {
                bulletDamageLookup        = GetComponentLookup<Damage>(true),
                bulletFirerLookup         = GetComponentLookup<BulletFirer>(),
                shipHealthLookup          = GetComponentLookup<ShipHealth>(),
                shipHitEffectPrefabLookup = GetComponentLookup<ShipHitEffectPrefab>(true),
                dcb                       = dcb,
                icb                       = icb,
                frameId                   = m_frameId
            };

            state.Dependency = Physics.FindPairs(bulletLayer, shipLayer, processor).ScheduleParallel(state.Dependency);

            m_frameId++;
        }

        [BurstCompile]
        partial struct BulletFirerJob : IJobEntity
        {
            public int frameId;

            public void Execute(ref BulletFirer firer)
            {
                if (!firer.initialized)
                {
                    firer.lastImpactFrame = frameId;
                    firer.initialized     = true;
                }
            }
        }

        //Assumes A is bullet and B is ship.
        struct DamageHitShipsAndDestroyBulletProcessor : IFindPairsProcessor
        {
            public PhysicsComponentLookup<ShipHealth>              shipHealthLookup;
            public PhysicsComponentLookup<BulletFirer>             bulletFirerLookup;
            [ReadOnly] public ComponentLookup<Damage>              bulletDamageLookup;
            [ReadOnly] public ComponentLookup<ShipHitEffectPrefab> shipHitEffectPrefabLookup;
            public int                                             frameId;

            public DestroyCommandBuffer.ParallelWriter                     dcb;
            public InstantiateCommandBuffer<WorldTransform>.ParallelWriter icb;

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
                        float3 upDir                                                           = math.select(math.up(), math.forward(), math.abs(hitData.normalB.y) == 1f);
                        var    rotation                                                        = quaternion.LookRotationSafe(hitData.normalB, upDir);
                        icb.Add(hitPrefab.hitEffectPrefab, new WorldTransform { worldTransform = new TransformQvvs( hitData.hitpointB, rotation) }, result.jobIndex);
                    }
                }
            }
        }
    }
}

