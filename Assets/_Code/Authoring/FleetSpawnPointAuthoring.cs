using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Lsss.Authoring
{
    [DisallowMultipleComponent]
    public class FleetSpawnPointAuthoring : MonoBehaviour, IDeclareReferencedPrefabs, IConvertGameObjectToEntity
    {
        public FactionAuthoring           faction;
        public SpawnPointGraphicAuthoring spawnPointGraphic;
        public float                      lifeTime = 15f;

        //We don't actually declare any referenced prefabs. We are just using this to modify the spawnPointGraphic before it gets converted.
        public void DeclareReferencedPrefabs(List<GameObject> referencedPrefabs)
        {
            spawnPointGraphic.lifeTime  = lifeTime;
            spawnPointGraphic.growTime  = 0f;
            spawnPointGraphic.growSpins = 0f;
            spawnPointGraphic.spawnTime = 0f;
        }

        public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
        {
            dstManager.AddComponentData(entity, new TimeToLive { timeToLive = lifeTime });
            dstManager.AddComponent<FleetSpawnPointTag>(entity);
            conversionSystem.DeclareLinkedEntityGroup(gameObject);
        }
    }
}

