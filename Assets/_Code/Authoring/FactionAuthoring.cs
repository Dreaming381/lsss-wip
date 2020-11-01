using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Lsss.Authoring
{
    [DisallowMultipleComponent]
    public class FactionAuthoring : MonoBehaviour, IDeclareReferencedPrefabs, IConvertGameObjectToEntity
    {
        public int                totalUnits         = 10;
        public int                maxUnitsAtOnce     = 10;
        public float              spawnWeightInverse = 1f;
        public SpaceshipAuthoring aiShipPrefab;
        public SpaceshipAuthoring playerShipPrefab;

        public void DeclareReferencedPrefabs(List<GameObject> referencedPrefabs)
        {
            referencedPrefabs.Add(aiShipPrefab.gameObject);
            if (playerShipPrefab != null)
                referencedPrefabs.Add(playerShipPrefab.gameObject);
        }

        public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
        {
            dstManager.AddComponentData(entity, new Faction
            {
                name                    = name,
                remainingReinforcements = totalUnits,
                maxFieldUnits           = maxUnitsAtOnce,
                spawnWeightInverse      = spawnWeightInverse,
                aiPrefab                = conversionSystem.GetPrimaryEntity(aiShipPrefab),
                playerPrefab            = conversionSystem.TryGetPrimaryEntity(playerShipPrefab)
            });
            dstManager.AddComponent<FactionTag>(entity);
        }
    }
}

