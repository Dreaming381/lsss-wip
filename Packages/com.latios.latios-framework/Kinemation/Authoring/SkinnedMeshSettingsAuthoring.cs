using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Latios.Kinemation.Authoring
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Latios/Kinemation/Skinned Mesh Settings")]
    public class SkinnedMeshSettingsAuthoring : MonoBehaviour
    {
        public BindingMode bindingMode = BindingMode.BakeTime;

        public List<string> customBonePathsReversed;

        public enum BindingMode
        {
            DoNotGenerate,
            BakeTime,
            OverridePaths
        }
    }
}

