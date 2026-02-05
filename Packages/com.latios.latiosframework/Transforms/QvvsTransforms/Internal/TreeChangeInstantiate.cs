using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Exposed;
using Unity.Jobs;
using Unity.Mathematics;

namespace Latios.Transforms
{
    internal static class TreeChangeInstantiate
    {
        public static void AddChildren(ref IInstantiateCommand.Context context)
        {
            var entities        = context.entities;
            var em              = context.entityManager;
            var childWorkStates = new NativeArray<ChildWorkState>(entities.Length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            for (int i = 0; i < entities.Length; i++)
            {
                var command        = context.ReadCommand<ParentCommand>(i);
                childWorkStates[i] = new ChildWorkState
                {
                    child   = entities[i],
                    parent  = command.parent,
                    flags   = command.inheritanceFlags,
                    options = command.options
                };
            }

            var classifyJh = new ClassifyJob
            {
                children              = childWorkStates,
                esil                  = em.GetEntityStorageInfoLookup(),
                transformLookup       = em.GetComponentLookup<WorldTransform>(true),
                tickedTransformLookup = em.GetComponentLookup<TickedWorldTransform>(true)
            }.ScheduleParallel(childWorkStates.Length, 32, default);
            classifyJh.Complete();

            for (int i = 0; i < childWorkStates.Length; i++)
            {
                var child = childWorkStates[i];
                if (child.parentIsDead)
                {
                    context.RequestDestroyEntity(child.child);
                    continue;
                }
                em.AddChild(child.parent, child.child, child.flags, child.options);
                if (em.HasComponent<WorldTransform>(child.child))
                    TransformTools.SetLocalTransform(child.child, in child.localTransform, em);
                if (em.HasComponent<TickedWorldTransform>(child.child))
                    TransformTools.SetTickedLocalTransform(child.child, in child.tickedLocalTransform, em);
            }

            childWorkStates.Dispose();
        }

        #region Types
        struct ChildWorkState
        {
            public Entity           parent;
            public Entity           child;
            public InheritanceFlags flags;
            public AddChildOptions  options;
            public bool             parentIsDead;
            public TransformQvvs    localTransform;
            public TransformQvvs    tickedLocalTransform;
        }

        struct RootWorkState
        {
        }
        #endregion

        #region Jobs
        [BurstCompile]
        struct ClassifyJob : IJobFor
        {
            public NativeArray<ChildWorkState> children;

            [ReadOnly] public ComponentLookup<WorldTransform>       transformLookup;
            [ReadOnly] public ComponentLookup<TickedWorldTransform> tickedTransformLookup;
            [ReadOnly] public EntityStorageInfoLookup               esil;

            public void Execute(int i)
            {
                var workState          = children[i];
                workState.parentIsDead = !esil.IsAlive(workState.parent);
                if (workState.parentIsDead)
                {
                    children[i] = new ChildWorkState { parentIsDead = true };
                    return;
                }

                bool hadNormal                 = transformLookup.TryGetComponent(workState.child, out var worldTransform, out _);
                bool hadTicked                 = tickedTransformLookup.TryGetComponent(workState.child, out var tickedTransform, out _);
                workState.localTransform       = hadNormal ? worldTransform.worldTransform : TransformQvvs.identity;
                workState.tickedLocalTransform = hadTicked ? tickedTransform.worldTransform : workState.localTransform;
                if (hadTicked && !hadNormal)
                    workState.localTransform = workState.tickedLocalTransform;
                children[i]                  = workState;
            }
        }
        #endregion
    }
}

