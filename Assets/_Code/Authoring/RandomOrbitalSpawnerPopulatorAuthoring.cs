using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Lsss.Authoring
{
    [DisallowMultipleComponent]
    [RequiresEntityConversion]
    public class RandomOrbitalSpawnerPopulatorAuthoring : MonoBehaviour, IConvertGameObjectToEntity
    {
        public int                        spawnerCount    = 0;
        public uint                       randomSeed      = 150;
        public float                      minRadius       = 10f;
        public float                      colliderRadius  = 15f;
        public float2                     minMaxOrbitTime = new float2(5f, 100f);
        public SpawnPointGraphicAuthoring spawnGraphicPrefab;

        public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
        {
            dstManager.AddComponentData(entity, new OrbitalSpawnPointPopulator
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

