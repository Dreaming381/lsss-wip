using Latios;
using Unity.Transforms;

namespace Lsss.SuperSystems
{
    /// <summary>
    /// Handles spawning and other initialization work related to core gameplay.
    /// </summary>
    public partial class GameplaySyncPointSuperSystem : SuperSystem
    {
        protected override void CreateSystems()
        {
            GetOrCreateAndAddUnmanagedSystem<OrbitalSpawnersProcGenSystem>();
            GetOrCreateAndAddUnmanagedSystem<SpawnFleetsSystem>();
            GetOrCreateAndAddUnmanagedSystem<SpawnShipsEnqueueSystem>();
            GetOrCreateAndAddUnmanagedSystem<SpawnShipsEnableSystem>();
        }
    }

    /// <summary>
    /// Updates the motion simulation after the player and AI have made decisions.
    /// </summary>
    public partial class AdvanceGameplayMotionSuperSystem : SuperSystem
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
    public partial class UpdateTransformSpatialQueriesSuperSystem : SuperSystem
    {
        protected override void CreateSystems()
        {
            GetOrCreateAndAddUnmanagedSystem<BuildSpawnPointCollisionLayerSystem>();
            GetOrCreateAndAddUnmanagedSystem<BuildShipsCollisionLayersSystem>();
            GetOrCreateAndAddUnmanagedSystem<BuildBulletsCollisionLayerSystem>();
            GetOrCreateAndAddUnmanagedSystem<BuildExplosionsCollisionLayerSystem>();
            GetOrCreateAndAddUnmanagedSystem<BuildWallsCollisionLayerSystem>();
            GetOrCreateAndAddUnmanagedSystem<BuildWormholesCollisionLayerSystem>();

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
    public partial class ProcessGameplayEventsSuperSystem : SuperSystem
    {
        protected override void CreateSystems()
        {
            GetOrCreateAndAddUnmanagedSystem<CheckSpawnPointIsSafeSystem>();
            GetOrCreateAndAddUnmanagedSystem<SpawnShipsPrioritizeSystem>();
            GetOrCreateAndAddUnmanagedSystem<SpawnShipsDequeueSystem>();  // Modifies transforms of spawners, which delays FireGunsSystem

            GetOrCreateAndAddUnmanagedSystem<ShipVsBulletDamageSystem>();
            GetOrCreateAndAddUnmanagedSystem<ShipVsShipDamageSystem>();
            GetOrCreateAndAddUnmanagedSystem<ShipVsExplosionDamageSystem>();
            GetOrCreateAndAddUnmanagedSystem<ShipVsWallDamageSystem>();
            GetOrCreateAndAddUnmanagedSystem<BulletVsWallSystem>();

            //GetOrCreateAndAddUnmanagedSystem<TravelThroughWormholeSystem>();
            GetOrCreateAndAddUnmanagedSystem<UpdateTimeToLiveSystem>();
            GetOrCreateAndAddUnmanagedSystem<DestroyShipsWithNoHealthSystem>();
            GetOrCreateAndAddUnmanagedSystem<EvaluateMissionSystem>();
            GetOrCreateAndAddUnmanagedSystem<FireGunsSystem>();
        }
    }
}

