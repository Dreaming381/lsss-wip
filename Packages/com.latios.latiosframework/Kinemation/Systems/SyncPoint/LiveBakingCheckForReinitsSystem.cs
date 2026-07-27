using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Exposed;
using Unity.Jobs;
using Unity.Mathematics;

namespace Latios.Kinemation.Systems
{
    [RequireMatchingQueriesForUpdate]
    [DisableAutoCreation]
    [BurstCompile]
    public partial struct LiveBakingCheckForReinitsSystem : ISystem, ILatiosApi
    {
        EntityQuery m_reinitMeshesQuery;
        EntityQuery m_unityTransformsBindSkeletonRootsQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
#if !UNITY_EDITOR
            state.Enabled = false;
            return;
#endif
            this.OnCreateForLatios(ref state);
            m_reinitMeshesQuery = state.Fluent().With<MeshDeformDataBlobReference, BoundMesh, LiveBakedTag>(true).Build();
            m_reinitMeshesQuery.AddChangedVersionFilter(ComponentType.ReadOnly<MeshDeformDataBlobReference>());
            m_unityTransformsBindSkeletonRootsQuery = state.Fluent().With<Unity.Transforms.LocalTransform>(false).With<BindSkeletonRoot, LiveBakedTag>(true).Build();
            m_unityTransformsBindSkeletonRootsQuery.AddChangedVersionFilter(ComponentType.ReadWrite<Unity.Transforms.LocalTransform>());
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var api                = this.GetApi(ref state);
            var lastSystemVersion = api.worldBlackboardEntity.GetComponentData<SystemVersionBeforeLiveBake>().version;
            m_reinitMeshesQuery.SetOverrideChangeFilterVersion(lastSystemVersion);
            m_unityTransformsBindSkeletonRootsQuery.SetOverrideChangeFilterVersion(lastSystemVersion);
            var ecb          = new EntityCommandBuffer(state.WorldUpdateAllocator);
            state.Dependency = new Job
            {
                ecb = ecb.AsParallelWriter()
            }.Inject(api).ScheduleParallel(m_reinitMeshesQuery, state.Dependency);
            state.CompleteDependency();
            ecb.Playback(state.EntityManager);

            if (!m_unityTransformsBindSkeletonRootsQuery.IsEmptyIgnoreFilter)
            {
                // Clean up Unity local transforms that we parent to skeletons in case we don't rebind but transforms get rebaked and diffed over.
                state.Dependency = new CleanupUnityLocalTransformsJob().Inject(api).ScheduleParallel(m_unityTransformsBindSkeletonRootsQuery, default);
            }
        }

        [BurstCompile]
        partial struct Job : IJobChunk, IInjectable
        {
            [ReadOnly, Inject] EntityTypeHandle                                 entityHandle;
            [ReadOnly, Inject] ComponentTypeHandle<MeshDeformDataBlobReference> meshReferenceHandle;
            [ReadOnly, Inject] ComponentTypeHandle<BoundMesh>                   boundMeshHandle;

            public EntityCommandBuffer.ParallelWriter ecb;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                var entities = chunk.GetNativeArray(entityHandle);
                var old      = chunk.GetNativeArray(ref boundMeshHandle);
                var target   = chunk.GetNativeArray(ref meshReferenceHandle);

                for (int i = 0; i < chunk.Count; i++)
                {
                    if (old[i].meshBlob != target[i].blob)
                        ecb.AddComponent<BoundMeshNeedsReinit>(unfilteredChunkIndex, entities[i]);
                }
            }
        }

        [BurstCompile]
        partial struct CleanupUnityLocalTransformsJob : IJobChunk, IInjectable
        {
            [Inject] ComponentTypeHandle<Unity.Transforms.LocalTransform> localTransformHandle;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                var locals = chunk.GetNativeArray(ref localTransformHandle);
                for (int i = 0; i < chunk.Count; i++)
                {
                    locals[i] = Unity.Transforms.LocalTransform.Identity;
                }
            }
        }
    }
}

