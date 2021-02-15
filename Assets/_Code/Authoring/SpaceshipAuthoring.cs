using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Lsss.Authoring
{
    [DisallowMultipleComponent]
    [AddComponentMenu("LSSS/Objects/Spaceship")]
    public class SpaceshipAuthoring : MonoBehaviour, IDeclareReferencedPrefabs, IConvertGameObjectToEntity
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

        public void DeclareReferencedPrefabs(List<GameObject> referencedPrefabs)
        {
            if (explosionPrefab != null)
                referencedPrefabs.Add(explosionPrefab.gameObject);
            if (hitEffectPrefab != null)
                referencedPrefabs.Add(hitEffectPrefab);
            if (bulletPrefab != null)
                referencedPrefabs.Add(bulletPrefab.gameObject);
            if (fireEffectPrefab != null)
                referencedPrefabs.Add(fireEffectPrefab);
        }

        public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
        {
            dstManager.AddComponentData(entity, new ShipSpeedStats
            {
                topSpeed     = topSpeed,
                boostSpeed   = boostSpeed,
                reverseSpeed = reverseSpeed,
                turnSpeed    = math.radians(turnSpeed),

                acceleration      = acceleration,
                deceleration      = deceleration,
                boostAcceleration = boostAcceleration,

                boostCapacity     = boostCapacity,
                boostDepleteRate  = boostDepletionRate,
                boostRechargeRate = boostRechargeRate
            });
            dstManager.AddComponentData(entity, new ShipBoostTank { boost = initialBoost });
            dstManager.AddComponentData(entity, new Speed { speed         = 0f });

            dstManager.AddComponentData(entity, new ShipHealth { health                   = health });
            dstManager.AddComponentData(entity, new ShipBaseHealth { baseHealth           = health });
            dstManager.AddComponentData(entity, new Damage { damage                       = collisionDamageToOther });
            dstManager.AddComponentData(entity, new CameraMountPoint { mountPoint         = conversionSystem.TryGetPrimaryEntity(cameraMountPoint) });
            dstManager.AddComponentData(entity, new ShipExplosionPrefab { explosionPrefab = conversionSystem.TryGetPrimaryEntity(explosionPrefab) });
            dstManager.AddComponentData(entity, new ShipHitEffectPrefab { hitEffectPrefab = conversionSystem.TryGetPrimaryEntity(hitEffectPrefab) });
            var gunBuffer                                                                 = dstManager.AddBuffer<ShipGunPoint>(entity);
            foreach(var gunTip in gunTips)
            {
                gunBuffer.Add(new ShipGunPoint { gun = conversionSystem.GetPrimaryEntity(gunTip) });
            }

            dstManager.AddComponentData(entity, new ShipReloadTime
            {
                bulletReloadTime    = 0f,
                maxBulletReloadTime = math.rcp(fireRate),
                bulletsRemaining    = bulletsPerClip,
                bulletsPerClip      = bulletsPerClip,
                clipReloadTime      = clipReloadTime,
                maxClipReloadTime   = clipReloadTime
            });
            dstManager.AddComponentData(entity, new ShipBulletPrefab { bulletPrefab     = conversionSystem.TryGetPrimaryEntity(bulletPrefab) });
            dstManager.AddComponentData(entity, new ShipFireEffectPrefab { effectPrefab = conversionSystem.TryGetPrimaryEntity(fireEffectPrefab) });

            dstManager.AddComponent<ShipDesiredActions>(entity);
            dstManager.AddComponent<ShipTag>(           entity);
        }
    }
}

