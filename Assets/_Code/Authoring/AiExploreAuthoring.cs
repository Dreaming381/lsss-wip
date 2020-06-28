using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Lsss.Authoring
{
    [DisallowMultipleComponent]
    [RequiresEntityConversion]
    public class AiExploreAuthoring : MonoBehaviour, IConvertGameObjectToEntity
    {
        public float2 spawnForwardDistanceMinMax       = new float2(10f, 20f);
        public float2 wanderDestinationRadiusMinMax    = new float2(3f, 15f);
        public float2 wanderPositionSearchRadiusMinMax = new float2(25f, 100f);

        public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
        {
            dstManager.AddComponentData(entity, new AiExplorePersonalityInitializerValues
            {
                spawnForwardDistanceMinMax       = spawnForwardDistanceMinMax,
                wanderDestinationRadiusMinMax    = wanderDestinationRadiusMinMax,
                wanderPositionSearchRadiusMinMax = wanderPositionSearchRadiusMinMax
            });
            dstManager.AddComponent<AiExplorePersonality>(entity);
            dstManager.AddComponent<AiExploreState>(      entity);
            dstManager.AddComponent<AiExploreOutput>(     entity);
        }
    }
}

