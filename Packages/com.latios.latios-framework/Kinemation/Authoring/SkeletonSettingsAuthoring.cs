using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace Latios.Kinemation.Authoring
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Latios/Kinemation/Skeleton Settings (Animated Hierarchy)")]
    public class SkeletonSettingsAuthoring : MonoBehaviour
    {
        public BindingMode bindingMode = BindingMode.BakeTime;

        //public List<Transform> customIncludeBones;
        //public List<Transform> exportInForcedOptimizedHierarchy;

        public enum BindingMode
        {
            DoNotGenerate,
            BakeTime,
            //BakeTimeForceOptimized
            //CustomWhitelistPlusAncestors
            //CustomWhitelistPlusAncestorsForceOptimized
        }
    }

    [Serializable]
    internal struct BoneTransformData
    {
        public float3     localPosition;
        public quaternion localRotation;
        public float3     localScale;
        public Transform  gameObjectTransform;  // Null if not exposed
        public int        parentIndex;  // -1 if root, otherwise must be less than current index
        public bool       ignoreParentScale;
        public string     hierarchyReversePath;  // Example: "foot.l/lower leg.l/upper leg.l/hips/armature/red soldier/"
    }
}

