#region Header
// This define fails tests due to the extra log spam. Don't check this in enabled
// #define DEBUG_LOG_HYBRID_RENDERER

// #define DEBUG_LOG_CHUNK_CHANGES
// #define DEBUG_LOG_GARBAGE_COLLECTION
// #define DEBUG_LOG_BATCH_UPDATES
// #define DEBUG_LOG_CHUNKS
// #define DEBUG_LOG_INVALID_CHUNKS
// #define DEBUG_LOG_UPLOADS
// #define DEBUG_LOG_BATCH_CREATION
// #define DEBUG_LOG_BATCH_DELETION
// #define DEBUG_LOG_PROPERTY_ALLOCATIONS
// #define DEBUG_LOG_PROPERTY_UPDATES
// #define DEBUG_LOG_VISIBLE_INSTANCES
// #define DEBUG_LOG_MATERIAL_PROPERTY_TYPES
// #define DEBUG_LOG_MEMORY_USAGE
// #define DEBUG_LOG_AMBIENT_PROBE
// #define DEBUG_LOG_DRAW_COMMANDS
// #define DEBUG_LOG_DRAW_COMMANDS_VERBOSE
// #define DEBUG_VALIDATE_DRAW_COMMAND_SORT
// #define DEBUG_LOG_BRG_MATERIAL_MESH
// #define DEBUG_LOG_GLOBAL_AABB
// #define PROFILE_BURST_JOB_INTERNALS
// #define DISABLE_HYBRID_RENDERER_ERROR_LOADING_SHADER
// #define DISABLE_INCLUDE_EXCLUDE_LIST_FILTERING

// Entities Graphics is disabled if SRP 10 is not found, unless an override define is present
// It is also disabled if -nographics is given from the command line.
#if !(SRP_10_0_0_OR_NEWER || HYBRID_RENDERER_ENABLE_WITHOUT_SRP)
#define HYBRID_RENDERER_DISABLED
#endif

using System;
using System.Collections.Generic;
using System.Text;
using Latios.Transforms;
using Unity.Assertions;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Entities.Graphics;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;

#if UNITY_EDITOR
using UnityEditor;
#endif

using System.Runtime.InteropServices;
using Latios.Transforms.Abstract;
using Unity.Entities.Exposed;
using Unity.Rendering;
using Unity.Transforms;
#endregion

using UnityEngine.XR;

#if !UNITY_BURST_EXPERIMENTAL_ATOMIC_INTRINSICS
#error Latios Framework requires UNITY_BURST_EXPERIMENTAL_ATOMIC_INTRINSICS to be defined in your scripting define symbols.
#endif

namespace Latios.Kinemation.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(UpdatePresentationSystemGroup))]
    [UpdateAfter(typeof(EntitiesGraphicsSystem))]
    [DisableAutoCreation]
    [BurstCompile]
    public unsafe partial class LatiosEntitiesGraphicsSystem : SubSystem
    {
        #region Managed API
        /// <summary>
        /// Toggles the activation of EntitiesGraphicsSystem.
        /// </summary>
        /// <remarks>
        /// To disable this system, use the HYBRID_RENDERER_DISABLED define.
        /// </remarks>
#if HYBRID_RENDERER_DISABLED
        public static bool EntitiesGraphicsEnabled => false;
#else
        public static bool EntitiesGraphicsEnabled => EntitiesGraphicsUtils.IsEntitiesGraphicsSupportedOnSystem();
#endif

        /// <summary>
        /// The maximum GPU buffer size (in bytes) that a batch can access.
        /// </summary>
        public static int MaxBytesPerBatch => UseConstantBuffers ? MaxBytesPerCBuffer : kMaxBytesPerBatchRawBuffer;

        /// <summary>
        /// Registers a material property type with the given name.
        /// </summary>
        /// <param name="type">The type of material property to register.</param>
        /// <param name="propertyName">The name of the property.</param>
        /// <param name="overrideTypeSizeGPU">An optional size of the type on the GPU.</param>
        public static void RegisterMaterialPropertyType(Type type, string propertyName, short overrideTypeSizeGPU = -1)
        {
            Assert.IsTrue(type != null,                        "type must be non-null");
            Assert.IsTrue(!string.IsNullOrEmpty(propertyName), "Property name must be valid");

            short typeSizeCPU = (short)UnsafeUtility.SizeOf(type);
            if (overrideTypeSizeGPU == -1)
                overrideTypeSizeGPU = typeSizeCPU;

            // For now, we only support overriding one material property with one type.
            // Several types can override one property, but not the other way around.
            // If necessary, this restriction can be lifted in the future.
            if (s_TypeToPropertyMappings.ContainsKey(type))
            {
                string prevPropertyName = s_TypeToPropertyMappings[type].Name;
                Assert.IsTrue(propertyName.Equals(
                                  prevPropertyName),
                              $"Attempted to register type {type.Name} with multiple different property names. Registered with \"{propertyName}\", previously registered with \"{prevPropertyName}\".");
            }
            else
            {
                var pm                         = new NamedPropertyMapping();
                pm.Name                        = propertyName;
                pm.SizeCPU                     = typeSizeCPU;
                pm.SizeGPU                     = overrideTypeSizeGPU;
                s_TypeToPropertyMappings[type] = pm;
            }
        }

        /// <summary>
        /// A templated version of the material type registration method.
        /// </summary>
        /// <typeparam name="T">The type of material property to register.</typeparam>
        /// <param name="propertyName">The name of the property.</param>
        /// <param name="overrideTypeSizeGPU">An optional size of the type on the GPU.</param>
        public static void RegisterMaterialPropertyType<T>(string propertyName, short overrideTypeSizeGPU = -1)
            where T : IComponentData
        {
            RegisterMaterialPropertyType(typeof(T), propertyName, overrideTypeSizeGPU);
        }

        /// <summary>
        /// Registers a material with the Entities Graphics System.
        /// </summary>
        /// <param name="material">The material instance to register</param>
        /// <returns>Returns the batch material ID</returns>
        public BatchMaterialID RegisterMaterial(Material material) => m_BatchRendererGroup.RegisterMaterial(material);

        /// <summary>
        /// Registers a mesh with the Entities Graphics System.
        /// </summary>
        /// <param name="mesh">Mesh instance to register</param>
        /// <returns>Returns the batch mesh ID</returns>
        public BatchMeshID RegisterMesh(Mesh mesh) => m_BatchRendererGroup.RegisterMesh(mesh);

        /// <summary>
        /// Unregisters a material from the Entities Graphics System.
        /// </summary>
        /// <param name="material">Material ID received from <see cref="RegisterMaterial"/></param>
        public void UnregisterMaterial(BatchMaterialID material) => m_BatchRendererGroup.UnregisterMaterial(material);

        /// <summary>
        /// Unregisters a mesh from the Entities Graphics System.
        /// </summary>
        /// <param name="mesh">A mesh ID received from <see cref="RegisterMesh"/>.</param>
        public void UnregisterMesh(BatchMeshID mesh) => m_BatchRendererGroup.UnregisterMesh(mesh);

        /// <summary>
        /// Returns the <see cref="Mesh"/> that corresponds to the given registered mesh ID, or <c>null</c> if no such mesh exists.
        /// </summary>
        /// <param name="mesh">A mesh ID received from <see cref="RegisterMesh"/>.</param>
        /// <returns>The <see cref="Mesh"/> object corresponding to the given mesh ID if the ID is valid, or <c>null</c> if it's not valid.</returns>
        public Mesh GetMesh(BatchMeshID mesh) => m_BatchRendererGroup.GetRegisteredMesh(mesh);

        /// <summary>
        /// Returns the <see cref="Material"/> that corresponds to the given registered material ID, or <c>null</c> if no such material exists.
        /// </summary>
        /// <param name="material">A material ID received from <see cref="RegisterMaterial"/>.</param>
        /// <returns>The <see cref="Material"/> object corresponding to the given material ID if the ID is valid, or <c>null</c> if it's not valid.</returns>
        public Material GetMaterial(BatchMaterialID material) => m_BatchRendererGroup.GetRegisteredMaterial(material);
        #endregion

        #region Managed Impl

        #endregion
    }
}

