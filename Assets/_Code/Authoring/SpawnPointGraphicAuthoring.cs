using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Lsss.Authoring
{
    [DisallowMultipleComponent]
    [AddComponentMenu("LSSS/Spawning/Spawner Graphic")]
    public class SpawnPointGraphicAuthoring : MonoBehaviour
    {
        public float lifeTime   = 5f;
        public float growTime   = 1.5f;
        public float shrinkTime = 1.5f;
        public float spawnTime  = 1.5f;

        public float growSpins   = 1f;
        public float shrinkSpins = 1f;
    }

    public class SpawnPointGraphicBaker : Baker<SpawnPointGraphicAuthoring>
    {
        public override void Bake(SpawnPointGraphicAuthoring authoring)
        {
            AddComponent(new SpawnPointAnimationData
            {
                growStartTime   = authoring.lifeTime,
                growEndTime     = authoring.lifeTime - authoring.growTime,
                shrinkStartTime = authoring.shrinkTime,
                growSpins       = authoring.growSpins * math.PI * 2,
                shrinkSpins     = authoring.shrinkSpins * math.PI * 2
            });
            AddComponent(new TimeToLive { timeToLive = authoring.lifeTime });
            AddComponent(new Scale { Value           = 0f });
        }
    }
}

