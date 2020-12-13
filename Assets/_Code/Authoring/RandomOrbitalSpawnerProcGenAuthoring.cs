using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Lsss.Authoring
{
    [DisallowMultipleComponent]
    [AddComponentMenu("LSSS/Spawning/Orbital Spawner Random Generator")]
    public class RandomOrbitalSpawnerProcGenAuthoring : MonoBehaviour, IDeclareReferencedPrefabs, IConvertGameObjectToEntity
    {
        public int                        spawnerCount    = 0;
        public uint                       randomSeed      = 150;
        public float                      minRadius       = 10f;
        public float                      colliderRadius  = 15f;
        public float2                     minMaxOrbitTime = new float2(5f, 100f);
        public SpawnPointGraphicAuthoring spawnGraphicPrefab;

        public void DeclareReferencedPrefabs(List<GameObject> referencedPrefabs)
        {
            referencedPrefabs.Add(spawnGraphicPrefab.gameObject);
        }

        public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
        {
            dstManager.AddComponentData(entity, new OrbitalSpawnPointProcGen
            {
                spawnerCount       = spawnerCount,
                randomSeed         = randomSeed,
                minRadius          = minRadius,
                minMaxOrbitSpeed   = 2 * math.PI / minMaxOrbitTime.yx,
                colliderRadius     = colliderRadius,
                spawnGraphicPrefab = conversionSystem.GetPrimaryEntity(spawnGraphicPrefab),
                maxTimeUntilSpawn  = spawnGraphicPrefab.spawnTime,
                maxPauseTime       = spawnGraphicPrefab.lifeTime
            });
        }
    }
}

