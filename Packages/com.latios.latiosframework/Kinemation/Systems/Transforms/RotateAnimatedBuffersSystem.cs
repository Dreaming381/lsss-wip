using Latios.Transforms.Systems;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Entities;
using Unity.Jobs;

namespace Latios.Kinemation.Systems
{
    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
    [UpdateInGroup(typeof(MotionHistoryUpdateSuperSystem))]
    [RequireMatchingQueriesForUpdate]
    [DisableAutoCreation]
    [BurstCompile]
    public partial struct RotateAnimatedBuffersSystem : ISystem, ILatiosApi
    {
        EntityQuery m_skeletonsQuery;
        EntityQuery m_blendShapesQuery;
        EntityQuery m_dynamicMeshesQuery;

        public void OnCreate(ref SystemState state)
        {
            this.OnCreateForLatios(ref state);
            m_skeletonsQuery     = state.Fluent().With<OptimizedSkeletonState>().With<OptimizedBoneTransform>(true).IncludeDisabledEntities().Build();
            m_blendShapesQuery   = state.Fluent().With<BlendShapeState>().With<BlendShapeWeight>(true).IncludeDisabledEntities().Build();
            m_dynamicMeshesQuery = state.Fluent().With<DynamicMeshState>().With<DynamicMeshVertex>(true).IncludeDisabledEntities().Build();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var api        = this.GetApi(ref state);
            var baseJh     = state.Dependency;
            var skeletonJh = new SkeletonJob().Inject(api).ScheduleParallel(m_skeletonsQuery, baseJh);

            var blendShapeJh = new BlendShapesJob().Inject(api).ScheduleParallel(m_blendShapesQuery, baseJh);

            var meshJh = new MeshJob().Inject(api).ScheduleParallel(m_dynamicMeshesQuery, baseJh);

            state.Dependency = JobHandle.CombineDependencies(skeletonJh, blendShapeJh, meshJh);
        }

        [BurstCompile]
        partial struct SkeletonJob : IJobChunk, IInjectable
        {
            [Inject] ComponentTypeHandle<OptimizedSkeletonState> stateHandle;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                var states = chunk.GetNativeArray(ref stateHandle);

                for (int i = 0; i < chunk.Count; i++)
                {
                    var  state    = states[i].state;
                    var  rotation = (byte)state & 0x3;
                    bool wasDirty = (state & OptimizedSkeletonState.Flags.IsDirty) == OptimizedSkeletonState.Flags.IsDirty;
                    if (wasDirty)
                    {
                        rotation++;
                        if (rotation >= 3)
                            rotation = 0;
                    }
                    state = (OptimizedSkeletonState.Flags)rotation;
                    if (wasDirty)
                        state |= OptimizedSkeletonState.Flags.WasPreviousDirty;
                    states[i]  = new OptimizedSkeletonState { state = state };
                }
            }
        }

        [BurstCompile]
        partial struct BlendShapesJob : IJobChunk, IInjectable
        {
            [Inject] ComponentTypeHandle<BlendShapeState> stateHandle;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                var states = chunk.GetNativeArray(ref stateHandle);

                for (int i = 0; i < chunk.Count; i++)
                {
                    var  state    = states[i].state;
                    var  rotation = (byte)state & 0x3;
                    bool wasDirty = (state & BlendShapeState.Flags.IsDirty) == BlendShapeState.Flags.IsDirty;
                    if (wasDirty)
                    {
                        rotation++;
                        if (rotation >= 3)
                            rotation = 0;
                    }
                    state = (BlendShapeState.Flags)rotation;
                    if (wasDirty)
                        state |= BlendShapeState.Flags.WasPreviousDirty;
                    states[i]  = new BlendShapeState { state = state };
                }
            }
        }

        [BurstCompile]
        partial struct MeshJob : IJobChunk, IInjectable
        {
            [Inject] ComponentTypeHandle<DynamicMeshState> stateHandle;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                var states = chunk.GetNativeArray(ref stateHandle);

                for (int i = 0; i < chunk.Count; i++)
                {
                    var  state    = states[i].state;
                    var  rotation = (byte)state & 0x3;
                    bool wasDirty = (state & DynamicMeshState.Flags.IsDirty) == DynamicMeshState.Flags.IsDirty;
                    if (wasDirty)
                    {
                        rotation++;
                        if (rotation >= 3)
                            rotation = 0;
                    }
                    state = (DynamicMeshState.Flags)rotation;
                    if (wasDirty)
                        state |= DynamicMeshState.Flags.WasPreviousDirty;
                    states[i]  = new DynamicMeshState { state = state };
                }
            }
        }
    }
}

