using System.Collections;
using Unity.Entities;
using UnityEngine;

namespace Lsss.Authoring
{
    [DisallowMultipleComponent]
    [AddComponentMenu("LSSS/Behaviors/Camera Draw Distances")]
    public class CameraDrawDistancesAuthoring : MonoBehaviour, IConvertGameObjectToEntity
    {
        public float[] distances;

        public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
        {
            DrawDistances drawDistances = default;
            foreach (var d in distances)
                drawDistances.distances.Add(d);
            dstManager.AddComponentData(entity, drawDistances);
        }
    }
}

