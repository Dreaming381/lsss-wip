using Latios;
using Latios.Transforms;
using Unity.Entities;
using UnityEngine;

namespace Lsss
{
    public partial struct CameraManager : IManagedStructComponent
    {
        public Camera camera;
    }

    public class CameraAuthoring : MonoBehaviour, IInitializeGameObjectEntity
    {
        public void Initialize(LatiosWorld latiosWorld, Entity gameObjectEntity)
        {
            latiosWorld.latiosWorldUnmanaged.AddManagedStructComponent(gameObjectEntity, new CameraManager { camera = GetComponent<Camera>() });
        }
    }
}

