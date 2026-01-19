using AOT;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Latios.Transforms
{
    /// <summary>
    /// An IInstantiateCommand to set the root transform of the solo entity or instantiated hierarchy.
    /// This sets both the WorldTransform and TickedWorldTransform depending on which are present.
    /// </summary>
    [BurstCompile]
    public struct WorldTransformCommand : IInstantiateCommand
    {
        public WorldTransformCommand(TransformQvvs newWorldTransform)
        {
            this.newWorldTransform = newWorldTransform;
        }

        public TransformQvvs newWorldTransform;

        public FunctionPointer<IInstantiateCommand.OnPlayback> GetFunctionPointer()
        {
            return BurstCompiler.CompileFunctionPointer<IInstantiateCommand.OnPlayback>(OnPlayback);
        }

        [MonoPInvokeCallback(typeof(IInstantiateCommand.OnPlayback))]
        [BurstCompile]
        static void OnPlayback(ref IInstantiateCommand.Context context)
        {
            var entities = context.entities;
            var em       = context.entityManager;
            for (int i = 0; i < entities.Length; i++)
            {
                var entity  = entities[i];
                var command = context.ReadCommand<WorldTransformCommand>(i);
                if (em.HasComponent<WorldTransform>(entity))
                    TransformTools.SetWorldTransform(entity, command.newWorldTransform, em);
                if (em.HasComponent<TickedWorldTransform>(entity))
                    TransformTools.SetTickedWorldTransform(entity, command.newWorldTransform, em);
            }
        }
    }

    /// <summary>
    /// An IInstantiateCommand to set the parent of the instantiated entity.
    /// This does not change the WorldTransform or TickedWorldTransform unless the inheritanceFlags
    /// requests CopyParentTransform.
    /// If the target parent entity no longer exists during playback, the instantiated entity will
    /// be immediately destroyed again.
    /// </summary>
    [BurstCompile]
    public struct ParentCommand : IInstantiateCommand
    {
        public ParentCommand(Entity parent, InheritanceFlags inheritanceFlags = InheritanceFlags.Normal)
        {
            this.parent           = parent;
            this.inheritanceFlags = inheritanceFlags;
        }

        public Entity           parent;
        public InheritanceFlags inheritanceFlags;

        public FunctionPointer<IInstantiateCommand.OnPlayback> GetFunctionPointer()
        {
            return BurstCompiler.CompileFunctionPointer<IInstantiateCommand.OnPlayback>(OnPlayback);
        }

        [MonoPInvokeCallback(typeof(IInstantiateCommand.OnPlayback))]
        [BurstCompile]
        static void OnPlayback(ref IInstantiateCommand.Context context)
        {
            var entities = context.entities;
            var em       = context.entityManager;
            for (int i = 0; i < entities.Length; i++)
            {
                var entity  = entities[i];
                var command = context.ReadCommand<ParentCommand>(i);
                em.AddChild(command.parent, entity, command.inheritanceFlags);
            }
        }
    }

    /// <summary>
    /// An IInstantiateCommand to set the parent of the instantiated entity and set a new local transform
    /// for the instantiated entity. This sets both the WorldTransform and TickedWorldTransform depending
    /// on which are present.
    /// If the target parent entity no longer exists during playback, the instantiated entity will
    /// be immediately destroyed again.
    /// </summary>
    [BurstCompile]
    public struct ParentAndLocalTransformCommand : IInstantiateCommand
    {
        public ParentAndLocalTransformCommand(Entity parent, TransformQvvs newLocalTransform, InheritanceFlags inheritanceFlags = InheritanceFlags.Normal)
        {
            this.parent            = parent;
            this.inheritanceFlags  = inheritanceFlags;
            this.newLocalTransform = newLocalTransform;
        }

        public Entity           parent;
        public TransformQvvs    newLocalTransform;
        public InheritanceFlags inheritanceFlags;

        public FunctionPointer<IInstantiateCommand.OnPlayback> GetFunctionPointer()
        {
            return BurstCompiler.CompileFunctionPointer<IInstantiateCommand.OnPlayback>(OnPlayback);
        }

        [MonoPInvokeCallback(typeof(IInstantiateCommand.OnPlayback))]
        [BurstCompile]
        static void OnPlayback(ref IInstantiateCommand.Context context)
        {
            var entities = context.entities;
            var em       = context.entityManager;
            for (int i = 0; i < entities.Length; i++)
            {
                var entity  = entities[i];
                var command = context.ReadCommand<ParentAndLocalTransformCommand>(i);
                em.AddChild(command.parent, entity, command.inheritanceFlags);
                if (em.HasComponent<WorldTransform>(entity))
                    TransformTools.SetLocalTransform(entity, command.newLocalTransform, em);
                if (em.HasComponent<TickedWorldTransform>(entity))
                    TransformTools.SetTickedLocalTransform(entity, command.newLocalTransform, em);
            }
        }
    }
}

