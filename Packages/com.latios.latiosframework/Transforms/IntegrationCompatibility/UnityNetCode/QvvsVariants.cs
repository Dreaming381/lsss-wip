#if UNITY_NETCODE && !LATIOS_TRANSFORMS_UNITY
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using UnityEditor;
using UnityEngine.Scripting;

namespace Latios.Transforms.Compatibility.UnityNetCode
{
    /// <summary>
    /// The default serialization strategy for the <see cref="Unity.Transforms.LocalTransform"/> components provided by the NetCode package.
    /// </summary>
    [Preserve]
    [GhostComponentVariation(typeof(Transforms.WorldTransform), "Transform QVVS - 3D")]
    [GhostComponent(PrefabType = GhostPrefabType.All, SendTypeOptimization = GhostSendType.AllClients)]
    public struct WorldTransformDefaultVariant
    {
        /// <summary>
        /// The rotation quaternion is replicated and the resulting floating point data use for replication the rotation is quantized with good precision (10 or more bits per component)
        /// </summary>
        [GhostField(Quantization = 1000, Smoothing = SmoothingAction.InterpolateAndExtrapolate)]
        public quaternion __rotation;

        /// <summary>
        /// The position value is replicated with a default quantization unit of 1000 (so roughly 1mm precision per component).
        /// The replicated position value support both interpolation and extrapolation
        /// </summary>
        [GhostField(Quantization = 1000, Smoothing = SmoothingAction.InterpolateAndExtrapolate)]
        public float3 __position;

        /// <summary>
        /// The scale value is replicated with a default quantization unit of 1000.
        /// The replicated scale value support both interpolation and extrapolation
        /// </summary>
        [GhostField(Quantization = 1000, Smoothing = SmoothingAction.InterpolateAndExtrapolate)]
        public float __scale;

        /// <summary>
        /// The stretch value is replicated with a default quantization unit of 1000.
        /// The replicated stretch value support both interpolation and extrapolation
        /// </summary>
        [GhostField(Quantization = 1000, Smoothing = SmoothingAction.InterpolateAndExtrapolate)]
        public float3 __stretch;
    }
}
#endif

