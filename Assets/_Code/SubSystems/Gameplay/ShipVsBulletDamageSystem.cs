using Latios;
using Latios.Psyshock;
using Latios.Transforms;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace Lsss
{
    [BurstCompile]
    public partial struct ShipVsBulletDamageSystem : ISystem, ILatiosApi
    {
        int m_frameId;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            this.OnCreateForLatios(ref state);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var api         = this.GetApi(ref state);
            var shipLayer   = api.sceneBlackboardEntity.GetCollectionComponent<ShipsCollisionLayer>(true).layer;
            var bulletLayer = api.sceneBlackboardEntity.GetCollectionComponent<BulletCollisionLayer>(true).layer;

            var icb = api.syncPoint.CreateInstantiateCommandBuffer<WorldTransformCommand>().AsParallelWriter();

            new BulletFirerJob { frameId = m_frameId, lastSystemVersion = state.LastSystemVersion }.ScheduleParallel();

            var processor = new DamageHitShipsAndDestroyBulletProcessor
            {
                icb     = icb,
                frameId = m_frameId
            }.Inject(api);

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
        partial struct DamageHitShipsAndDestroyBulletProcessor : IFindPairsProcessor, IInjectable
        {
            [Inject] PhysicsComponentLookup<ShipHealth>             shipHealthLookup;
            [Inject] PhysicsComponentLookup<BulletFirer>            bulletFirerLookup;
            [Inject] PhysicsComponentLookup<TimeToLive>             timeToLiveLookup;
            [ReadOnly, Inject] ComponentLookup<Damage>              bulletDamageLookup;
            [ReadOnly, Inject] ComponentLookup<ShipHitEffectPrefab> shipHitEffectPrefabLookup;
            public int                                              frameId;

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

