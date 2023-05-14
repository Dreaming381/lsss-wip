using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Lsss.Authoring
{
    [DisallowMultipleComponent]
    [AddComponentMenu("LSSS/Player/Mission Objective")]
    public class MissionObjectiveAuthoring : MonoBehaviour
    {
        public bool destroyAllFactions = true;

        public List<FactionAuthoring> factionsToDestroy = new List<FactionAuthoring>();
    }

    public class MissionObjectiveBaker : Baker<MissionObjectiveAuthoring>
    {
        public override void Bake(MissionObjectiveAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            if (authoring.destroyAllFactions)
                AddComponent<LastAliveObjectiveTag>(entity);
            else
            {
                var factions = AddBuffer<DestroyFactionObjective>(entity);
                foreach (var f in authoring.factionsToDestroy)
                {
                    if (f != null)
                        factions.Add(new DestroyFactionObjective { factionToDestroy = GetEntity(f, TransformUsageFlags.None) });
                }
            }
        }
    }
}

