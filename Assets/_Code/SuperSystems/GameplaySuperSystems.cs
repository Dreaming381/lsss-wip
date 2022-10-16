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
            GetOrCreateAndAddUnmanagedSystem<OrbitalSpawnersProcGenSystem>();
            GetOrCreateAndAddUnmanagedSystem<SpawnFleetsSystem>();
            GetOrCreateAndAddUnmanagedSystem<SpawnShipsEnqueueSystem>();
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
            GetOrCreateAndAddUnmanagedSystem<BuildFactionShipsCollisionLayersSystem>();
            GetOrCreateAndAddUnmanagedSystem<BuildBulletsCollisionLayerSystem>();
            GetOrCreateAndAddUnmanagedSystem<BuildExplosionsCollisionLayerSystem>();
            GetOrCreateAndAddUnmanagedSystem<BuildWallsCollisionLayerSystem>();
            GetOrCreateAndAddUnmanagedSystem<BuildWormholesCollisionLayerSystem>();
            GetOrCreateAndAddUnmanagedSystem<BuildSpawnPointCollisionLayerSystem>();

            //GetOrCreateAndAddManagedSystem<DebugDrawFactionShipsCollisionLayersSystem>();
            //GetOrCreateAndAddManagedSystem<DebugDrawFactionShipsCollidersSystem>();
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
            GetOrCreateAndAddUnmanagedSystem<ShipVsBulletDamageSystem>();
            GetOrCreateAndAddUnmanagedSystem<ShipVsShipDamageSystem>();
            GetOrCreateAndAddUnmanagedSystem<ShipVsExplosionDamageSystem>();
            GetOrCreateAndAddUnmanagedSystem<ShipVsWallDamageSystem>();
            GetOrCreateAndAddUnmanagedSystem<BulletVsWallSystem>();
            GetOrCreateAndAddUnmanagedSystem<CheckSpawnPointIsSafeSystem>();
            GetOrCreateAndAddUnmanagedSystem<TravelThroughWormholeSystem>();
            GetOrCreateAndAddUnmanagedSystem<UpdateTimeToLiveSystem>();
            GetOrCreateAndAddUnmanagedSystem<DestroyShipsWithNoHealthSystem>();
            GetOrCreateAndAddUnmanagedSystem<SpawnShipsPrioritizeSystem>();
            GetOrCreateAndAddUnmanagedSystem<SpawnShipsDequeueSystem>();
            GetOrCreateAndAddUnmanagedSystem<EvaluateMissionSystem>();
            GetOrCreateAndAddUnmanagedSystem<FireGunsSystem>();
        }
    }
}

