﻿using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Lsss.Authoring
{
    [DisallowMultipleComponent]
    [AddComponentMenu("LSSS/Spawning/Fleet Spawner")]
    public class FleetSpawnPointAuthoring : MonoBehaviour
    {
        public FactionAuthoring           faction;  // Read by slots
        public SpawnPointGraphicAuthoring spawnPointGraphic;
    }

    public class FleetSpawnPointBaker : Baker<FleetSpawnPointAuthoring>
    {
        public override void Bake(FleetSpawnPointAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);  // SpawnPointAnimationSystem assumes a parent.
            GetComponent<SpawnPointGraphicAuthoring>(authoring.spawnPointGraphic);
            AddComponent(                    entity, new TimeToLive { timeToLive = authoring.spawnPointGraphic.lifeTime });
            AddComponent<FleetSpawnPointTag>(entity);
        }
    }
}

