using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Lsss.Authoring
{
    [DisallowMultipleComponent]
    public class SpawnPointGraphicAuthoring : MonoBehaviour, IConvertGameObjectToEntity
    {
        public float lifeTime   = 5f;
        public float growTime   = 1.5f;
        public float shrinkTime = 1.5f;
        public float spawnTime  = 1.5f;

        public float growSpins   = 1f;
        public float shrinkSpins = 1f;

        public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
        {
            dstManager.AddComponentData(entity, new SpawnPointAnimationData
            {
                growStartTime   = lifeTime,
                growEndTime     = lifeTime - growTime,
                shrinkStartTime = shrinkTime,
                growSpins       = growSpins * math.PI * 2,
                shrinkSpins     = shrinkSpins * math.PI * 2
            });
            dstManager.AddComponentData(entity, new TimeToLive { timeToLive = lifeTime });
            if (dstManager.HasComponent<NonUniformScale>(entity))
                dstManager.RemoveComponent<NonUniformScale>(entity);
            if (!dstManager.HasComponent<Scale>(entity))
                dstManager.AddComponent<Scale>(entity);
            dstManager.SetComponentData(entity, new Scale { Value = 0f });
        }
    }
}

