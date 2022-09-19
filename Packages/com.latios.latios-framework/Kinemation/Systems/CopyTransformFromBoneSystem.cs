using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace Latios.Kinemation.Systems
{
    [UpdateInGroup(typeof(TransformSystemGroup))]
    [UpdateAfter(typeof(TRSToLocalToParentSystem))]
    [UpdateBefore(typeof(TRSToLocalToWorldSystem))]
    [DisableAutoCreation]
    [BurstCompile]
    public partial struct CopyTransformFromBoneSystem : ISystem
    {
        EntityQuery m_query;

        public void OnCreate(ref SystemState state)
        {
            m_query = state.Fluent().WithAll<LocalToParent>(false).WithAll<CopyLocalToParentFromBone>(true).WithAll<BoneOwningSkeletonReference>(true).Build();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.Dependency = new CopyFromBoneJob
            {
                fromBoneHandle    = state.GetComponentTypeHandle<CopyLocalToParentFromBone>(true),
                skeletonHandle    = state.GetComponentTypeHandle<BoneOwningSkeletonReference>(true),
                btrBfe            = state.GetBufferFromEntity<OptimizedBoneToRoot>(true),
                ltpHandle         = state.GetComponentTypeHandle<LocalToParent>(false),
                lastSystemVersion = state.LastSystemVersion
            }.ScheduleParallel(m_query, state.Dependency);
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state) {
        }

        [BurstCompile]
        struct CopyFromBoneJob : IJobEntityBatch
        {
            [ReadOnly] public ComponentTypeHandle<CopyLocalToParentFromBone>   fromBoneHandle;
            [ReadOnly] public ComponentTypeHandle<BoneOwningSkeletonReference> skeletonHandle;
            [ReadOnly] public BufferFromEntity<OptimizedBoneToRoot>            btrBfe;
            public ComponentTypeHandle<LocalToParent>                          ltpHandle;

            public uint lastSystemVersion;

            public void Execute(ArchetypeChunk batchInChunk, int batchIndex)
            {
                var skeletons = batchInChunk.GetNativeArray(skeletonHandle);

                if (!batchInChunk.DidChange(fromBoneHandle, lastSystemVersion) && !batchInChunk.DidChange(skeletonHandle, lastSystemVersion))
                {
                    bool needsCopy = false;
                    for (int i = 0; i < batchInChunk.Count; i++)
                    {
                        if (btrBfe.DidChange(skeletons[i].skeletonRoot, lastSystemVersion))
                        {
                            needsCopy = true;
                            break;;
                        }
                    }

                    if (!needsCopy)
                        return;
                }

                var bones = batchInChunk.GetNativeArray(fromBoneHandle);
                var ltps  = batchInChunk.GetNativeArray(ltpHandle).Reinterpret<float4x4>();

                for (int i = 0; i < batchInChunk.Count; i++)
                {
                    var buffer = btrBfe[skeletons[i].skeletonRoot];
                    ltps[i]    = buffer[bones[i].boneIndex].boneToRoot;
                }
            }
        }
    }
}

