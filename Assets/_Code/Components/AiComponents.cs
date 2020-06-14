using Latios;
using Latios.PhysicsEngine;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Lsss
{
    public struct AiTag : IComponentData { }

    public struct AiInitializeTag : IComponentData { }

    public struct AiDestination : IComponentData
    {
        public float3 position;
        public bool   chase;
    }

    public struct AiTargetEnemy : IComponentData
    {
        public Entity enemy;
        public float3 enemyPositionLastUpdate;
    }

    public struct AiWantsToFire : IComponentData
    {
        public bool fire;
    }

    public struct AiPersonality : IComponentData
    {
        public float spawnForwardDistance;
        public float destinationRadius;
        public float targetLeadDistance;
    }

    public struct AiBrain : IComponentData
    {
        public Entity shipRadar;
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

