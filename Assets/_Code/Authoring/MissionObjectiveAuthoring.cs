using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Lsss.Authoring
{
    [DisallowMultipleComponent]
    [AddComponentMenu("LSSS/Player/Mission Objective")]
    public class MissionObjectiveAuthoring : MonoBehaviour, IConvertGameObjectToEntity
    {
        public bool destroyAllFactions = true;

        public List<FactionAuthoring> factionsToDestroy = new List<FactionAuthoring>();

        public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
        {
            if (destroyAllFactions)
                dstManager.AddComponent<LastAliveObjectiveTag>(entity);
            else
            {
                var factions = dstManager.AddBuffer<DestroyFactionObjective>(entity);
                foreach(var f in factionsToDestroy)
                {
                    if (f != null)
                        factions.Add(new DestroyFactionObjective { factionToDestroy = conversionSystem.TryGetPrimaryEntity(f) });
                }
            }
        }
    }
}

