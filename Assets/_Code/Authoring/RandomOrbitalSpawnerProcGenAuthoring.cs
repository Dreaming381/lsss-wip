using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Lsss.Authoring
{
    [DisallowMultipleComponent]
    [AddComponentMenu("LSSS/Spawning/Orbital Spawner Random Generator")]
    public class RandomOrbitalSpawnerProcGenAuthoring : MonoBehaviour
    {
        public int                        spawnerCount    = 0;
        public uint                       randomSeed      = 150;
        public float                      minRadius       = 10f;
        public float                      colliderRadius  = 15f;
        public float2                     minMaxOrbitTime = new float2(5f, 100f);
        public SpawnPointGraphicAuthoring spawnGraphicPrefab;
    }

    public class RandomOrbitalSpawnerProcGenBaker : Baker<RandomOrbitalSpawnerProcGenAuthoring>
    {
        public override void Bake(RandomOrbitalSpawnerProcGenAuthoring authoring)
        {
            GetComponent<SpawnPointGraphicAuthoring>(authoring.spawnGraphicPrefab);

            AddComponent(new OrbitalSpawnPointProcGen
            {
                spawnerCount       = authoring.spawnerCount,
                randomSeed         = authoring.randomSeed,
                minRadius          = authoring.minRadius,
                minMaxOrbitSpeed   = 2 * math.PI / authoring.minMaxOrbitTime.yx,
                colliderRadius     = authoring.colliderRadius,
                spawnGraphicPrefab = GetEntity(authoring.spawnGraphicPrefab),
                maxTimeUntilSpawn  = authoring.spawnGraphicPrefab.spawnTime,
                maxPauseTime       = authoring.spawnGraphicPrefab.lifeTime
            });
        }
    }
}

