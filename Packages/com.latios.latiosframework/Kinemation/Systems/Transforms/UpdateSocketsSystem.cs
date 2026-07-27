#if LATIOS_TRANSFORMS_UNITY
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace Latios.Kinemation.Systems
{
    [RequireMatchingQueriesForUpdate]
    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
    [UpdateInGroup(typeof(TransformSystemGroup))]
    [UpdateBefore(typeof(LocalToWorldSystem))]
    [DisableAutoCreation]
    [BurstCompile]
    public partial struct UpdateSocketsSystem : ISystem, ILatiosApi
    {
        EntityQuery m_query;

        public void OnCreate(ref SystemState state)
        {
            this.OnCreateForLatios(ref state);
            m_query = state.Fluent().With<LocalTransform>(false).With<Socket>(true).With<BoneOwningSkeletonReference>(true).Build();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var api           = this.GetApi(ref state);
            state.Dependency = new CopyFromBoneJob
            {
                lastSystemVersion = state.LastSystemVersion
            }.Inject(api).ScheduleParallel(m_query, state.Dependency);
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state) {
        }

        [BurstCompile]
        partial struct CopyFromBoneJob : IJobChunk, IInjectable
        {
            [ReadOnly, Inject] ComponentTypeHandle<Socket> socketHandle;
            [ReadOnly, Inject] ComponentTypeHandle<BoneOwningSkeletonReference> skeletonHandle;
            [ReadOnly, Inject] BufferLookup<OptimizedBoneTransform> obtLookup;
            [ReadOnly, Inject] ComponentLookup<OptimizedSkeletonState> stateLookup;
            [Inject] ComponentTypeHandle<LocalTransform> transformHandle;

            public uint lastSystemVersion;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                var skeletons = chunk.GetNativeArray(ref skeletonHandle);

                if (!chunk.DidChange(ref socketHandle, lastSystemVersion) && !chunk.DidChange(ref skeletonHandle, lastSystemVersion))
                {
                    bool needsCopy = false;
                    for (int i = 0; i < chunk.Count; i++)
                    {
                        if (obtLookup.DidChange(skeletons[i].skeletonRoot, lastSystemVersion))
                        {
                            needsCopy = true;
                            break;;
                        }
                    }

                    if (!needsCopy)
                        return;
                }

                var sockets    = chunk.GetNativeArray(ref socketHandle);
                var transforms = chunk.GetNativeArray(ref transformHandle);

                for (int i = 0; i < chunk.Count; i++)
                {
                    var buffer    = obtLookup[skeletons[i].skeletonRoot];
                    var state     = stateLookup[skeletons[i].skeletonRoot].state;
                    var boneCount = buffer.Length / 6;
                    var mask      = (byte)(state & OptimizedSkeletonState.Flags.RotationMask);
                    int root;
                    if ((state & OptimizedSkeletonState.Flags.IsDirty) == OptimizedSkeletonState.Flags.IsDirty)
                    {
                        root = OptimizedSkeletonState.CurrentFromMask[mask];
                    }
                    else
                    {
                        root = OptimizedSkeletonState.PreviousFromMask[mask];
                    }
                    var srcTransform = buffer[sockets[i].boneIndex + root * boneCount * 2].boneTransform;
                    transforms[i] = new LocalTransform
                    {
                        Position = srcTransform.position,
                        Rotation = srcTransform.rotation,
                        Scale    = srcTransform.scale,
                    };
                }
            }
        }
    }
}
#endif

