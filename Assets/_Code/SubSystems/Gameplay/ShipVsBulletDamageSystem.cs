using Latios;
using Latios.Psyshock;
using Latios.Transforms;
using Unity.Burst;
using Unity.Burst.Intrinsics;
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
        public void OnUpdate(ref SystemState state)
        {
            var shipLayer   = latiosWorld.sceneBlackboardEntity.GetCollectionComponent<ShipsCollisionLayer>(true).layer;
            var bulletLayer = latiosWorld.sceneBlackboardEntity.GetCollectionComponent<BulletCollisionLayer>(true).layer;

            var icb = latiosWorld.syncPoint.CreateInstantiateCommandBuffer<WorldTransformCommand>().AsParallelWriter();

            new BulletFirerJob { frameId = m_frameId, lastSystemVersion = state.LastSystemVersion }.ScheduleParallel();

            var processor = new DamageHitShipsAndDestroyBulletProcessor
            {
                bulletDamageLookup        = GetComponentLookup<Damage>(true),
                bulletFirerLookup         = GetComponentLookup<BulletFirer>(),
                timeToLiveLookup          = GetComponentLookup<TimeToLive>(false),
                shipHealthLookup          = GetComponentLookup<ShipHealth>(),
                shipHitEffectPrefabLookup = GetComponentLookup<ShipHitEffectPrefab>(true),
                icb                       = icb,
                frameId                   = m_frameId
            };

            state.Dependency = Physics.FindPairs(bulletLayer, shipLayer, processor).ScheduleParallel(state.Dependency);

            m_frameId++;
        }

        [BurstCompile]
        partial struct BulletFirerJob : IJobEntity, IJobEntityChunkBeginEnd
        {
            public int  frameId;
            public uint lastSystemVersion;

            public void Execute(ref BulletFirer firer)
            {
                if (!firer.initialized)
                {
                    firer.lastImpactFrame = frameId;
                    firer.initialized     = true;
                }
            }

            public bool OnChunkBegin(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                return chunk.DidOrderChange(lastSystemVersion);
            }

            public void OnChunkEnd(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask, bool chunkWasExecuted)
            {
            }
        }

        //Assumes A is bullet and B is ship.
        struct DamageHitShipsAndDestroyBulletProcessor : IFindPairsProcessor
        {
            public PhysicsComponentLookup<ShipHealth>              shipHealthLookup;
            public PhysicsComponentLookup<BulletFirer>             bulletFirerLookup;
            public PhysicsComponentLookup<TimeToLive>              timeToLiveLookup;
            [ReadOnly] public ComponentLookup<Damage>              bulletDamageLookup;
            [ReadOnly] public ComponentLookup<ShipHitEffectPrefab> shipHitEffectPrefabLookup;
            public int                                             frameId;

            public InstantiateCommandBufferCommand1<WorldTransformCommand>.ParallelWriter icb;

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

                if (Physics.DistanceBetween(result.colliderA, result.transformA, result.colliderB, result.transformB, 0f, out var hitData))
                {
                    var     damage = bulletDamageLookup[result.entityA];
                    ref var health = ref shipHealthLookup.GetRW(result.entityB).ValueRW;

                    health.health -= damage.damage;

                    timeToLiveLookup.GetRW(result.entityA).ValueRW.timeToLive = 0f;

                    var hitPrefab = shipHitEffectPrefabLookup[result.entityB];
                    if (hitPrefab.hitEffectPrefab != Entity.Null)
                    {
                        float3 upDir =
                            math.select(math.up(), math.forward(), math.abs(hitData.normalB.y) == 1f);
                        var rotation                                                                     = quaternion.LookRotationSafe(hitData.normalB, upDir);
                        icb.Add(hitPrefab.hitEffectPrefab, new WorldTransformCommand { newWorldTransform = new TransformQvvs( hitData.hitpointB, rotation) }, result.jobIndex);
                    }
                }
            }
        }
    }
}

