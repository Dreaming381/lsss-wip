using Latios;
using Latios.Psyshock;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

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
            var bulletIcb           = latiosWorld.syncPoint.CreateInstantiateCommandBuffer<Rotation, Translation, BulletFirer>().AsParallelWriter();
            var effectIcbMainThread = latiosWorld.syncPoint.CreateInstantiateCommandBuffer<Parent>();
            effectIcbMainThread.AddComponentTag<LocalToParent>();
            var   effectIcb = effectIcbMainThread.AsParallelWriter();
            float dt        = Time.DeltaTime;

            var job = new Job
            {
                bulletIcb      = bulletIcb,
                effectIcb      = effectIcb,
                dt             = dt,
                ltwLookup      = GetComponentLookup<LocalToWorld>(true),
                colliderLookup = GetComponentLookup<Collider>(true)
            };
            job.ScheduleParallel();

            //CompleteDependency();
            //EntityManager.CompleteAllJobs();
        }

        [WithAll(typeof(ShipTag))]
        [BurstCompile]
        partial struct Job : IJobEntity
        {
            public InstantiateCommandBuffer<Rotation, Translation, BulletFirer>.ParallelWriter bulletIcb;
            public InstantiateCommandBuffer<Parent>.ParallelWriter                             effectIcb;
            public float                                                                       dt;

            [ReadOnly] public ComponentLookup<LocalToWorld> ltwLookup;
            [ReadOnly] public ComponentLookup<Collider>     colliderLookup;

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
                            CapsuleCollider collider   = colliderLookup[bulletPrefab.bulletPrefab];
                            float           halfLength = math.distance(collider.pointA, collider.pointB) / 2f + collider.radius;
                            var             ltw        = ltwLookup[gunPoints[i].gun];
                            var             rot        = quaternion.LookRotationSafe(ltw.Forward, ltw.Up);
                            bulletIcb.Add(bulletPrefab.bulletPrefab,
                                          new Rotation { Value     = rot },
                                          new Translation { Value  = ltw.Position + math.forward(rot) * halfLength },
                                          new BulletFirer { entity = entity, initialized = false },
                                          chunkIndexInQuery);
                            if (effectPrefab.effectPrefab != Entity.Null)
                            {
                                effectIcb.Add(effectPrefab.effectPrefab, new Parent { Value = gunPoints[i].gun }, chunkIndexInQuery);
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

