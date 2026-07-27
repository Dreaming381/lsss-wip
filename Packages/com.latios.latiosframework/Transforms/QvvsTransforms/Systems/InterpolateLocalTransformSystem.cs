#if !LATIOS_TRANSFORMS_UNITY
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Latios.Transforms.Systems
{
    [UpdateInGroup(typeof(Latios.Systems.TickedInterpolateSuperSystem))]
    [RequireMatchingQueriesForUpdate]
    [DisableAutoCreation]
    [BurstCompile]
    public partial struct InterpolateLocalTransformSystem : ISystem, ILatiosApi
    {
        EntityQuery m_query;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            this.OnCreateForLatios(ref state);
            m_query = state.Fluent().With<InterpolateLocalTransformTag, TickedWorldTransform, TickedPreviousTransform>(true).With<WorldTransform>(false).Build();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var api = this.GetApi(ref state);
            var job = new Job
            {
                factor = api.worldBlackboardEntity.GetComponentData<TickingState>().finalTickFraction,
            }.Inject(api);
            var jh           = job.transformAspectHandleAccess.ScheduleChunkCaptureForQuery(m_query, state.Dependency);
            jh               = job.transformAspectHandleAccess.ScheduleChunkGrouping(jh);
            state.Dependency = job.GetTransformsScheduler().ScheduleParallel(jh);
        }

        [BurstCompile]
        partial struct Job : IJobChunk, IJobChunkParallelTransform, IInjectable
        {
            [ReadOnly, Inject] ComponentTypeHandle<TickedWorldTransform>              tickedWorldHandle;
            [ReadOnly, Inject] ComponentLookup<TickedWorldTransform>                  tickedWorldLookup;
            [ReadOnly, Inject] ComponentTypeHandle<TickedPreviousTransform>           previousTickedWorldHandle;
            [ReadOnly, Inject] ComponentLookup<TickedPreviousTransform>               previousTickedWorldLookup;
            [ReadOnly, Inject] ComponentTypeHandle<TickedPreviousLocalTransformCache> previousTickedLocalHandle;
            [Inject] TransformAspectParallelChunkHandle                               transformHandle;
            public float                                                              factor;

            public ref TransformAspectParallelChunkHandle transformAspectHandleAccess => ref transformHandle.RefAccess();

            public unsafe void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                var currentWorlds  = chunk.GetComponentDataPtrRO(ref tickedWorldHandle);
                var previousWorlds = chunk.GetComponentDataPtrRO(ref previousTickedWorldHandle);
                var previousLocals = chunk.GetComponentDataPtrRO(ref previousTickedLocalHandle);
                transformHandle.OnChunkBegin(in chunk, unfilteredChunkIndex, useEnabledMask, chunkEnabledMask);
                for (int i = 0; i < chunk.Count; i++)
                {
                    var currentTickedWorld  = currentWorlds[i];
                    var previousTickedWorld = previousWorlds[i];
                    var transform           = transformHandle.Deferable(i);
                    var handle              = transform.entityInHierarchyHandle;
                    if (handle.isNull || handle.isRoot)
                    {
                        InterpolateWorld(in previousTickedWorld, in currentTickedWorld, ref transform);
                    }
                    else
                    {
                        var parentHandle = handle.FindParent(transformHandle.entityStorageInfoLookup);
                        if (parentHandle.isNull)
                        {
                            InterpolateWorld(in previousTickedWorld, in currentTickedWorld, ref transform);
                        }
                        else
                        {
                            var parentWorld        = tickedWorldLookup[parentHandle.entity];
                            var parentPrevious     = previousTickedWorldLookup[parentHandle.entity];
                            var previousLocalCache = previousLocals[i];
                            var currentLocal       = WorldLocalOps.GetLocalTransformRO(in parentWorld.worldTransform,
                                                                                       in currentTickedWorld.worldTransform,
                                                                                       in parentHandle,
                                                                                       in handle,
                                                                                       true);
                            var previousLocal = WorldLocalOps.GetLocalTransformRO(in parentPrevious.worldTransform,
                                                                                  in previousTickedWorld.worldTransform,
                                                                                  in parentHandle,
                                                                                  in handle,
                                                                                  previousLocalCache.position,
                                                                                  previousLocalCache.scale);
                            var rotation                 = math.nlerp(previousLocal.rotation, currentLocal.rotation, factor);
                            var position                 = math.lerp(previousLocal.position, currentLocal.position, factor);
                            var scale                    = math.lerp(previousLocal.scale, currentLocal.scale, factor);
                            var stretch                  = math.lerp(previousTickedWorld.stretch, currentTickedWorld.stretch, factor);
                            var context32                = transform.context32;
                            transform.localTransformQvvs = new TransformQvvs(position, rotation, scale, stretch, context32);
                        }
                    }
                }
            }

            void InterpolateWorld(in TickedPreviousTransform previousTickedWorld, in TickedWorldTransform currentTickedWorld, ref TransformDeferableAspect transform)
            {
                var rotation             = math.nlerp(previousTickedWorld.rotation, currentTickedWorld.rotation, factor);
                var position             = math.lerp(previousTickedWorld.position, currentTickedWorld.position, factor);
                var scale                = math.lerp(previousTickedWorld.scale, currentTickedWorld.scale, factor);
                var stretch              = math.lerp(previousTickedWorld.stretch, currentTickedWorld.stretch, factor);
                var context32            = transform.context32;
                transform.worldTransform = new TransformQvvs(position, rotation, scale, stretch, context32);
            }
        }
    }
}
#endif

