using System;
using Latios;
using Latios.Psyshock;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace Lsss
{
    public struct ArenaTag : IComponentData { }

    public struct ArenaRadius : IComponentData
    {
        public float radius;
    }

    public struct ArenaCollisionSettings : IComponentData
    {
        public CollisionLayerSettings settings;
    }

    public struct FactionTag : IComponentData { }

    public struct Faction : IComponentData
    {
        public FixedString64Bytes name;
        public int                remainingReinforcements;
        public int                maxFieldUnits;
        public float              spawnWeightInverse;
        public EntityWith<Prefab> aiPrefab;
        public EntityWith<Prefab> playerPrefab;
    }

    public struct FactionShipsCollisionLayer : ICollectionComponent
    {
        public CollisionLayer layer;

        public Type AssociatedComponentType => typeof(FactionTag);

        public JobHandle Dispose(JobHandle inputDeps) => layer.Dispose(inputDeps);
    }

    public struct FactionMember : ISharedComponentData
    {
        public EntityWith<Faction> factionEntity;
    }

    public struct LastAliveObjectiveTag : IComponentData { }

    public struct DestroyFactionObjective : IBufferElementData
    {
        public EntityWith<Faction> factionToDestroy;
    }

    public struct ShipTag : IComponentData { }

    //Todo: Blob this if chunk utilization gets low
    public struct ShipSpeedStats : IComponentData
    {
        public float topSpeed;
        public float boostSpeed;
        public float reverseSpeed;
        public float turnSpeed;  //rad/s

        public float acceleration;
        public float deceleration;
        public float boostAcceleration;

        public float boostCapacity;
        public float boostDepleteRate;
        public float boostRechargeRate;
    }

    public struct ShipHealth : IComponentData
    {
        public float health;
    }

    public struct ShipBaseHealth : IComponentData
    {
        public float baseHealth;
    }

    [InternalBufferCapacity(8)]
    public struct ShipGunPoint : IBufferElementData
    {
        public EntityWith<LocalToWorld> gun;
    }

    public struct ShipBulletPrefab : IComponentData
    {
        public EntityWith<Prefab> bulletPrefab;
    }

    public struct ShipFireEffectPrefab : IComponentData
    {
        public EntityWith<Prefab> effectPrefab;
    }

    public struct ShipExplosionPrefab : IComponentData
    {
        public EntityWith<Prefab> explosionPrefab;
    }

    public struct ShipHitEffectPrefab : IComponentData
    {
        public EntityWith<Prefab> hitEffectPrefab;
    }

    public struct ShipReloadTime : IComponentData
    {
        public float bulletReloadTime;
        public float maxBulletReloadTime;
        public int   bulletsRemaining;
        public int   bulletsPerClip;
        public float clipReloadTime;
        public float maxClipReloadTime;
    }

    public struct ShipDesiredActions : IComponentData
    {
        public float  gas;  //-1 to 1
        public float2 turn;  //-1 to 1 per axis
        public bool   fire;
        public bool   boost;
    }

    public struct ShipBoostTank : IComponentData
    {
        public float boost;
    }

    public struct BulletTag : IComponentData { }

    //Partial CCD hack
    public struct BulletPreviousPosition : IComponentData
    {
        public float3 previousPosition;
    }

    public struct BulletFirer : IComponentData
    {
        // Todo: Change this after upgrade to 2020.3.
        //public EntityWith<ShipTag> entity;
        public Entity entity;
        public int    lastImpactFrame;
        public bool   initialized;
    }

    //Todo: Replace with Spherecast
    public struct BulletCollisionLayer : ICollectionComponent
    {
        public CollisionLayer layer;

        public Type AssociatedComponentType => typeof(BulletCollisionLayerTag);

        public JobHandle Dispose(JobHandle inputDeps)
        {
            return layer.Dispose(inputDeps);
        }
    }

    public struct BulletCollisionLayerTag : IComponentData { }

    public struct ExplosionTag : IComponentData { }

    public struct ExplosionStats : IComponentData
    {
        public float radius;
        public float expansionRate;
    }

    public struct ExplosionCollisionLayer : ICollectionComponent
    {
        public CollisionLayer layer;

        public Type AssociatedComponentType => typeof(ExplosionCollisionLayerTag);

        public JobHandle Dispose(JobHandle inputDeps) => layer.Dispose(inputDeps);
    }

    public struct ExplosionCollisionLayerTag : IComponentData { }

    public struct WormholeTag : IComponentData { }

    public struct WormholeDestination : IComponentData
    {
        public EntityWith<WormholeDestination> wormholeDestination;
    }

    public struct WormholeCollisionLayer : ICollectionComponent
    {
        public CollisionLayer layer;

        public Type AssociatedComponentType => typeof(WormholeCollisionLayerTag);

        public JobHandle Dispose(JobHandle inputDeps) => layer.Dispose(inputDeps);
    }

    public struct WormholeCollisionLayerTag : IComponentData { }

    public struct WallTag : IComponentData { }

    public struct WallCollisionLayer : ICollectionComponent
    {
        public CollisionLayer layer;

        public Type AssociatedComponentType => typeof(WallCollisionLayerTag);

        public JobHandle Dispose(JobHandle inputDeps) => layer.Dispose(inputDeps);
    }

    public struct WallCollisionLayerTag : IComponentData { }

    public struct SpawnPointTag : IComponentData { }

    public struct SpawnPoint : IComponentData
    {
        public EntityWith<Prefab> spawnGraphicPrefab;
        public float              maxTimeUntilSpawn;
        public float              maxPauseTime;
    }

    public struct SpawnPayload : IComponentData
    {
        public EntityWith<Disabled> disabledShip;
    }

    public struct SafeToSpawn : IComponentData
    {
        public bool safe;
    }

    public struct SpawnPointOrbitalPath : IComponentData
    {
        public float3 center;
        public float  radius;
        public float3 orbitPlaneNormal;
        public float  orbitSpeed;  //rad/s
    }

    public struct SpawnTimes : IComponentData
    {
        public float enableTime;
        public float pauseTime;
    }

    public struct SpawnPointCollisionLayer : ICollectionComponent
    {
        public CollisionLayer layer;

        public Type AssociatedComponentType => typeof(SpawnPointCollisionLayerTag);

        public JobHandle Dispose(JobHandle inputDeps) => layer.Dispose(inputDeps);
    }

    public struct SpawnPointCollisionLayerTag : IComponentData { }

    public struct SpawnQueues : ICollectionComponent
    {
        public struct FactionRanges
        {
            public int   start;
            public int   count;
            public float weight;
        }

        public NativeQueue<EntityWith<Disabled> > playerQueue;
        public NativeQueue<EntityWith<Disabled> > aiQueue;
        public NativeList<EntityWith<Disabled> >  newAiEntitiesToPrioritize;
        public NativeList<FactionRanges>          factionRanges;

        public Type AssociatedComponentType => typeof(SpawnQueuesTag);

        public unsafe JobHandle Dispose(JobHandle inputDeps)
        {
            var jh = stackalloc[] {
                playerQueue.Dispose(inputDeps),
                aiQueue.Dispose(inputDeps),
                newAiEntitiesToPrioritize.Dispose(inputDeps),
                factionRanges.Dispose(inputDeps)
            };
            return Unity.Jobs.LowLevel.Unsafe.JobHandleUnsafeUtility.CombineDependencies(jh, 4);
        }
    }

    public struct SpawnQueuesTag : IComponentData { }

    public struct FleetSpawnPointTag : IComponentData { }

    public struct FleetSpawnSlotTag : IComponentData { }

    public struct FleetSpawnPlayerSlotTag : IComponentData { }

    public struct FleetSpawnSlotFactionReference : IComponentData
    {
        public EntityWith<Faction> factionEntity;
    }

    public struct OrbitalSpawnPointProcGen : IComponentData
    {
        public int    spawnerCount;
        public uint   randomSeed;
        public float  minRadius;
        public float2 minMaxOrbitSpeed;
        public float  colliderRadius;

        public EntityWith<Prefab> spawnGraphicPrefab;
        public float              maxTimeUntilSpawn;
        public float              maxPauseTime;
    }

    //Shared
    public struct Speed : IComponentData
    {
        public float speed;  //Signed
    }

    public struct TimeToLive : IComponentData
    {
        public float timeToLive;
    }

    //Dishes out
    public struct Damage : IComponentData
    {
        public float damage;
    }

    //Singletons
    public struct SceneCollisionSettings : IComponentData
    {
        public CollisionLayerSettings settings;
    }
}

