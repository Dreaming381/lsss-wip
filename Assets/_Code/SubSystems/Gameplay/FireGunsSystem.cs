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
    public partial class FireGunsSystem : SubSystem
    {
        protected override void OnUpdate()
        {
            var bulletIcb           = latiosWorld.syncPoint.CreateInstantiateCommandBuffer<Rotation, Translation, BulletFirer>().AsParallelWriter();
            var effectIcbMainThread = latiosWorld.syncPoint.CreateInstantiateCommandBuffer<Parent>();
            effectIcbMainThread.AddComponentTag<LocalToParent>();
            var   effectIcb = effectIcbMainThread.AsParallelWriter();
            float dt        = SystemAPI.Time.DeltaTime;

            Entities.WithAll<ShipTag>().ForEach((Entity entity,
                                                 int entityInQueryIndex,
                                                 ref ShipReloadTime reloadTimes,
                                                 in ShipDesiredActions desiredActions,
                                                 in ShipBulletPrefab bulletPrefab,
                                                 in ShipFireEffectPrefab effectPrefab,
                                                 in DynamicBuffer<ShipGunPoint> gunPoints) =>
            {
                bool fire = reloadTimes.bulletsRemaining > 0 && reloadTimes.bulletReloadTime <= 0f && desiredActions.fire;
                if (fire)
                {
                    if (bulletPrefab.bulletPrefab != Entity.Null)
                    {
                        for (int i = 0; i < gunPoints.Length; i++)
                        {
                            CapsuleCollider collider   = GetComponent<Collider>(bulletPrefab.bulletPrefab);
                            float           halfLength = math.distance(collider.pointA, collider.pointB) / 2f + collider.radius;
                            var             ltw        = GetComponent<LocalToWorld>(gunPoints[i].gun);
                            var             rot        = quaternion.LookRotationSafe(ltw.Forward, ltw.Up);
                            bulletIcb.Add(bulletPrefab.bulletPrefab,
                                          new Rotation { Value     = rot },
                                          new Translation { Value  = ltw.Position + math.forward(rot) * halfLength },
                                          new BulletFirer { entity = entity, initialized = false },
                                          entityInQueryIndex);
                            if (effectPrefab.effectPrefab != Entity.Null)
                            {
                                effectIcb.Add(effectPrefab.effectPrefab, new Parent { Value = gunPoints[i].gun }, entityInQueryIndex);
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
            }).ScheduleParallel();

            //CompleteDependency();
            //EntityManager.CompleteAllJobs();
        }
    }
}

