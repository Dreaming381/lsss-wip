using System;
using Latios.Unsafe;
using static UnityEngine.Rendering.VirtualTexturing.Debugging;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Latios.Transforms
{
    public static partial class TransformTools
    {
        internal static class Propagate
        {
            public struct WriteCommand
            {
                public enum WriteType
                {
                    LocalPositionSet,
                    LocalRotationSet,
                    LocalScaleSet,
                    LocalTransformSet,
                    StretchSet,
                    LocalTransformAndStretchSet,
                    WorldPositionSet,
                    WorldRotationSet,
                    WorldScaleSet,
                    WorldTransformSet,
                    LocalPositionDelta,
                    LocalRotationDelta,
                    LocalTransformDelta,
                    LocalInverseTransformDelta,
                    ScaleDelta,
                    StretchDelta,
                    WorldPositionDelta,
                    WorldRotationDelta,
                    WorldTransformDelta,
                    WorldInverseTransformDelta,
                    CopyParentParentChanged
                }
                public int       indexInHierarchy;
                public WriteType writeType;
            }

            // Rules:
            // commands are sorted by index in hierarchy
            // commands are unique indices
            // commands only reference alive entities
            // Only commands that use CopyParentParentChanged are allowed when the inheritance flag is CopyParent
            // Multiple commands embedded within the first command's tree (or any previous command tree) is not yet supported
            public static void WriteAndPropagate<TWorld, TAlive>(NativeArray<EntityInHierarchy> hierarchy,
                                                                 ReadOnlySpan<TransformQvvs>    commandTransformParts,
                                                                 ReadOnlySpan<WriteCommand>     commands,
                                                                 ref TWorld transformLookup,
                                                                 ref TAlive aliveLookup) where TWorld : unmanaged, IWorldTransform where TAlive : unmanaged,
            IAlive
            {
                var tsa = ThreadStackAllocator.GetAllocator();

                var maxEntitiesToProcess   = hierarchy.Length - commands[0].indexInHierarchy;
                var oldNewTransforms       = tsa.AllocateAsSpan<OldNewWorldTransform>(maxEntitiesToProcess);
                var instructions           = tsa.AllocateAsSpan<PropagationInstruction>(maxEntitiesToProcess);
                int oldNewTransformsLength = 0;
                int instructionsLength     = 0;
                int commandsRead           = 0;
                for (int instructionsRead = 0; instructionsRead < instructionsLength || commandsRead < commands.Length; instructionsRead++)
                {
                    EntityInHierarchy    entityInHierarchyToPropagate = default;
                    OldNewWorldTransform changedTransform             = default;
                    bool                 dead                         = false;
                    bool                 hasCommandsRemaining         = commandsRead < commands.Length;
                    bool                 hasInstructionsRemaining     = instructionsRead < instructionsLength;
                    if (hasInstructionsRemaining)
                        entityInHierarchyToPropagate = hierarchy[instructions[instructionsRead].indexInHierarchy];
                    if (hasCommandsRemaining && (!hasInstructionsRemaining || commands[commandsRead].indexInHierarchy <= instructions[instructionsRead].indexInHierarchy))
                    {
                        // Next up is a command we need to write
                        var command                  = commands[commandsRead];
                        entityInHierarchyToPropagate = hierarchy[command.indexInHierarchy];

                        if (!hasInstructionsRemaining || command.indexInHierarchy < instructions[instructionsRead].indexInHierarchy)
                        {
                            // We aren't inheriting any changes from parents. This is a modification only.
                            changedTransform = ComputeCommandTransform(command, commandTransformParts[commandsRead], new EntityInHierarchyHandle
                            {
                                m_hierarchy = hierarchy,
                                m_index     = command.indexInHierarchy
                            }, ref transformLookup, ref aliveLookup);
                            // Don't advance the instruction reader
                            instructionsRead--;
                        }
                        else
                        {
                            // We are writing to a transform, but there's also propagations coming.
                            var instruction              = instructions[instructionsRead];
                            entityInHierarchyToPropagate = hierarchy[instruction.indexInHierarchy];
                            var flags                    = entityInHierarchyToPropagate.m_flags.HasCopyParent() &&
                                                           instruction.useOverrideFlagsForCopyParent ? instruction.overrideFlagsForCopyParent : entityInHierarchyToPropagate.m_flags;
                            var parentOldNewTransforms = oldNewTransforms[instruction.ancestorOldNewTransformIndex];
                            if (entityInHierarchyToPropagate.m_flags.HasCopyParent())
                            {
                                changedTransform = ComputePropagatedTransform(in parentOldNewTransforms,
                                                                              flags,
                                                                              entityInHierarchyToPropagate.entity,
                                                                              ref transformLookup);
                            }
                            else
                            {
                                var localChangedTransform = ComputeCommandTransform(command,
                                                                                    commandTransformParts[commandsRead],
                                                                                    entityInHierarchyToPropagate.entity,
                                                                                    in parentOldNewTransforms.oldTransform,
                                                                                    ref transformLookup);
                                changedTransform = ComputePropagatedTransform(in parentOldNewTransforms,
                                                                              flags,
                                                                              entityInHierarchyToPropagate.entity,
                                                                              ref transformLookup);
                                changedTransform.oldTransform = localChangedTransform.oldTransform;
                            }
                        }
                        commandsRead++;
                    }
                    else if (!transformLookup.HasWorldTransform(entityInHierarchyToPropagate.entity))
                    {
                        // This entity only has one of either WorldTransform or TickedWorldTransform, and we are propagating the other type.
                        entityInHierarchyToPropagate.m_childCount = 0;
                    }
                    else if (!aliveLookup.IsAlive(entityInHierarchyToPropagate.entity))
                    {
                        // The current handle is dead. If its parent is also dead, we need to propagate the override flags from the parent.
                        dead                         = true;
                        var instruction              = instructions[instructionsRead];
                        changedTransform             = oldNewTransforms[instruction.ancestorOldNewTransformIndex];
                        entityInHierarchyToPropagate = hierarchy[instruction.indexInHierarchy];
                        if (instruction.useOverrideFlagsForCopyParent)
                            entityInHierarchyToPropagate.m_flags = instruction.overrideFlagsForCopyParent;
                    }
                    else
                    {
                        // This is a normal handle that needs to receive propagations.
                        var instruction              = instructions[instructionsRead];
                        entityInHierarchyToPropagate = hierarchy[instruction.indexInHierarchy];
                        var flags                    = entityInHierarchyToPropagate.m_flags.HasCopyParent() &&
                                                       instruction.useOverrideFlagsForCopyParent ? instruction.overrideFlagsForCopyParent : entityInHierarchyToPropagate.m_flags;
                        changedTransform = ComputePropagatedTransform(in oldNewTransforms[instruction.ancestorOldNewTransformIndex],
                                                                      flags,
                                                                      entityInHierarchyToPropagate.entity,
                                                                      ref transformLookup);
                    }

                    if (entityInHierarchyToPropagate.childCount != 0 && !changedTransform.newTransform.Equals(changedTransform.oldTransform))
                    {
                        // Add children to the propagation list
                        oldNewTransforms[oldNewTransformsLength] = changedTransform;
                        var newInstruction                       = new PropagationInstruction
                        {
                            ancestorOldNewTransformIndex  = oldNewTransformsLength,
                            overrideFlagsForCopyParent    = entityInHierarchyToPropagate.m_flags,
                            useOverrideFlagsForCopyParent = dead,
                            indexInHierarchy              = entityInHierarchyToPropagate.firstChildIndex,
                        };
                        oldNewTransformsLength++;

                        for (int i = 0; i < entityInHierarchyToPropagate.childCount; i++)
                        {
                            instructions[instructionsLength] = newInstruction;
                            instructionsLength++;
                            newInstruction.indexInHierarchy++;
                        }
                    }
                }

                tsa.Dispose();
            }

            struct OldNewWorldTransform
            {
                public TransformQvvs oldTransform;
                public TransformQvvs newTransform;
            }

            struct PropagationInstruction
            {
                public int              indexInHierarchy;
                public int              ancestorOldNewTransformIndex;
                public InheritanceFlags overrideFlagsForCopyParent;
                public bool             useOverrideFlagsForCopyParent;
            }

            static OldNewWorldTransform ComputeCommandTransform<TWorld, TAlive>(WriteCommand command,
                                                                                TransformQvvs writeData,
                                                                                EntityInHierarchyHandle handle,
                                                                                ref TWorld transformLookup,
                                                                                ref TAlive aliveLookup) where TWorld : unmanaged,
            IWorldTransform where TAlive : unmanaged, IAlive
            {
                ref var transform            = ref transformLookup.GetWorldTransformRefRW(handle.entity).ValueRW.worldTransform;
                var     oldNewWorldTransform = new OldNewWorldTransform { oldTransform = transform };
                switch (command.writeType)
                {
                    case WriteCommand.WriteType.LocalPositionSet:
                    {
                        var localTransform      = LocalTransformFrom(handle, ref aliveLookup, ref transformLookup, out var parentTransform);
                        localTransform.position = writeData.position;
                        qvvs.mulclean(ref transform, in parentTransform, in localTransform);
                        break;
                    }
                    case WriteCommand.WriteType.LocalRotationSet:
                    {
                        var localTransform      = LocalTransformFrom(handle, ref aliveLookup, ref transformLookup, out var parentTransform);
                        localTransform.rotation = writeData.rotation;
                        qvvs.mulclean(ref transform, in parentTransform, in localTransform);
                        break;
                    }
                    case WriteCommand.WriteType.LocalScaleSet:
                    {
                        var localTransform   = LocalTransformFrom(handle, ref aliveLookup, ref transformLookup, out var parentTransform);
                        localTransform.scale = writeData.scale;
                        qvvs.mulclean(ref transform, in parentTransform, in localTransform);
                        break;
                    }
                    case WriteCommand.WriteType.LocalTransformSet:
                    {
                        var localTransform      = LocalTransformFrom(handle, ref aliveLookup, ref transformLookup, out var parentTransform);
                        localTransform.position = writeData.position;
                        localTransform.rotation = writeData.rotation;
                        localTransform.scale    = writeData.scale;
                        qvvs.mulclean(ref transform, in parentTransform, in localTransform);
                        break;
                    }
                    case WriteCommand.WriteType.StretchSet:
                        transform.stretch = writeData.stretch;
                        break;
                    case WriteCommand.WriteType.LocalTransformAndStretchSet:
                    {
                        var localTransform      = LocalTransformFrom(handle, ref aliveLookup, ref transformLookup, out var parentTransform);
                        localTransform.position = writeData.position;
                        localTransform.rotation = writeData.rotation;
                        localTransform.scale    = writeData.scale;
                        transform.stretch       = writeData.stretch;
                        qvvs.mulclean(ref transform, in parentTransform, in localTransform);
                        break;
                    }
                    case WriteCommand.WriteType.WorldPositionSet:
                        transform.position = writeData.position;
                        break;
                    case WriteCommand.WriteType.WorldRotationSet:
                        transform.rotation = writeData.rotation;
                        break;
                    case WriteCommand.WriteType.WorldScaleSet:
                        transform.scale = writeData.scale;
                        break;
                    case WriteCommand.WriteType.WorldTransformSet:
                        transform = writeData;
                        break;
                    case WriteCommand.WriteType.LocalPositionDelta:
                    {
                        var localTransform       = LocalTransformFrom(handle, ref aliveLookup, ref transformLookup, out var parentTransform);
                        localTransform.position += writeData.position;
                        qvvs.mulclean(ref transform, in parentTransform, in localTransform);
                        break;
                    }
                    case WriteCommand.WriteType.LocalRotationDelta:
                    {
                        var localTransform      = LocalTransformFrom(handle, ref aliveLookup, ref transformLookup, out var parentTransform);
                        localTransform.rotation = math.normalize(math.mul(writeData.rotation, localTransform.rotation));
                        qvvs.mulclean(ref transform, in parentTransform, in localTransform);
                        break;
                    }
                    case WriteCommand.WriteType.LocalTransformDelta:
                    {
                        var localTransform = LocalTransformFrom(handle, ref aliveLookup, ref transformLookup, out var parentTransform);
                        qvvs.mul(ref transform, in writeData, in localTransform);
                        transform = qvvs.mulclean(in parentTransform, transform);
                        break;
                    }
                    case WriteCommand.WriteType.LocalInverseTransformDelta:
                    {
                        var localTransform     = LocalTransformFrom(handle, ref aliveLookup, ref transformLookup, out var parentTransform);
                        var localTransformQvvs = new TransformQvvs(localTransform.position, localTransform.rotation, localTransform.scale, transform.stretch, transform.worldIndex);
                        localTransformQvvs     = qvvs.inversemulqvvs(in writeData, in localTransformQvvs);
                        transform              = qvvs.mulclean(in parentTransform, localTransformQvvs);
                        break;
                    }
                    case WriteCommand.WriteType.StretchDelta:
                        transform.stretch *= writeData.stretch;
                        break;
                    case WriteCommand.WriteType.WorldPositionDelta:
                        transform.position += writeData.position;
                        break;
                    case WriteCommand.WriteType.WorldRotationDelta:
                        transform.rotation = math.normalize(math.mul(writeData.rotation, transform.rotation));
                        break;
                    case WriteCommand.WriteType.ScaleDelta:
                        transform.scale *= writeData.scale;
                        break;
                    case WriteCommand.WriteType.WorldTransformDelta:
                        transform = qvvs.mulclean(writeData, transform);
                        break;
                    case WriteCommand.WriteType.WorldInverseTransformDelta:
                        transform = qvvs.inversemulqvvsclean(writeData, transform);
                        break;
                    case WriteCommand.WriteType.CopyParentParentChanged:
                    {
                        var parent = handle.FindParent(ref aliveLookup);
                        transform  = transformLookup.GetWorldTransform(parent.entity).worldTransform;
                        break;
                    }
                }
                oldNewWorldTransform.newTransform = transform;
                return oldNewWorldTransform;
            }

            static OldNewWorldTransform ComputeCommandTransform<T>(WriteCommand command,
                                                                   TransformQvvs writeData,
                                                                   Entity entity,
                                                                   in TransformQvvs parentTransform,
                                                                   ref T transformLookup) where T : unmanaged, IWorldTransform
            {
                ref var transform            = ref transformLookup.GetWorldTransformRefRW(entity).ValueRW.worldTransform;
                var     oldNewWorldTransform = new OldNewWorldTransform { oldTransform = transform };
                switch (command.writeType)
                {
                    case WriteCommand.WriteType.LocalPositionSet:
                    {
                        var localTransform      = qvvs.inversemul(in parentTransform, in transform);
                        localTransform.position = writeData.position;
                        qvvs.mulclean(ref transform, in parentTransform, in localTransform);
                        break;
                    }
                    case WriteCommand.WriteType.LocalRotationSet:
                    {
                        var localTransform      = qvvs.inversemul(in parentTransform, in transform);
                        localTransform.rotation = writeData.rotation;
                        qvvs.mulclean(ref transform, in parentTransform, in localTransform);
                        break;
                    }
                    case WriteCommand.WriteType.LocalScaleSet:
                    {
                        var localTransform   = qvvs.inversemul(in parentTransform, in transform);
                        localTransform.scale = writeData.scale;
                        qvvs.mulclean(ref transform, in parentTransform, in localTransform);
                        break;
                    }
                    case WriteCommand.WriteType.LocalTransformSet:
                    {
                        var localTransform      = qvvs.inversemul(in parentTransform, in transform);
                        localTransform.position = writeData.position;
                        localTransform.rotation = writeData.rotation;
                        localTransform.scale    = writeData.scale;
                        qvvs.mulclean(ref transform, in parentTransform, in localTransform);
                        break;
                    }
                    case WriteCommand.WriteType.StretchSet:
                        transform.stretch = writeData.stretch;
                        break;
                    case WriteCommand.WriteType.LocalTransformAndStretchSet:
                    {
                        var localTransform      = qvvs.inversemul(in parentTransform, in transform);
                        localTransform.position = writeData.position;
                        localTransform.rotation = writeData.rotation;
                        localTransform.scale    = writeData.scale;
                        transform.stretch       = writeData.stretch;
                        qvvs.mulclean(ref transform, in parentTransform, in localTransform);
                        break;
                    }
                    case WriteCommand.WriteType.WorldPositionSet:
                        transform.position = writeData.position;
                        break;
                    case WriteCommand.WriteType.WorldRotationSet:
                        transform.rotation = writeData.rotation;
                        break;
                    case WriteCommand.WriteType.WorldScaleSet:
                        transform.scale = writeData.scale;
                        break;
                    case WriteCommand.WriteType.WorldTransformSet:
                        transform = writeData;
                        break;
                    case WriteCommand.WriteType.LocalPositionDelta:
                    {
                        var localTransform       = qvvs.inversemul(in parentTransform, in transform);
                        localTransform.position += writeData.position;
                        qvvs.mulclean(ref transform, in parentTransform, in localTransform);
                        break;
                    }
                    case WriteCommand.WriteType.LocalRotationDelta:
                    {
                        var localTransform      = qvvs.inversemul(in parentTransform, in transform);
                        localTransform.rotation = math.normalize(math.mul(writeData.rotation, localTransform.rotation));
                        qvvs.mulclean(ref transform, in parentTransform, in localTransform);
                        break;
                    }
                    case WriteCommand.WriteType.LocalTransformDelta:
                    {
                        var localTransform = qvvs.inversemul(in parentTransform, in transform);
                        qvvs.mul(ref transform, in writeData, in localTransform);
                        transform = qvvs.mulclean(in parentTransform, transform);
                        break;
                    }
                    case WriteCommand.WriteType.LocalInverseTransformDelta:
                    {
                        var localTransform     = qvvs.inversemul(in parentTransform, in transform);
                        var localTransformQvvs = new TransformQvvs(localTransform.position, localTransform.rotation, localTransform.scale, transform.stretch, transform.worldIndex);
                        localTransformQvvs     = qvvs.inversemulqvvs(in writeData, in localTransformQvvs);
                        transform              = qvvs.mulclean(in parentTransform, localTransformQvvs);
                        break;
                    }
                    case WriteCommand.WriteType.StretchDelta:
                        transform.stretch *= writeData.stretch;
                        break;
                    case WriteCommand.WriteType.WorldPositionDelta:
                        transform.position += writeData.position;
                        break;
                    case WriteCommand.WriteType.WorldRotationDelta:
                        transform.rotation = math.normalize(math.mul(writeData.rotation, transform.rotation));
                        break;
                    case WriteCommand.WriteType.ScaleDelta:
                        transform.scale *= writeData.scale;
                        break;
                    case WriteCommand.WriteType.WorldTransformDelta:
                        transform = qvvs.mulclean(writeData, transform);
                        break;
                    case WriteCommand.WriteType.WorldInverseTransformDelta:
                        transform = qvvs.inversemulqvvsclean(writeData, transform);
                        break;
                    case WriteCommand.WriteType.CopyParentParentChanged:
                    {
                        transform = parentTransform;
                        break;
                    }
                }
                oldNewWorldTransform.newTransform = transform;
                return oldNewWorldTransform;
            }

            static OldNewWorldTransform ComputePropagatedTransform<T>(in OldNewWorldTransform oldNewWorldTransform, InheritanceFlags flags, Entity entity,
                                                                      ref T transformLookup) where T : unmanaged, IWorldTransform
            {
                if (flags.HasCopyParent())
                {
                    transformLookup.GetWorldTransformRefRW(entity).ValueRW.worldTransform = oldNewWorldTransform.newTransform;
                    return oldNewWorldTransform;
                }
                if (flags == InheritanceFlags.WorldAll)
                {
                    var t                                          = transformLookup.GetWorldTransform(entity).worldTransform;
                    return new OldNewWorldTransform { oldTransform = t, newTransform = t };
                }

                // Todo: If flags is not default, check change between old and new transforms of parent, because we might
                // still be able to early out.

                ref var worldTransform         = ref transformLookup.GetWorldTransformRefRW(entity).ValueRW.worldTransform;
                var     originalWorldTransform = worldTransform;
                var     parentTransform        = oldNewWorldTransform.newTransform;
                var     localTransform         = qvvs.inversemul(oldNewWorldTransform.oldTransform, originalWorldTransform);
                qvvs.mulclean(ref worldTransform, in parentTransform, in localTransform);

                if ((flags & InheritanceFlags.WorldRotation) == InheritanceFlags.WorldRotation)
                    worldTransform.rotation = originalWorldTransform.rotation;
                else if ((flags & InheritanceFlags.WorldRotation) != InheritanceFlags.Normal)
                    worldTransform.rotation = ComputeMixedRotation(originalWorldTransform.rotation, worldTransform.rotation, flags);
                if ((flags & InheritanceFlags.WorldX) == InheritanceFlags.WorldX)
                    worldTransform.position.x = originalWorldTransform.position.x;
                if ((flags & InheritanceFlags.WorldY) == InheritanceFlags.WorldY)
                    worldTransform.position.y = originalWorldTransform.position.y;
                if ((flags & InheritanceFlags.WorldZ) == InheritanceFlags.WorldZ)
                    worldTransform.position.z = originalWorldTransform.position.z;
                if ((flags & InheritanceFlags.WorldScale) == InheritanceFlags.WorldScale)
                    worldTransform.scale = originalWorldTransform.scale;

                return new OldNewWorldTransform { oldTransform = originalWorldTransform, newTransform = worldTransform };
            }

            static quaternion ComputeMixedRotation(quaternion originalWorldRotation, quaternion hierarchyWorldRotation, InheritanceFlags flags)
            {
                var forward = math.select(math.forward(hierarchyWorldRotation),
                                          math.forward(originalWorldRotation),
                                          (flags & InheritanceFlags.WorldForward) == InheritanceFlags.WorldForward);
                var up = math.select(math.rotate(hierarchyWorldRotation, math.up()),
                                     math.rotate(originalWorldRotation, math.up()),
                                     (flags & InheritanceFlags.WorldUp) == InheritanceFlags.WorldUp);

                if ((flags & InheritanceFlags.StrictUp) == InheritanceFlags.StrictUp)
                {
                    float3 right = math.normalizesafe(math.cross(up, forward), float3.zero);
                    if (right.Equals(float3.zero))
                        return math.select(hierarchyWorldRotation.value, originalWorldRotation.value, (flags & InheritanceFlags.WorldUp) == InheritanceFlags.WorldUp);
                    var newForward = math.cross(right, up);
                    return new quaternion(new float3x3(right, up, newForward));
                }
                else
                {
                    float3 right = math.normalizesafe(math.cross(up, forward), float3.zero);
                    if (right.Equals(float3.zero))
                        return math.select(hierarchyWorldRotation.value,
                                           originalWorldRotation.value,
                                           (flags & InheritanceFlags.WorldForward) == InheritanceFlags.WorldForward);
                    var newUp = math.cross(forward, right);
                    return new quaternion(new float3x3(right, newUp, forward));
                }
            }
        }
    }
}

