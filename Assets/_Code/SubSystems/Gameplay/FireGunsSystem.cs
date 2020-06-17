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
    public class FireGunsSystem : SubSystem
    {
        BeginInitializationEntityCommandBufferSystem m_ecbSystem;

        protected override void OnCreate()
        {
            m_ecbSystem = World.GetExistingSystem<BeginInitializationEntityCommandBufferSystem>();
        }

        protected override void OnUpdate()
        {
            var   ecbPackage = m_ecbSystem.CreateCommandBuffer();
            var   ecb        = ecbPackage.ToConcurrent();
            float dt         = Time.DeltaTime;

            Entities.WithAll<ShipTag>().ForEach((int entityInQueryIndex,
                                                 ref ShipReloadTime reloadTimes,
                                                 in ShipDesiredActions desiredActions,
                                                 in ShipBulletPrefab bulletPrefab,
                                                 in DynamicBuffer<ShipGunPoint> gunPoints) =>
            {
                bool fire = reloadTimes.bulletsRemaining > 0 && reloadTimes.bulletReloadTime <= 0f && desiredActions.fire;
                if (fire)
                {
                    for (int i = 0; i < gunPoints.Length; i++)
                    {
                        var             bullet                                               = ecb.Instantiate(entityInQueryIndex, bulletPrefab.bulletPrefab);
                        CapsuleCollider collider                                             = GetComponent<Collider>(bulletPrefab.bulletPrefab);
                        float           halfLength                                           = math.distance(collider.pointA, collider.pointB) / 2f + collider.radius;
                        var             ltw                                                  = GetComponent<LocalToWorld>(gunPoints[i].gun);
                        var             rot                                                  = quaternion.LookRotationSafe(ltw.Forward, ltw.Up);
                        ecb.SetComponent(entityInQueryIndex, bullet, new Rotation { Value    = rot });
                        ecb.SetComponent(entityInQueryIndex, bullet, new Translation { Value = ltw.Position + math.forward(rot) * halfLength});
                        //ecb.SetComponent(entityInQueryIndex, bullet, new BulletPreviousPosition { previousPosition = ltw.Position }); //Shouldn't be necessary?
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

            m_ecbSystem.AddJobHandleForProducer(Dependency);
        }
    }
}

