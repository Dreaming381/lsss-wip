using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Lsss.Authoring
{
    [DisallowMultipleComponent]
    [AddComponentMenu("LSSS/Objects/Faction")]
    public class FactionAuthoring : MonoBehaviour
    {
        public int                totalUnits         = 10;
        public int                maxUnitsAtOnce     = 10;
        public float              spawnWeightInverse = 1f;
        public SpaceshipAuthoring aiShipPrefab;
        public SpaceshipAuthoring playerShipPrefab;
    }

    public class FactionBaker : Baker<FactionAuthoring>
    {
        public override void Bake(FactionAuthoring authoring)
        {
            AddComponent(new Faction
            {
                name                    = authoring.name,
                remainingReinforcements = authoring.totalUnits,
                maxFieldUnits           = authoring.maxUnitsAtOnce,
                spawnWeightInverse      = authoring.spawnWeightInverse,
                aiPrefab                = GetEntity(authoring.aiShipPrefab),
                playerPrefab            = GetEntity(authoring.playerShipPrefab)  // This method handles null correctly
            });
            AddComponent<FactionTag>();
        }
    }
}

