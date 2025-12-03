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

#if UNITY_EDITOR && !DISABLE_HYBRID_RENDERER_PICKING
#define ENABLE_PICKING
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

using MaterialPropertyType = Unity.Rendering.MaterialPropertyType;
using UnityEngine.XR;

namespace Latios.Kinemation.Systems
{
    public unsafe partial class LatiosEntitiesGraphicsSystem : SubSystem
    {
        private JobHandle OnPerformCulling(BatchRendererGroup rendererGroup, BatchCullingContext batchCullingContext, BatchCullingOutput cullingOutput, IntPtr userContext)
        {
            //return m_unmanaged.OnPerformCulling(ref CheckedStateRef, rendererGroup, batchCullingContext, cullingOutput, userContext);
            if (m_unmanaged.m_FirstFrameAfterInit)
                return default;

            using var         marker                    = m_latiosPerformCullingMarker.Auto();
            var               wrappedIncludeExcludeList = new WrappedPickingIncludeExcludeList(batchCullingContext.viewType);
            fixed (Unmanaged* unmanaged                 = &m_unmanaged)
            {
                DoOnPerformCullingBegin(unmanaged, ref CheckedStateRef, ref batchCullingContext, ref cullingOutput, ref wrappedIncludeExcludeList);
                SuperSystem.UpdateSystem(latiosWorldUnmanaged, m_cullingSuperSystem.SystemHandle);
                DoOnPerformCullingEnd(unmanaged, out var result);
                return result;
            }
        }

        static readonly ProfilerMarker m_latiosPerformCullingMarker = new ProfilerMarker("LatiosOnPerformCulling");

        [BurstCompile]
        static bool DoOnPerformCullingBegin(Unmanaged*                           unmanaged,
                                            ref SystemState state,
                                            ref BatchCullingContext batchCullingContext,
                                            ref BatchCullingOutput cullingOutput,
                                            ref WrappedPickingIncludeExcludeList wrappedIncludeExcludeList)
        {
            return unmanaged->OnPerformCullingBegin(ref state, ref batchCullingContext, ref cullingOutput, ref wrappedIncludeExcludeList);
        }

        [BurstCompile]
        static void DoOnPerformCullingEnd(Unmanaged* unmanaged, out JobHandle finalHandle)
        {
            finalHandle = unmanaged->OnPerformCullingEnd();
        }

        private unsafe void OnFinishedCulling(IntPtr customCullingResult)
        {
            m_unmanaged.OnFinishedCulling(ref CheckedStateRef, customCullingResult);
        }

        partial struct Unmanaged
        {
            public bool OnPerformCullingBegin(ref SystemState state,
                                              ref BatchCullingContext batchCullingContext,
                                              ref BatchCullingOutput cullingOutput,
                                              ref WrappedPickingIncludeExcludeList wrappedIncludeExcludeList)
            {
                cullingOutput.customCullingResult[0] = (IntPtr)m_cullPassIndexThisFrame;

                IncludeExcludeListFilter includeExcludeListFilter = GetPickingIncludeExcludeListFilterForCurrentCullingCallback(state.EntityManager,
                                                                                                                                batchCullingContext,
                                                                                                                                wrappedIncludeExcludeList,
                                                                                                                                m_ThreadLocalAllocators.GeneralAllocator->ToAllocator);

                // If inclusive filtering is enabled and we know there are no included entities,
                // we can skip all the work because we know that the result will be nothing.
                if (includeExcludeListFilter.IsIncludeEnabled && includeExcludeListFilter.IsIncludeEmpty)
                {
                    includeExcludeListFilter.Dispose();
                    return false;
                }

                latiosWorld.worldBlackboardEntity.SetComponentData(new CullingContext
                {
                    cullIndexThisFrame  = m_cullPassIndexThisFrame,
                    cullingFlags        = batchCullingContext.cullingFlags,
                    cullingLayerMask    = batchCullingContext.cullingLayerMask,
                    localToWorldMatrix  = batchCullingContext.localToWorldMatrix,
                    lodParameters       = batchCullingContext.lodParameters,
                    projectionType      = batchCullingContext.projectionType,
                    receiverPlaneCount  = batchCullingContext.receiverPlaneCount,
                    receiverPlaneOffset = batchCullingContext.receiverPlaneOffset,
                    sceneCullingMask    = batchCullingContext.sceneCullingMask,
                    viewID              = batchCullingContext.viewID,
                    viewType            = batchCullingContext.viewType,
                });
                latiosWorld.worldBlackboardEntity.SetComponentData(new DispatchContext
                {
                    globalSystemVersionOfLatiosEntitiesGraphics = m_globalSystemVersionAtLastUpdate,
                    lastSystemVersionOfLatiosEntitiesGraphics   = m_LastSystemVersionAtLastUpdate,
                    dispatchIndexThisFrame                      = m_dispatchPassIndexThisFrame
                });

                var cullingPlanesBuffer = latiosWorld.worldBlackboardEntity.GetBuffer<CullingPlane>(false);
                cullingPlanesBuffer.Clear();
                cullingPlanesBuffer.Reinterpret<Plane>().AddRange(batchCullingContext.cullingPlanes);
                var splitsBuffer = latiosWorld.worldBlackboardEntity.GetBuffer<CullingSplitElement>(false);
                splitsBuffer.Clear();
                splitsBuffer.Reinterpret<CullingSplit>().AddRange(batchCullingContext.cullingSplits);

                latiosWorld.worldBlackboardEntity.SetCollectionComponentAndDisposeOld(new BrgCullingContext
                {
                    cullingThreadLocalAllocator                          = m_ThreadLocalAllocators,
                    batchCullingOutput                                   = cullingOutput,
                    batchFilterSettingsByRenderFilterSettingsSharedIndex = m_FilterSettings,
                    // To be able to access the material/mesh IDs, we need access to the registered material/mesh
                    // arrays. If we can't get them, then we simply skip in those cases.
                    brgRenderMeshArrays = m_brgRenderMeshArrays,

#if UNITY_EDITOR
                    includeExcludeListFilter = includeExcludeListFilter,
#endif
                });
                latiosWorld.worldBlackboardEntity.UpdateJobDependency<BrgCullingContext>(default, false);
                return true;
            }

            public JobHandle OnPerformCullingEnd()
            {
                var worldBlackboardEntity = latiosWorld.worldBlackboardEntity;
                latiosWorld.GetCollectionComponent<BrgCullingContext>(worldBlackboardEntity, out var finalHandle);
                worldBlackboardEntity.UpdateJobDependency<BrgCullingContext>(finalHandle, false);
                m_cullingCallbackFinalJobHandles.Add(finalHandle);
                m_cullPassIndexThisFrame++;
                return finalHandle;
            }

            public unsafe void OnFinishedCulling(ref SystemState state, IntPtr customCullingResult)
            {
                //UnityEngine.Debug.Log($"OnFinishedCulling pass {(int)customCullingResult}");

                if (m_FirstFrameAfterInit || m_cullPassIndexThisFrame == m_cullPassIndexForLastDispatch)
                    return;

                m_cullingDispatchSuperSystem.Update(state.WorldUnmanaged);
                m_cullPassIndexForLastDispatch = m_cullPassIndexThisFrame;
                m_dispatchPassIndexThisFrame++;
                if (m_dispatchPassIndexThisFrame > 1024)
                {
                    JobHandle.CompleteAll(m_cullingCallbackFinalJobHandles.AsArray());
                    m_ThreadLocalAllocators.Rewind();
                }
            }
        }

        struct WrappedPickingIncludeExcludeList
        {
#if ENABLE_PICKING && !DISABLE_INCLUDE_EXCLUDE_LIST_FILTERING
            public PickingIncludeExcludeList includeExcludeList;

            public WrappedPickingIncludeExcludeList(BatchCullingViewType viewType)
            {
                includeExcludeList = default;
                if (viewType == BatchCullingViewType.Picking)
                    includeExcludeList = HandleUtility.GetPickingIncludeExcludeList(Allocator.Temp);
                else if (viewType == BatchCullingViewType.SelectionOutline)
                    includeExcludeList = HandleUtility.GetSelectionOutlineIncludeExcludeList(Allocator.Temp);
            }
#else
            public WrappedPickingIncludeExcludeList(BatchCullingViewType viewType)
            {
            }
#endif
        }

        // This function does only return a meaningful IncludeExcludeListFilter object when called from a BRG culling callback.
        static IncludeExcludeListFilter GetPickingIncludeExcludeListFilterForCurrentCullingCallback(EntityManager entityManager,
                                                                                                    in BatchCullingContext cullingContext,
                                                                                                    WrappedPickingIncludeExcludeList wrappedIncludeExcludeList,
                                                                                                    Allocator allocator)
        {
#if ENABLE_PICKING && !DISABLE_INCLUDE_EXCLUDE_LIST_FILTERING
            PickingIncludeExcludeList includeExcludeList = wrappedIncludeExcludeList.includeExcludeList;

            NativeArray<int> emptyArray = new NativeArray<int>(0, Allocator.Temp);

            NativeArray<int> includeEntityIndices = includeExcludeList.IncludeEntities;
            if (cullingContext.viewType == BatchCullingViewType.SelectionOutline)
            {
                // Make sure the include list for the selection outline is never null even if there is nothing in it.
                // Null NativeArray and empty NativeArray are treated as different things when used to construct an IncludeExcludeListFilter object:
                // - Null include list means that nothing is discarded because the filtering is skipped.
                // - Empty include list means that everything is discarded because the filtering is enabled but never passes.
                // With selection outline culling, we want the filtering to happen in any case even if the array contains nothing so that we don't highlight everything in the latter case.
                if (!includeEntityIndices.IsCreated)
                    includeEntityIndices = emptyArray;
            }
            else if (includeEntityIndices.Length == 0)
            {
                includeEntityIndices = default;
            }

            NativeArray<int> excludeEntityIndices = includeExcludeList.ExcludeEntities;
            if (excludeEntityIndices.Length == 0)
                excludeEntityIndices = default;

            IncludeExcludeListFilter includeExcludeListFilter = new IncludeExcludeListFilter(
                entityManager,
                includeEntityIndices,
                excludeEntityIndices,
                allocator);

            return includeExcludeListFilter;
#else
            return default;
#endif
        }
    }
}

