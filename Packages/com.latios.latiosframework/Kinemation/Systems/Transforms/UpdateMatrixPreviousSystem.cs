#if !LATIOS_TRANSFORMS_UNITY
using Latios.Transforms.Systems;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Rendering;

namespace Latios.Kinemation.Systems
{
    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
    [UpdateInGroup(typeof(MotionHistoryInitializeSuperSystem))]
    [RequireMatchingQueriesForUpdate]
    [DisableAutoCreation]
    [BurstCompile]
    public partial struct InitializeMatrixPreviousSystem : ISystem, ILatiosApi
    {
        EntityQuery m_query;

        public void OnCreate(ref SystemState state)
        {
            this.OnCreateForLatios(ref state);
            m_query = state.Fluent().With<PostProcessMatrix>(true).With<PreviousPostProcessMatrix>().IncludeDisabledEntities().Build();
            m_query.SetOrderVersionFilter();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var api           = this.GetApi(ref state);
            state.Dependency = new UpdateMatricesJob().Inject(api).ScheduleParallel(m_query, state.Dependency);
        }

        [BurstCompile]
        partial struct UpdateMatricesJob : IJobChunk, IInjectable
        {
            [ReadOnly, Inject] ComponentTypeHandle<PostProcessMatrix> postProcessMatrixHandle;
            [Inject] ComponentTypeHandle<PreviousPostProcessMatrix>    previousPostProcessMatrixHandle;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                var current  = chunk.GetNativeArray(ref postProcessMatrixHandle).Reinterpret<float3x4>();
                var previous = chunk.GetNativeArray(ref previousPostProcessMatrixHandle).Reinterpret<float3x4>();
                for (int i = 0; i < previous.Length; i++)
                {
                    if (previous[i].Equals(float3x4.zero))
                        previous[i] = current[i];
                }
            }
        }
    }

    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
    [UpdateInGroup(typeof(MotionHistoryUpdateSuperSystem))]
    [RequireMatchingQueriesForUpdate]
    [DisableAutoCreation]
    [BurstCompile]
    public partial struct UpdateMatrixPreviousSystem : ISystem, ILatiosApi
    {
        EntityQuery m_query;

        public void OnCreate(ref SystemState state)
        {
            this.OnCreateForLatios(ref state);
            m_query = state.Fluent().With<PostProcessMatrix>(true).With<PreviousPostProcessMatrix>().IncludeDisabledEntities().Build();
            m_query.AddChangedVersionFilter(ComponentType.ReadOnly<PostProcessMatrix>());
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var api           = this.GetApi(ref state);
            state.Dependency = new UpdateMatricesJob().Inject(api).ScheduleParallel(m_query, state.Dependency);
        }

        [BurstCompile]
        partial struct UpdateMatricesJob : IJobChunk, IInjectable
        {
            [ReadOnly, Inject] ComponentTypeHandle<PostProcessMatrix> postProcessMatrixHandle;
            [Inject] ComponentTypeHandle<PreviousPostProcessMatrix>    previousPostProcessMatrixHandle;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                var current  = chunk.GetNativeArray(ref postProcessMatrixHandle).Reinterpret<float3x4>();
                var previous = chunk.GetNativeArray(ref previousPostProcessMatrixHandle).Reinterpret<float3x4>();
                previous.CopyFrom(current);
            }
        }
    }
}

#elif LATIOS_TRANSFORMS_UNITY
using Latios.Transforms.Systems;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;

namespace Latios.Kinemation.Systems
{
    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
    [UpdateInGroup(typeof(MotionHistoryUpdateSuperSystem))]
    [RequireMatchingQueriesForUpdate]
    [DisableAutoCreation]
    [BurstCompile]
    public partial struct UpdateMatrixPreviousSystem : ISystem, ILatiosApi
    {
        EntityQuery m_query;

        public void OnCreate(ref SystemState state)
        {
            this.OnCreateForLatios(ref state);
            m_query = state.Fluent().With<LocalToWorld>(true).With<BuiltinMaterialPropertyUnity_MatrixPreviousM>().IncludeDisabledEntities().Build();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var api           = this.GetApi(ref state);
            state.Dependency = new UpdateMatricesJob
            {
                lastSystemVersion = state.LastSystemVersion
            }.Inject(api).ScheduleParallel(m_query, state.Dependency);
        }

        [BurstCompile]
        partial struct UpdateMatricesJob : IJobChunk, IInjectable
        {
            [ReadOnly, Inject] ComponentTypeHandle<LocalToWorld> postProcessMatrixHandle;
            [Inject] ComponentTypeHandle<BuiltinMaterialPropertyUnity_MatrixPreviousM> previousPostProcessMatrixHandle;
            public uint lastSystemVersion;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                if (!chunk.DidChange(ref postProcessMatrixHandle, lastSystemVersion))
                    return;

                var current  = chunk.GetNativeArray(ref postProcessMatrixHandle).Reinterpret<float4x4>();
                var previous = chunk.GetNativeArray(ref previousPostProcessMatrixHandle).Reinterpret<float4x4>();
                previous.CopyFrom(current);
            }
        }
    }
}
#endif

