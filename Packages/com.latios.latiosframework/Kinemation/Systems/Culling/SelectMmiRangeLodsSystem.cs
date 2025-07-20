using Latios;
using Latios.Transforms;
using Latios.Transforms.Abstract;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Exposed;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Rendering;

using static Unity.Entities.SystemAPI;

namespace Latios.Kinemation
{
    [RequireMatchingQueriesForUpdate]
    [DisableAutoCreation]
    [BurstCompile]
    public partial struct SelectMmiRangeLodsSystem : ISystem, ISystemShouldUpdate
    {
        LatiosWorldUnmanaged                    latiosWorld;
        WorldTransformReadOnlyAspect.TypeHandle m_worldTransformHandle;

        EntityQuery m_query;

        int   m_maximumLODLevel;
        float m_lodBias;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            latiosWorld            = state.GetLatiosWorldUnmanaged();
            m_worldTransformHandle = new WorldTransformReadOnlyAspect.TypeHandle(ref state);

            m_query = state.Fluent().With<MaterialMeshInfo, LodCrossfade>(false).With<UseMmiRangeLodTag>(true)
                      .WithAnyEnabled<MmiRange2LodSelect, MmiRange3LodSelect>(true).WithWorldTransformReadOnly().Build();
        }

        public bool ShouldUpdateSystem(ref SystemState state)
        {
            m_maximumLODLevel = UnityEngine.QualitySettings.maximumLODLevel;
            m_lodBias         = UnityEngine.QualitySettings.lodBias;
            return m_maximumLODLevel < 2;
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var parameters = latiosWorld.worldBlackboardEntity.GetComponentData<CullingContext>().lodParameters;

            m_worldTransformHandle.Update(ref state);

            state.Dependency = new Job
            {
                perCameraMaskHandle   = GetComponentTypeHandle<ChunkPerCameraCullingMask>(false),
                worldTransformHandle  = m_worldTransformHandle,
                boundsHandle          = GetComponentTypeHandle<WorldRenderBounds>(true),
                select2Handle         = GetComponentTypeHandle<MmiRange2LodSelect>(true),
                select3Handle         = GetComponentTypeHandle<MmiRange3LodSelect>(true),
                lodGroupCrossfades    = GetComponentTypeHandle<LodHeightPercentagesWithCrossfadeMargins>(true),
                mmiHandle             = GetComponentTypeHandle<MaterialMeshInfo>(false),
                crossfadeHandle       = GetComponentTypeHandle<LodCrossfade>(false),
                cameraPosition        = parameters.cameraPosition,
                isPerspective         = !parameters.isOrthographic,
                cameraFactor          = LodUtilities.CameraFactorFrom(in parameters, m_lodBias),
                maxResolutionLodLevel = m_maximumLODLevel
            }.ScheduleParallel(m_query, state.Dependency);
        }

        [BurstCompile]
        unsafe struct Job : IJobChunk
        {
            [ReadOnly] public WorldTransformReadOnlyAspect.TypeHandle                       worldTransformHandle;
            [ReadOnly] public ComponentTypeHandle<WorldRenderBounds>                        boundsHandle;
            [ReadOnly] public ComponentTypeHandle<MmiRange2LodSelect>                       select2Handle;
            [ReadOnly] public ComponentTypeHandle<MmiRange3LodSelect>                       select3Handle;
            [ReadOnly] public ComponentTypeHandle<LodHeightPercentagesWithCrossfadeMargins> lodGroupCrossfades;

            public ComponentTypeHandle<ChunkPerCameraCullingMask> perCameraMaskHandle;
            public ComponentTypeHandle<MaterialMeshInfo>          mmiHandle;
            public ComponentTypeHandle<LodCrossfade>              crossfadeHandle;

            public float3 cameraPosition;
            public float  cameraFactor;
            public int    maxResolutionLodLevel;
            public bool   isPerspective;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                ref var mask = ref chunk.GetChunkComponentRefRW(ref perCameraMaskHandle);
                if ((mask.upper.Value | mask.lower.Value) == 0)
                    return;

                var transforms          = worldTransformHandle.Resolve(chunk);
                var boundsArray         = (WorldRenderBounds*)chunk.GetRequiredComponentDataPtrRO(ref boundsHandle);
                var mmis                = (MaterialMeshInfo*)chunk.GetRequiredComponentDataPtrRW(ref mmiHandle);
                var crossfades          = (LodCrossfade*)chunk.GetRequiredComponentDataPtrRW(ref crossfadeHandle);
                var crossfadesEnabled   = chunk.GetEnabledMask(ref crossfadeHandle);
                var select2s            = chunk.GetComponentDataPtrRO(ref select2Handle);
                var select3s            = chunk.GetComponentDataPtrRO(ref select3Handle);
                var lodGroupPercentages = chunk.GetComponentDataPtrRO(ref lodGroupCrossfades);
                var enumerator          = new ChunkEntityEnumerator(true, new v128(mask.lower.Value, mask.upper.Value), chunk.Count);
                while (enumerator.NextEntityIndex(out int i))
                {
                    MmiRange3LodSelect select;
                    MaterialMeshInfo   mmi;
                    int                maxLodSupported;
                    if (select3s != null)
                    {
                        select          = select3s[i];
                        mmi             = mmis[i];
                        maxLodSupported = 2;
                    }
                    else if (select2s != null)
                    {
                        select = new MmiRange3LodSelect
                        {
                            fullLod0ScreenHeightFraction    = select2s[i].fullLod0ScreenHeightFraction,
                            fullLod1ScreenHeightMaxFraction = (half)math.abs(select2s[i].fullLod1ScreenHeightFraction),
                            fullLod1ScreenHeightMinFraction = default,
                            fullLod2ScreenHeightFraction    = (half)math.select(0f, -1f, select2s[i].fullLod1ScreenHeightFraction < 0f),
                        };
                        mmi             = mmis[i];
                        maxLodSupported = 1;
                    }
                    else
                    {
                        select = new MmiRange3LodSelect
                        {
                            fullLod0ScreenHeightFraction    = default,
                            fullLod1ScreenHeightMaxFraction = default,
                            fullLod1ScreenHeightMinFraction = default,
                            fullLod2ScreenHeightFraction    = default,
                        };
                        mmi             = default;
                        maxLodSupported = 0;
                    }

                    float height = math.cmax(boundsArray[i].Value.Extents) * 2f;
                    float groupMin, groupMax;
                    if (lodGroupPercentages != null)
                    {
                        var   transform        = transforms[i].worldTransformQvvs;
                        float groupWorldHeight = math.abs(lodGroupPercentages[i].localSpaceHeight) * math.abs(transform.scale) * math.cmax(math.abs(transform.stretch));
                        float factor           = height / groupWorldHeight;
                        groupMin               = factor * lodGroupPercentages[i].minCrossFadeEdge;
                        groupMax               = factor * lodGroupPercentages[i].maxCrossFadeEdge;
                    }
                    else
                    {
                        groupMin = 0f;
                        groupMax = float.MaxValue;
                    }

                    DoEntity(ref mmi,
                             ref crossfades[i],
                             out var crossfadeEnabled,
                             out var cull,
                             select,
                             transforms[i].worldTransformQvvs,
                             height,
                             groupMin,
                             groupMax,
                             maxLodSupported);

                    if (cull)
                        mask.ClearBitAtIndex(i);
                    crossfadesEnabled[i] = crossfadeEnabled;
                    if (select2s != null || select3s != null)
                        mmis[i] = mmi;
                }
            }

            void DoEntity(ref MaterialMeshInfo mmi,
                          ref LodCrossfade crossfade,
                          out bool crossfadeEnabled,
                          out bool cull,
                          MmiRange3LodSelect select,
                          in TransformQvvs transform,
                          float height,
                          float groupMin,
                          float groupMax,
                          int maxLodSupported)
            {
                cull       = false;
                int minLod = 0;
                int maxLod = 2;
                if (select.fullLod1ScreenHeightMinFraction < groupMin)
                    maxLod = 0;
                else if (select.fullLod2ScreenHeightFraction < groupMin)
                    maxLod = 1;
                if (select.fullLod1ScreenHeightMaxFraction > groupMax)
                    minLod = 2;
                else if (select.fullLod0ScreenHeightFraction > groupMax)
                    minLod = 1;
                minLod     = math.max(minLod, maxResolutionLodLevel);
                maxLod     = math.max(maxLod, maxResolutionLodLevel);
                minLod     = math.min(minLod, maxLodSupported);
                maxLod     = math.min(maxLod, maxLodSupported);
                minLod     = math.min(minLod, maxLodSupported);
                if (minLod == maxLod)
                {
                    crossfadeEnabled = false;
                    mmi.SetCurrentLodRegion(minLod, false);
                    return;
                }
                else
                {
                    if (minLod == 1)
                    {
                        select.fullLod1ScreenHeightMaxFraction = half.MaxValueAsHalf;
                        select.fullLod0ScreenHeightFraction    = half.MaxValueAsHalf;
                    }
                    else if (maxLod == 1)
                    {
                        select.fullLod1ScreenHeightMinFraction = default;
                        select.fullLod2ScreenHeightFraction    = (half)math.min(select.fullLod2ScreenHeightFraction, 0f);
                    }
                }

                height       *= cameraFactor;
                var distance  = math.select(1f, math.distance(transform.position, cameraPosition), isPerspective);

                var zeroHeight   = select.fullLod0ScreenHeightFraction * distance;
                var oneMaxHeight = select.fullLod1ScreenHeightMaxFraction * distance;
                var oneMinHeight = select.fullLod1ScreenHeightMinFraction * distance;
                var twoHeight    = select.fullLod2ScreenHeightFraction * distance;

                if (height >= zeroHeight)
                {
                    crossfadeEnabled = false;
                    mmi.SetCurrentLodRegion(0, false);
                }
                else if (height <= twoHeight)
                {
                    crossfadeEnabled = false;
                    mmi.SetCurrentLodRegion(2, false);
                    if (select.fullLod2ScreenHeightFraction < 0f)
                        cull = true;
                }
                else if ((height <= oneMaxHeight) && height >= oneMinHeight)
                {
                    crossfadeEnabled = false;
                    mmi.SetCurrentLodRegion(1, false);
                }
                else if (height > oneMaxHeight)
                {
                    crossfadeEnabled = true;
                    mmi.SetCurrentLodRegion(0, true);
                    crossfade.SetFromHiResOpacity(math.unlerp(oneMaxHeight, zeroHeight, height), false);
                }
                else
                {
                    crossfadeEnabled = true;
                    mmi.SetCurrentLodRegion(1, true);
                    crossfade.SetFromHiResOpacity(math.unlerp(twoHeight, oneMinHeight, height), false);
                }
            }
        }
    }
}

