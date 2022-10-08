using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Lsss.Authoring
{
    [DisallowMultipleComponent]
    [AddComponentMenu("LSSS/AI/AI Brain", 0)]
    public class AiBrainAuthoring : MonoBehaviour
    {
    }

    public class AiBrainBaker : Baker<AiBrainAuthoring>
    {
        public override void Bake(AiBrainAuthoring authoring)
        {
            AddComponent<AiGoalOutput>();
            AddComponent<AiTag>();
        }
    }
}

