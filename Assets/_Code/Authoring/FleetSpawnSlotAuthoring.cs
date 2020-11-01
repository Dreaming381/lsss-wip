using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Lsss.Authoring
{
    [DisallowMultipleComponent]
    public class FleetSpawnSlotAuthoring : MonoBehaviour, IConvertGameObjectToEntity
    {
        public bool spawnPlayer = false;

        public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
        {
            FleetSpawnPointAuthoring fleetSpawner   = null;
            var                      fleetTransform = transform;
            while (fleetSpawner == null && fleetTransform != null)
            {
                fleetSpawner   = fleetTransform.GetComponent<FleetSpawnPointAuthoring>();
                fleetTransform = fleetTransform.parent;
            }

            if (fleetSpawner == null)
            {
                Debug.LogWarning("FleetSpawnSlot was created without a parent FleetSpawnPoint and was not converted");
                return;
            }

            dstManager.AddSharedComponentData(entity, new FactionMember { factionEntity = conversionSystem.GetPrimaryEntity(fleetSpawner.faction) });
            dstManager.AddComponent<FleetSpawnSlotTag>(entity);
            if (spawnPlayer)
                dstManager.AddComponent<FleetSpawnPlayerSlotTag>(entity);
        }
    }
}

