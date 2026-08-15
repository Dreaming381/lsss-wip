#if !LATIOS_TRANSFORMS_UNITY
using AOT;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Latios.Transforms
{
    /// <summary>
    /// An ICustomCommand to set the parent of an entity on the main thread.
    /// </summary>
    [BurstCompile]
    public struct ParentCustomCommand : ICustomCommand
    {
        public ParentCustomCommand(Entity child,
                                   Entity parent,
                                   InheritanceFlags inheritanceFlags = InheritanceFlags.Normal,
                                   SetParentOptions setParentOptions = SetParentOptions.AttachLinkedEntityGroup)
        {
            this.child            = child;
            this.parent           = parent;
            this.inheritanceFlags = inheritanceFlags;
            this.options          = setParentOptions;
        }

        public Entity           child;
        public Entity           parent;
        public InheritanceFlags inheritanceFlags;
        public SetParentOptions options;

        public FunctionPointer<ICustomCommand.OnPlayback> GetFunctionPointer()
        {
            return BurstCompiler.CompileFunctionPointer<ICustomCommand.OnPlayback>(OnPlayback);
        }

        [MonoPInvokeCallback(typeof(ICustomCommand.OnPlayback))]
        [BurstCompile]
        static void OnPlayback(ref ICustomCommand.Context context)
        {
            var em = context.entityManager;
            for (int i = 0; i < context.count; i++)
            {
                var command = context.ReadCommand<ParentCustomCommand>(i);
                if (em.Exists(command.child) && em.Exists(command.parent))
                {
                    em.SetParent(command.child, command.parent, command.inheritanceFlags, command.options);
                }
            }
        }
    }

    /// <summary>
    /// An ICustomCommand to set the parent of an entity and set a new local transform
    /// for the child entity on the main thread.
    /// </summary>
    [BurstCompile]
    public struct ParentAndLocalTransformCustomCommand : ICustomCommand
    {
        public ParentAndLocalTransformCustomCommand(Entity child,
                                                    Entity parent,
                                                    TransformQvvs newLocalTransform,
                                                    InheritanceFlags inheritanceFlags = InheritanceFlags.Normal,
                                                    SetParentOptions setParentOptions = SetParentOptions.AttachLinkedEntityGroup)
        {
            this.child             = child;
            this.parent            = parent;
            this.newLocalTransform = newLocalTransform;
            this.inheritanceFlags  = inheritanceFlags;
            this.options           = setParentOptions;
        }

        public Entity           child;
        public Entity           parent;
        public TransformQvvs    newLocalTransform;
        public InheritanceFlags inheritanceFlags;
        public SetParentOptions options;

        public FunctionPointer<ICustomCommand.OnPlayback> GetFunctionPointer()
        {
            return BurstCompiler.CompileFunctionPointer<ICustomCommand.OnPlayback>(OnPlayback);
        }

        [MonoPInvokeCallback(typeof(ICustomCommand.OnPlayback))]
        [BurstCompile]
        static void OnPlayback(ref ICustomCommand.Context context)
        {
            var em = context.entityManager;
            for (int i = 0; i < context.count; i++)
            {
                var command = context.ReadCommand<ParentAndLocalTransformCustomCommand>(i);
                if (em.Exists(command.child) && em.Exists(command.parent))
                {
                    em.SetParent(command.child, command.parent, command.inheritanceFlags, command.options);
                    if (em.HasComponent<WorldTransform>(command.child))
                        TransformTools.SetLocalTransform(command.child, in command.newLocalTransform, em);
                    if (em.HasComponent<TickedWorldTransform>(command.child))
                        TransformTools.SetTickedLocalTransform(command.child, in command.newLocalTransform, em);
                }
            }
        }
    }

    /// <summary>
    /// An ICustomCommand to remove an entity from its parent on the main thread.
    /// </summary>
    [BurstCompile]
    public struct ClearParentCustomCommand : ICustomCommand
    {
        public ClearParentCustomCommand(Entity child, ClearParentOptions clearParentOptions = ClearParentOptions.TransferLinkedEntityGroup)
        {
            this.child   = child;
            this.options = clearParentOptions;
        }

        public Entity             child;
        public ClearParentOptions options;

        public FunctionPointer<ICustomCommand.OnPlayback> GetFunctionPointer()
        {
            return BurstCompiler.CompileFunctionPointer<ICustomCommand.OnPlayback>(OnPlayback);
        }

        [MonoPInvokeCallback(typeof(ICustomCommand.OnPlayback))]
        [BurstCompile]
        static void OnPlayback(ref ICustomCommand.Context context)
        {
            var em = context.entityManager;
            for (int i = 0; i < context.count; i++)
            {
                var command = context.ReadCommand<ClearParentCustomCommand>(i);
                if (em.Exists(command.child))
                {
                    em.ClearParent(command.child, command.options);
                }
            }
        }
    }
}
#endif
