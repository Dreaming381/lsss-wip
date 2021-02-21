using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraManager : MonoBehaviour
{
    new public Camera camera;
}

public class CameraManagerConversionSystem : GameObjectConversionSystem
{
    protected override void OnUpdate()
    {
        Entities.ForEach((CameraManager manager) =>
        {
            AddHybridComponent(manager);
        });
    }
}

