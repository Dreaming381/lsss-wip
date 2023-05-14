using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

public class CameraManager : IComponentData
{
    public Camera camera;
}

public class CameraAuthoring : MonoBehaviour
{
}

public class CameraBaker : Baker<CameraAuthoring>
{
    public override void Bake(CameraAuthoring authoring)
    {
        var entity = GetEntity(TransformUsageFlags.Dynamic);
        AddComponentObject(entity, new CameraManager());
    }
}

