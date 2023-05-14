using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Lsss.Authoring
{
    [DisallowMultipleComponent]
    [AddComponentMenu("LSSS/Spawning/Fleet Spawner Slot")]
    public class FleetSpawnSlotAuthoring : MonoBehaviour
    {
        public bool spawnPlayer = false;
    }

    public class FleetSpawnSlotBaker : Baker<FleetSpawnSlotAuthoring>
    {
        public override void Bake(FleetSpawnSlotAuthoring authoring)
        {
            var fleetSpawner = GetComponentInParent<FleetSpawnPointAuthoring>();

            if (fleetSpawner == null)
            {
                Debug.LogWarning("FleetSpawnSlot was created without a parent FleetSpawnPoint and was not baked");
                return;
            }

            var entity = GetEntity(TransformUsageFlags.Renderable);

            AddComponent(                   entity, new FleetSpawnSlotFactionReference { factionEntity = GetEntity(fleetSpawner.faction, TransformUsageFlags.None) });
            AddComponent<FleetSpawnSlotTag>(entity);
            if (authoring.spawnPlayer)
                AddComponent<FleetSpawnPlayerSlotTag>(entity);
        }
    }
}

