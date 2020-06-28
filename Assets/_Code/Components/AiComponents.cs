using Latios;
using Latios.PhysicsEngine;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Lsss
{
    public struct AiTag : IComponentData { }

    public struct AiBrain : IComponentData
    {
        public Entity shipRadar;
    }

    public struct AiWantsToFire : IComponentData
    {
        public bool fire;
    }

    public struct AiGoalOutput : IComponentData
    {
        public float3 flyTowardsPosition;
        public bool   useAggressiveSteering;
        public bool   isValid;
    }

    public struct AiSearchAndDestroyOutput : IComponentData
    {
        public float3 flyTowardsPosition;
        public bool   isPositionValid;
        public bool   fire;
    }

    public struct AiSearchAndDestroyPersonality : IComponentData
    {
        public float targetLeadDistance;
    }

    public struct AiSearchAndDestroyPersonalityInitializerValues : IComponentData
    {
        public float2 targetLeadDistanceMinMax;
    }

    public struct AiShipRadarEntity : IComponentData
    {
        public Entity shipRadar;
    }

    public struct AiExploreOutput : IComponentData
    {
        public float3 wanderPosition;
        public float3 nearestWormhole;
        public bool   wanderPositionValid;
        public bool   nearestWormholeValid;
    }

    public struct AiExplorePersonality : IComponentData
    {
        public float spawnForwardDistance;
        public float wanderDestinationRadius;
        public float wanderPositionSearchRadius;
    }

    public struct AiExplorePersonalityInitializerValues : IComponentData
    {
        public float2 spawnForwardDistanceMinMax;
        public float2 wanderDestinationRadiusMinMax;
        public float2 wanderPositionSearchRadiusMinMax;
    }

    public struct AiExploreState : IComponentData
    {
        public float3 wanderPosition;
    }

    public struct AiShipRadar : IComponentData
    {
        public float distance;
        public float cosFov;

        public Entity target;  //Null if needs target
        public float  preferredTargetDistance;

        public float friendCrossHairsDistanceFilter;
        public float friendCrossHairsCosFovFilter;
        public float nearestEnemyCrossHairsDistanceFilter;
        public float nearestEnemyCrossHairsCosFovFilter;
    }

    public struct AiShipRadarScanResults : IComponentData
    {
        public Entity         target;  //Null if target not found. Otherwise either new target or existing target based on whether target of radar was null.
        public RigidTransform targetTransform;

        public bool friendFound;

        public Entity         nearestEnemy;
        public RigidTransform nearestEnemyTransform;
    }

    public struct AiRadarTag : IComponentData { }
}

