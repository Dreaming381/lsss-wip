using Latios;
using Latios.Transforms;
using Lsss.SuperSystems;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace Lsss.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class LsssSimulationRootSuperSystem : RootSuperSystem
    {
        protected override void CreateSystems()
        {
            GetOrCreateAndAddManagedSystem<PlayerGameplayReadInputSystem>();

            // Motion
            GetOrCreateAndAddUnmanagedSystem<MoveShipsSystem>();
            GetOrCreateAndAddUnmanagedSystem<MoveBulletsSystem>();
            GetOrCreateAndAddUnmanagedSystem<ExpandExplosionsSystem>();
            GetOrCreateAndAddUnmanagedSystem<MoveOrbitalSpawnPointsSystem>();

            // Collision layers
            GetOrCreateAndAddUnmanagedSystem<BuildSpawnPointCollisionLayerSystem>();
            GetOrCreateAndAddUnmanagedSystem<BuildShipsCollisionLayersSystem>();
            GetOrCreateAndAddUnmanagedSystem<BuildBulletsCollisionLayerSystem>();
            GetOrCreateAndAddUnmanagedSystem<BuildExplosionsCollisionLayerSystem>();
            GetOrCreateAndAddUnmanagedSystem<BuildWallsCollisionLayerSystem>();
            GetOrCreateAndAddUnmanagedSystem<BuildWormholesCollisionLayerSystem>();

            // Debug collision layers
            //GetOrCreateAndAddManagedSystem<DebugDrawFactionShipsCollisionLayersSystem>();
            //GetOrCreateAndAddManagedSystem<DebugDrawFactionShipsCollidersSystem>();
            //GetOrCreateAndAddSystem<DebugDrawBulletCollisionLayersSystem>();
            //GetOrCreateAndAddSystem<DebugDrawWormholeCollisionLayersSystem>();
            //GetOrCreateAndAddSystem<DebugDrawSpawnPointCollisionLayersSystem>();

            // AI
            GetOrCreateAndAddUnmanagedSystem<AiUpdateRadarScanRequestsSystem>();
            GetOrCreateAndAddUnmanagedSystem<AiShipRadarScanSystem3>();
            GetOrCreateAndAddUnmanagedSystem<AiSearchAndDestroyInitializePersonalitySystem>();
            GetOrCreateAndAddUnmanagedSystem<AiSearchAndDestroySystem>();
            GetOrCreateAndAddUnmanagedSystem<AiExploreInitializePersonalitySystem>();
            GetOrCreateAndAddUnmanagedSystem<AiExploreSystem>();
            GetOrCreateAndAddUnmanagedSystem<AiEvaluateGoalsSystem>();
            GetOrCreateAndAddUnmanagedSystem<AiCreateDesiredActionsSystem>();

            // Spawners
            GetOrCreateAndAddUnmanagedSystem<CheckSpawnPointIsSafeSystem>();
            GetOrCreateAndAddUnmanagedSystem<SpawnShipsPrioritizeSystem>();
            GetOrCreateAndAddUnmanagedSystem<SpawnShipsDequeueSystem>();  // Modifies transforms of spawners, which delays FireGunsSystem

            // Collision
            GetOrCreateAndAddUnmanagedSystem<ShipVsBulletDamageSystem>();
            GetOrCreateAndAddUnmanagedSystem<ShipVsShipDamageSystem>();
            GetOrCreateAndAddUnmanagedSystem<ShipVsExplosionDamageSystem>();
            GetOrCreateAndAddUnmanagedSystem<ShipVsWallDamageSystem>();
            GetOrCreateAndAddUnmanagedSystem<BulletVsWallSystem>();

            // Analysis
            GetOrCreateAndAddUnmanagedSystem<UpdateTimeToLiveSystem>();
            GetOrCreateAndAddUnmanagedSystem<DestroyShipsWithNoHealthSystem>();
            GetOrCreateAndAddUnmanagedSystem<EvaluateMissionSystem>();
            GetOrCreateAndAddUnmanagedSystem<FireGunsSystem>();
            //GetOrCreateAndAddUnmanagedSystem<TravelThroughWormholeSystem>();

            // Camera
            GetOrCreateAndAddUnmanagedSystem<CameraFollowPlayerSystem>();
            GetOrCreateAndAddUnmanagedSystem<FaceCameraSystem>();
        }
    }
}

