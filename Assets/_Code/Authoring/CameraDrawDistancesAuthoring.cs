using System.Collections;
using Unity.Entities;
using UnityEngine;

namespace Lsss.Authoring
{
    [DisallowMultipleComponent]
    [AddComponentMenu("LSSS/Behaviors/Camera Draw Distances")]
    public class CameraDrawDistancesAuthoring : MonoBehaviour
    {
        public float[] distances;
    }

    public class CameraDrawDistancesBaker : Baker<CameraDrawDistancesAuthoring>
    {
        public override void Bake(CameraDrawDistancesAuthoring authoring)
        {
            var           entity        = GetEntity(TransformUsageFlags.None);
            DrawDistances drawDistances = default;
            foreach (var d in authoring.distances)
                drawDistances.distances.Add(d);
            AddComponent(entity, drawDistances);
        }
    }
}

