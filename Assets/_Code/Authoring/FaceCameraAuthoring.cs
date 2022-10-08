using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Lsss.Authoring
{
    [DisallowMultipleComponent]
    [AddComponentMenu("LSSS/Behaviors/Face Camera")]
    public class FaceCameraAuthoring : MonoBehaviour
    {
    }

    public class FaceCameraBaker : Baker<FaceCameraAuthoring>
    {
        public override void Bake(FaceCameraAuthoring authoring)
        {
            AddComponent<FaceCameraTag>();
        }
    }
}

