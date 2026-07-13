#if !LATIOS_TRANSFORMS_UNITY
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

using static Unity.Entities.SystemAPI;

namespace Latios.Transforms.Systems
{
    [UpdateInGroup(typeof(Latios.Systems.TickedInterpolateSuperSystem))]
    [RequireMatchingQueriesForUpdate]
    [DisableAutoCreation]
    [BurstCompile]
    public partial struct InterpolateLocalTransformSystem : ISystem
    {
        LatiosWorldUnmanaged latiosWorld;
        EntityQuery          m_query;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            latiosWorld = state.GetLatiosWorldUnmanaged();
            m_query     = state.Fluent().With<InterpolateLocalTransformTag, TickedWorldTransform, TickedPreviousTransform>(true).With<WorldTransform>(false).Build();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var job = new Job
            {
                factor                    = latiosWorld.worldBlackboardEntity.GetComponentData<TickingState>().finalTickFraction,
                previousTickedLocalHandle = GetComponentTypeHandle<TickedPreviousLocalTransformCache>(true),
                previousTickedWorldHandle = GetComponentTypeHandle<TickedPreviousTransform>(true),
                previousTickedWorldLookup = GetComponentLookup<TickedPreviousTransform>(true),
                tickedWorldLookup         = GetComponentLookup<TickedWorldTransform>(true),
                tickedWorldHandle         = GetComponentTypeHandle<TickedWorldTransform>(true),
                transformHandle           = new TransformAspectParallelChunkHandle(GetComponentLookup<WorldTransform>(false),
                                                                                   GetComponentTypeHandle<RootReference>(true),
                                                                                   GetBufferLookup<EntityInHierarchy>(       true),
                                                                                   GetBufferLookup<EntityInHierarchyCleanup>(true),
                                                                                   GetEntityStorageInfoLookup(),
                                                                                   ref state)
            };
            var jh           = job.transformHandle.ScheduleChunkCaptureForQuery(m_query, state.Dependency);
            jh               = job.transformHandle.ScheduleChunkGrouping(jh);
            state.Dependency = job.GetTransformsScheduler().ScheduleParallel(jh);
        }

        [BurstCompile]
        struct Job : IJobChunk, IJobChunkParallelTransform
        {
            [ReadOnly] public ComponentTypeHandle<TickedWorldTransform>              tickedWorldHandle;
            [ReadOnly] public ComponentLookup<TickedWorldTransform>                  tickedWorldLookup;
            [ReadOnly] public ComponentTypeHandle<TickedPreviousTransform>           previousTickedWorldHandle;
            [ReadOnly] public ComponentLookup<TickedPreviousTransform>               previousTickedWorldLookup;
            [ReadOnly] public ComponentTypeHandle<TickedPreviousLocalTransformCache> previousTickedLocalHandle;
            public TransformAspectParallelChunkHandle                                transformHandle;
            public float                                                             factor;

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

