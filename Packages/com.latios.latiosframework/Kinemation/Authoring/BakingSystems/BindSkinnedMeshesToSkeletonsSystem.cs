using Latios.Transforms.Authoring;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

using static Unity.Entities.SystemAPI;

namespace Latios.Kinemation.Authoring.Systems
{
    [RequireMatchingQueriesForUpdate]
    [DisableAutoCreation]
    [BurstCompile]
    public partial struct BindSkinnedMeshesToSkeletonsSystem : ISystem
    {
        [BakingType] struct IdentityRequestTag : IRequestCopyParentTransform { }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            new ClearJob().ScheduleParallel();

            var ecbAdd                 = new EntityCommandBuffer(state.WorldUpdateAllocator);
            var bindSkeletonRootLookup = GetComponentLookup<BindSkeletonRoot>(false);
            new BindJob
            {
                ecb        = ecbAdd.AsParallelWriter(),
                rootLookup = bindSkeletonRootLookup
            }.ScheduleParallel();

            var ecbRemove       = new EntityCommandBuffer(state.WorldUpdateAllocator);
            new UnbindJob { ecb = ecbRemove.AsParallelWriter() }.ScheduleParallel();

            state.CompleteDependency();

            ecbAdd.Playback(state.EntityManager);
            ecbRemove.Playback(state.EntityManager);
        }

        [WithOptions(EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.IncludePrefab)]
        [BurstCompile]
        partial struct ClearJob : IJobEntity
        {
            public void Execute(ref BindSkeletonRoot root)
            {
                root.root = Entity.Null;
            }
        }

        [WithOptions(EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.IncludePrefab)]
        [BurstCompile]
        partial struct BindJob : IJobEntity
        {
            [NativeDisableParallelForRestriction] public ComponentLookup<BindSkeletonRoot> rootLookup;
            public EntityCommandBuffer.ParallelWriter                                      ecb;

            public void Execute(Entity entity, [ChunkIndexInQuery] int chunkIndexInQuery, in DynamicBuffer<AutoBindSkinnedMeshToSkeleton> skinnedMeshes)
            {
                foreach (var skinnedMesh in skinnedMeshes)
                {
                    if (rootLookup.HasComponent(skinnedMesh.skinnedMeshEntity))
                    {
                        rootLookup[skinnedMesh.skinnedMeshEntity] = new BindSkeletonRoot { root = entity };
                    }
                    else
                    {
                        ecb.AddComponent(                    chunkIndexInQuery, skinnedMesh.skinnedMeshEntity, new BindSkeletonRoot { root = entity });
                        ecb.AddComponent<IdentityRequestTag>(chunkIndexInQuery, skinnedMesh.skinnedMeshEntity);
                    }
                }
            }
        }

        [WithOptions(EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.IncludePrefab)]
        [BurstCompile]
        partial struct UnbindJob : IJobEntity
        {
            public EntityCommandBuffer.ParallelWriter ecb;

            public void Execute(Entity entity, [ChunkIndexInQuery] int chunkIndexInQuery, ref BindSkeletonRoot root)
            {
                if (root.root == Entity.Null)
                {
                    ecb.RemoveComponent<BindSkeletonRoot>(  chunkIndexInQuery, entity);
                    ecb.RemoveComponent<IdentityRequestTag>(chunkIndexInQuery, entity);
                }
            }
        }
    }
}

