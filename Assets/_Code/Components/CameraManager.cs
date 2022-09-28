using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

public class CameraManager : MonoBehaviour
{
    new public Camera camera;
}

public partial class CameraManagerConversionSystem : GameObjectConversionSystem
{
    protected override void OnUpdate()
    {
        int i = 0;
        Entities.ForEach((Entity entity, CameraManager manager) =>
        {
            var go    = new GameObject($"Camera Manager {i++}");
            var cm    = go.AddComponent<CameraManager>();
            cm.camera = manager.camera;
            DstEntityManager.AddComponentObject(GetPrimaryEntity(manager), cm);
            //AddHybridComponent(manager);
        }).WithStructuralChanges().Run();
    }
}

