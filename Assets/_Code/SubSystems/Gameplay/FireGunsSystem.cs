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
    public partial struct FireGunsSystem : ISystem
    {
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
            var   bulletIcb = latiosWorld.syncPoint.CreateInstantiateCommandBuffer<WorldTransform, BulletFirer>().AsParallelWriter();
            var   effectIcb = latiosWorld.syncPoint.CreateInstantiateCommandBuffer<Parent>().AsParallelWriter();
            float dt        = Time.DeltaTime;

            var job = new Job
            {
                bulletIcb            = bulletIcb,
                effectIcb            = effectIcb,
                dt                   = dt,
                worldTransformLookup = GetComponentLookup<WorldTransform>(true),
                colliderLookup       = GetComponentLookup<Collider>(true)
            };
            job.ScheduleParallel();

            //CompleteDependency();
            //EntityManager.CompleteAllJobs();
        }

        [WithAll(typeof(ShipTag))]
        [BurstCompile]
        partial struct Job : IJobEntity
        {
            public InstantiateCommandBuffer<WorldTransform, BulletFirer>.ParallelWriter bulletIcb;
            public InstantiateCommandBuffer<Parent>.ParallelWriter                      effectIcb;
            public float                                                                dt;

            [ReadOnly] public ComponentLookup<WorldTransform> worldTransformLookup;
            [ReadOnly] public ComponentLookup<Collider>       colliderLookup;

            public void Execute(Entity entity,
                                [ChunkIndexInQuery] int chunkIndexInQuery,
                                ref ShipReloadTime reloadTimes,
                                in ShipDesiredActions desiredActions,
                                in ShipBulletPrefab bulletPrefab,
                                in ShipFireEffectPrefab effectPrefab,
                                in DynamicBuffer<ShipGunPoint> gunPoints)
            {
                bool fire = reloadTimes.bulletsRemaining > 0 && reloadTimes.bulletReloadTime <= 0f && desiredActions.fire;
                if (fire)
                {
                    if (bulletPrefab.bulletPrefab != Entity.Null)
                    {
                        for (int i = 0; i < gunPoints.Length; i++)
                        {
                            CapsuleCollider collider                   = colliderLookup[bulletPrefab.bulletPrefab];
                            float           halfLength                 = math.distance(collider.pointA, collider.pointB) / 2f + collider.radius;
                            var             gunPointTransform          = worldTransformLookup[gunPoints[i].gun];
                            gunPointTransform.worldTransform.position += gunPointTransform.forwardDirection * halfLength;
                            bulletIcb.Add(bulletPrefab.bulletPrefab,
                                          gunPointTransform,
                                          new BulletFirer { entity = entity, initialized = false },
                                          chunkIndexInQuery);
                            if (effectPrefab.effectPrefab != Entity.Null)
                            {
                                effectIcb.Add(effectPrefab.effectPrefab, new Parent { parent = gunPoints[i].gun }, chunkIndexInQuery);
                            }
                        }
                    }

                    reloadTimes.bulletsRemaining--;
                    reloadTimes.bulletReloadTime = reloadTimes.maxBulletReloadTime;
                    reloadTimes.clipReloadTime   = reloadTimes.maxClipReloadTime;
                }
                else
                {
                    reloadTimes.bulletReloadTime = math.max(0f, reloadTimes.bulletReloadTime - dt);
                    reloadTimes.clipReloadTime   = math.max(0f, reloadTimes.clipReloadTime - dt);
                    bool reloadClip              = reloadTimes.clipReloadTime <= 0f;
                    reloadTimes.bulletsRemaining = math.select(reloadTimes.bulletsRemaining, reloadTimes.bulletsPerClip, reloadClip);
                }
            }
        }
    }
}

