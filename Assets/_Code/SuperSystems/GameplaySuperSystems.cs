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
            GetOrCreateAndAddSystem<PopulateOrbitalSpawnersSystem>();
            GetOrCreateAndAddSystem<SpawnFleetsSystem>();
            GetOrCreateAndAddSystem<SpawnShipsEnqueueSystem>();
            GetOrCreateAndAddSystem<SpawnShipsEnableSystem>();
        }
    }

    /// <summary>
    /// Updates the motion simulation after the player and AI have made decisions.
    /// </summary>
    public class AdvanceGameplayMotionSuperSystem : SuperSystem
    {
        protected override void CreateSystems()
        {
            GetOrCreateAndAddSystem<MoveShipsSystem>();
            GetOrCreateAndAddSystem<MoveBulletsSystem>();
            GetOrCreateAndAddSystem<ExpandExplosionsSystem>();
            GetOrCreateAndAddSystem<MoveOrbitalSpawnPointsSystem>();
        }
    }

    /// <summary>
    /// Updates spatial query data structures and other metadata for future systems to use.
    /// </summary>
    public class UpdateTransformSpatialQueriesSuperSystem : SuperSystem
    {
        protected override void CreateSystems()
        {
            GetOrCreateAndAddSystem<BuildFactionShipsCollisionLayersSystem>();
            GetOrCreateAndAddSystem<BuildBulletsCollisionLayerSystem>();
            GetOrCreateAndAddSystem<BuildExplosionsCollisionLayerSystem>();
            GetOrCreateAndAddSystem<BuildWallsCollisionLayerSystem>();
            GetOrCreateAndAddSystem<BuildWormholesCollisionLayerSystem>();
            GetOrCreateAndAddSystem<BuildSpawnPointCollisionLayerSystem>();

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
            GetOrCreateAndAddSystem<ShipVsBulletDamageSystem>();
            GetOrCreateAndAddSystem<ShipVsShipDamageSystem>();
            GetOrCreateAndAddSystem<ShipVsExplosionDamageSystem>();
            GetOrCreateAndAddSystem<ShipVsWallDamageSystem>();
            GetOrCreateAndAddSystem<BulletVsWallSystem>();
            GetOrCreateAndAddSystem<CheckSpawnPointIsSafeSystem>();
            GetOrCreateAndAddSystem<FireGunsSystem>();
            GetOrCreateAndAddSystem<TravelThroughWormholeSystem>();
            GetOrCreateAndAddSystem<UpdateTimeToLiveSystem>();
            GetOrCreateAndAddSystem<DestroyShipsWithNoHealthSystem>();
            GetOrCreateAndAddSystem<SpawnShipsDequeueSystem>();
        }
    }
}

