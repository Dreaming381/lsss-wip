using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Lsss.Authoring
{
    [DisallowMultipleComponent]
    [AddComponentMenu("LSSS/AI/Explore Module")]
    public class AiExploreAuthoring : MonoBehaviour
    {
        public float2 spawnForwardDistanceMinMax       = new float2(10f, 20f);
        public float2 wanderDestinationRadiusMinMax    = new float2(3f, 15f);
        public float2 wanderPositionSearchRadiusMinMax = new float2(25f, 100f);
    }

    public class AiExploreBaker : Baker<AiExploreAuthoring>
    {
        public override void Bake(AiExploreAuthoring authoring)
        {
            AddComponent(new AiExplorePersonalityInitializerValues
            {
                spawnForwardDistanceMinMax       = authoring.spawnForwardDistanceMinMax,
                wanderDestinationRadiusMinMax    = authoring.wanderDestinationRadiusMinMax,
                wanderPositionSearchRadiusMinMax = authoring.wanderPositionSearchRadiusMinMax
            });
            AddComponent<AiExplorePersonality>();
            AddComponent<AiExploreState>();
            AddComponent<AiExploreOutput>();
        }
    }
}

