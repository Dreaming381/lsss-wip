using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Lsss.Authoring
{
    [DisallowMultipleComponent]
    [AddComponentMenu("LSSS/Objects/Spaceship")]
    public class SpaceshipAuthoring : MonoBehaviour
    {
        [Header("Speed")]
        public float topSpeed     = 1f;
        public float boostSpeed   = 2f;
        public float reverseSpeed = 0.5f;
        public float turnSpeed    = 60f;  //deg/s

        [Header("Acceleration")]
        public float acceleration      = 1f;
        public float deceleration      = 2f;
        public float boostAcceleration = 3f;

        [Header("Boost Tank")]
        public float boostCapacity      = 5f;
        public float boostDepletionRate = 1f;
        public float boostRechargeRate  = 0.25f;
        public float initialBoost       = 5f;

        [Header("Chassis")]
        public float              health                 = 100f;
        public float              collisionDamageToOther = 100f;
        public GameObject         cameraMountPoint;
        public ExplosionAuthoring explosionPrefab;
        public GameObject         hitEffectPrefab;
        public List<GameObject>   gunTips = new List<GameObject>();

        [Header("Bullets")]
        public float           fireRate       = 2f;
        public int             bulletsPerClip = 10;
        public float           clipReloadTime = 3f;
        public BulletAuthoring bulletPrefab;
        public GameObject      fireEffectPrefab;
    }

    public class SpaceshipBaker : Baker<SpaceshipAuthoring>
    {
        public override void Bake(SpaceshipAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new ShipSpeedStats
            {
                topSpeed     = authoring.topSpeed,
                boostSpeed   = authoring.boostSpeed,
                reverseSpeed = authoring.reverseSpeed,
                turnSpeed    = math.radians(authoring.turnSpeed),

                acceleration      = authoring.acceleration,
                deceleration      = authoring.deceleration,
                boostAcceleration = authoring.boostAcceleration,

                boostCapacity     = authoring.boostCapacity,
                boostDepleteRate  = authoring.boostDepletionRate,
                boostRechargeRate = authoring.boostRechargeRate
            });
            AddComponent(entity, new ShipBoostTank { boost = authoring.initialBoost });
            AddComponent(entity, new Speed { speed         = 0f });

            AddComponent(entity, new ShipHealth { health                   = authoring.health });
            AddComponent(entity, new ShipBaseHealth { baseHealth           = authoring.health });
            AddComponent(entity, new Damage { damage                       = authoring.collisionDamageToOther });
            AddComponent(entity, new CameraMountPoint { mountPoint         = GetEntity(authoring.cameraMountPoint, TransformUsageFlags.Renderable) });
            AddComponent(entity, new ShipExplosionPrefab { explosionPrefab = GetEntity(authoring.explosionPrefab, TransformUsageFlags.Dynamic) });
            AddComponent(entity, new ShipHitEffectPrefab { hitEffectPrefab = GetEntity(authoring.hitEffectPrefab, TransformUsageFlags.Dynamic) });
            var gunBuffer                                                  = AddBuffer<ShipGunPoint>(entity);
            foreach (var gunTip in authoring.gunTips)
            {
                gunBuffer.Add(new ShipGunPoint { gun = GetEntity(gunTip, TransformUsageFlags.Renderable) });
            }

            AddComponent(entity, new ShipReloadTime
            {
                bulletReloadTime    = 0f,
                maxBulletReloadTime = math.rcp(authoring.fireRate),
                bulletsRemaining    = authoring.bulletsPerClip,
                bulletsPerClip      = authoring.bulletsPerClip,
                clipReloadTime      = authoring.clipReloadTime,
                maxClipReloadTime   = authoring.clipReloadTime
            });
            AddComponent(                    entity, new ShipBulletPrefab { bulletPrefab     = GetEntity(authoring.bulletPrefab, TransformUsageFlags.Dynamic) });
            AddComponent(                    entity, new ShipFireEffectPrefab { effectPrefab = GetEntity(authoring.fireEffectPrefab, TransformUsageFlags.Dynamic) });

            AddComponent<ShipDesiredActions>(entity);
            AddComponent<ShipTag>(           entity);
        }
    }
}

