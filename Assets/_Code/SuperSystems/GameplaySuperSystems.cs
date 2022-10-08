using Latios;
using Unity.Transforms;

namespace Lsss.SuperSystems
{
    /// <summary>
    /// Handles spawning and other initialization work related to core gameplay.
    /// </summary>
    public class GameplaySyncPointSuperSystem : SuperSystem
    {
        protected override void CreateSystems()
        {
            GetOrCreateAndAddUnmanagedSystem<DestroyUninitializedOrphanedEffectsSystem>();
            GetOrCreateAndAddManagedSystem<OrbitalSpawnersProcGenSystem>();
            GetOrCreateAndAddUnmanagedSystem<SpawnFleetsSystem>();
            GetOrCreateAndAddManagedSystem<SpawnShipsEnqueueSystem>();
            GetOrCreateAndAddUnmanagedSystem<SpawnShipsEnableSystem>();
        }
    }

    /// <summary>
    /// Updates the motion simulation after the player and AI have made decisions.
    /// </summary>
    public class AdvanceGameplayMotionSuperSystem : SuperSystem
    {
        protected override void CreateSystems()
        {
            GetOrCreateAndAddUnmanagedSystem<MoveShipsSystem>();
            GetOrCreateAndAddUnmanagedSystem<MoveBulletsSystem>();
            GetOrCreateAndAddUnmanagedSystem<ExpandExplosionsSystem>();
            GetOrCreateAndAddUnmanagedSystem<MoveOrbitalSpawnPointsSystem>();
        }
    }

    /// <summary>
    /// Updates spatial query data structures and other metadata for future systems to use.
    /// </summary>
    public class UpdateTransformSpatialQueriesSuperSystem : SuperSystem
    {
        protected override void CreateSystems()
        {
            GetOrCreateAndAddManagedSystem<BuildFactionShipsCollisionLayersSystem>();
            GetOrCreateAndAddManagedSystem<BuildBulletsCollisionLayerSystem>();
            GetOrCreateAndAddManagedSystem<BuildExplosionsCollisionLayerSystem>();
            GetOrCreateAndAddManagedSystem<BuildWallsCollisionLayerSystem>();
            //GetOrCreateAndAddManagedSystem<BuildWormholesCollisionLayerSystem>();
            GetOrCreateAndAddManagedSystem<BuildSpawnPointCollisionLayerSystem>();

            //GetOrCreateAndAddSystem<DebugDrawFactionShipsCollisionLayersSystem>();
            //GetOrCreateAndAddSystem<DebugDrawBulletCollisionLayersSystem>();
            //GetOrCreateAndAddSystem<DebugDrawWormholeCollisionLayersSystem>();
            //GetOrCreateAndAddSystem<DebugDrawSpawnPointCollisionLayersSystem>();
        }
    }

    /// <summary>
    /// Reacts to the latest transform updates and handles core gameplay logic
    /// </summary>
    public class ProcessGameplayEventsSuperSystem : SuperSystem
    {
        protected override void CreateSystems()
        {
            GetOrCreateAndAddManagedSystem<ShipVsBulletDamageSystem>();
            GetOrCreateAndAddManagedSystem<ShipVsShipDamageSystem>();
            GetOrCreateAndAddManagedSystem<ShipVsExplosionDamageSystem>();
            //GetOrCreateAndAddManagedSystem<ShipVsWallDamageSystem>();
            //GetOrCreateAndAddSystem<BulletVsWallSystem>();
            GetOrCreateAndAddManagedSystem<CheckSpawnPointIsSafeSystem>();
            //GetOrCreateAndAddSystem<TravelThroughWormholeSystem>();
            GetOrCreateAndAddManagedSystem<UpdateTimeToLiveSystem>();
            GetOrCreateAndAddManagedSystem<DestroyShipsWithNoHealthSystem>();
            GetOrCreateAndAddManagedSystem<SpawnShipsPrioritizeSystem>();
            GetOrCreateAndAddManagedSystem<SpawnShipsDequeueSystem>();
            GetOrCreateAndAddManagedSystem<EvaluateMissionSystem>();
            GetOrCreateAndAddManagedSystem<FireGunsSystem>();
        }
    }
}

