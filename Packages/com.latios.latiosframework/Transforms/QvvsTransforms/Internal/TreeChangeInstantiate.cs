using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Latios.Unsafe;
using static Latios.Transforms.TransformTools;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Entities.Exposed;
using Unity.Jobs;
using Unity.Mathematics;

namespace Latios.Transforms
{
    internal static class TreeChangeInstantiate
    {
        static readonly Unity.Profiling.ProfilerMarker specializedMarker = new Unity.Profiling.ProfilerMarker("Specialized");

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

            var batchedAddSetsStream = new NativeStream(childWorkStates.Length, Allocator.TempJob);
            var classifyJh           = new ClassifyJob
            {
                children              = childWorkStates,
                batchAddSetsStream    = batchedAddSetsStream.AsWriter(),
                esil                  = em.GetEntityStorageInfoLookup(),
                transformLookup       = em.GetComponentLookup<WorldTransform>(true),
                tickedTransformLookup = em.GetComponentLookup<TickedWorldTransform>(true),
                rootReferenceLookup   = em.GetComponentLookup<RootReference>(true),
                hierarchyLookup       = em.GetBufferLookup<EntityInHierarchy>(true),
                cleanupLookup         = em.GetBufferLookup<EntityInHierarchyCleanup>(true),
            }.ScheduleParallel(childWorkStates.Length, 32, default);
            var batchedAddSets = new NativeList<BatchedAddSet>(Allocator.TempJob);
            var sortChildAddJh = new SortAndMergeBatchAddSetsJob
            {
                batchAddSetsStream = batchedAddSetsStream,
                outputBatchAddSets = batchedAddSets
            }.Schedule(classifyJh);
            var rootWorkStates = new NativeList<RootWorkState>(Allocator.TempJob);
            var childIndices   = new NativeArray<int>(childWorkStates.Length, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            var groupRootsJh   = new GroupRootsJob
            {
                childIndices    = childIndices,
                rootWorkStates  = rootWorkStates,
                childWorkStates = childWorkStates
            }.Schedule(classifyJh);
            sortChildAddJh.Complete();
            var entityCacheList = new NativeList<Entity>(batchedAddSets.Length, Allocator.Temp);
            for (int i = 0; i < batchedAddSets.Length; )
            {
                entityCacheList.Clear();
                var addSet = batchedAddSets[i].addSet;
                var mask   = addSet.changeFlags;
                entityCacheList.Add(addSet.entity);
                for (int subCount = 1; i + subCount < batchedAddSets.Length; subCount++)
                {
                    var nextAddSet = batchedAddSets[i + subCount].addSet;
                    if (nextAddSet.changeFlags != mask)
                        break;
                    entityCacheList.Add(nextAddSet.entity);
                }
                TreeKernels.AddComponentsBatched(em, addSet, entityCacheList.AsArray());
                i += entityCacheList.Length;
            }
            var buffersJh = new ProcessBuffersJob
            {
                batchAddSetStream          = batchedAddSetsStream.AsReader(),
                childIndices               = childIndices,
                childWorkStates            = childWorkStates,
                cleanupHandle              = em.GetBufferTypeHandle<EntityInHierarchyCleanup>(false),
                esil                       = em.GetEntityStorageInfoLookup(),
                hierarchyHandle            = em.GetBufferTypeHandle<EntityInHierarchy>(false),
                legHandle                  = em.GetBufferTypeHandle<LinkedEntityGroup>(false),
                rootWorkStates             = rootWorkStates.AsDeferredJobArray(),
                tickedWorldTransformLookup = em.GetComponentLookup<TickedWorldTransform>(false),
                worldTransformLookup       = em.GetComponentLookup<WorldTransform>(false),
            }.Schedule(rootWorkStates, 4, groupRootsJh);
            buffersJh.Complete();

            specializedMarker.Begin();
            for (int i = 0; i < childWorkStates.Length; i++)
            {
                var child = childWorkStates[i];
                if (child.parentIsDead)
                {
                    context.RequestDestroyEntity(child.child);
                    continue;
                }

                AddChild(em, ref child);
                if (em.HasComponent<WorldTransform>(child.child))
                    TransformTools.SetLocalTransform(child.child, in child.localTransform, em);
                if (em.HasComponent<TickedWorldTransform>(child.child))
                    TransformTools.SetTickedLocalTransform(child.child, in child.tickedLocalTransform, em);
            }
            specializedMarker.End();

            childWorkStates.Dispose();
            batchedAddSetsStream.Dispose();
            batchedAddSets.Dispose();
            rootWorkStates.Dispose();
            childIndices.Dispose();
        }

        #region Types
        struct ChildWorkState
        {
            public Entity                         parent;
            public Entity                         child;
            public InheritanceFlags               flags;
            public AddChildOptions                options;
            public bool                           parentIsDead;
            public bool                           addedLeg;
            public TransformQvvs                  localTransform;
            public TransformQvvs                  tickedLocalTransform;
            public TreeKernels.TreeClassification parentClassification;
            public TreeKernels.TreeClassification childClassification;
            public TreeKernels.ComponentAddSet    childAddSet;
        }

        struct RootWorkState
        {
            public int childStart;
            public int childCount;
        }

        struct BatchedAddSet : IComparable<BatchedAddSet>
        {
            public TreeKernels.ComponentAddSet addSet;
            public int                         chunkOrder;
            public int                         indexInChunk;

            public int CompareTo(BatchedAddSet other)
            {
                var result = addSet.changeFlags.CompareTo(other.addSet.changeFlags);
                if (result == 0)
                {
                    result = chunkOrder.CompareTo(other.chunkOrder);
                    if (result == 0)
                        result = indexInChunk.CompareTo(other.indexInChunk);
                }
                return result;
            }
        }
        #endregion

        #region Jobs
        [BurstCompile]
        struct ClassifyJob : IJobFor
        {
            public NativeArray<ChildWorkState> children;
            public NativeStream.Writer         batchAddSetsStream;

            [ReadOnly] public ComponentLookup<WorldTransform>        transformLookup;
            [ReadOnly] public ComponentLookup<TickedWorldTransform>  tickedTransformLookup;
            [ReadOnly] public EntityStorageInfoLookup                esil;
            [ReadOnly] public ComponentLookup<RootReference>         rootReferenceLookup;
            [ReadOnly] public BufferLookup<EntityInHierarchy>        hierarchyLookup;
            [ReadOnly] public BufferLookup<EntityInHierarchyCleanup> cleanupLookup;

            HasChecker<TickedEntityTag>    tickedEntityChecker;
            HasChecker<LiveBakedTag>       liveBakedChecker;
            HasChecker<LiveAddedParentTag> liveAddedParentChecker;
            HasChecker<LinkedEntityGroup>  legChecker;

            public void Execute(int i)
            {
                var workState          = children[i];
                workState.parentIsDead = !esil.IsAlive(workState.parent);
                if (workState.parentIsDead)
                {
                    children[i] = new ChildWorkState { parentIsDead = true };
                    return;
                }

                batchAddSetsStream.BeginForEachIndex(i);

                bool hadNormal                 = transformLookup.TryGetComponent(workState.child, out var worldTransform, out _);
                bool hadTicked                 = tickedTransformLookup.TryGetComponent(workState.child, out var tickedTransform, out _);
                workState.localTransform       = hadNormal ? worldTransform.worldTransform : TransformQvvs.identity;
                workState.tickedLocalTransform = hadTicked ? tickedTransform.worldTransform : workState.localTransform;
                if (hadTicked && !hadNormal)
                    workState.localTransform = workState.tickedLocalTransform;

                workState.childClassification  = TreeKernels.ClassifyAlive(ref rootReferenceLookup, ref hierarchyLookup, ref cleanupLookup, workState.child);
                workState.parentClassification = TreeKernels.ClassifyAlive(ref rootReferenceLookup, ref hierarchyLookup, ref cleanupLookup, workState.parent);
                CheckDeadRootLegRules(in workState.parentClassification, workState.options);

                var childStorageInfo  = esil[workState.child];
                workState.childAddSet = GetChildComponentsToAdd(workState.child, workState.childClassification.role, workState.flags, hadNormal, hadTicked);
                batchAddSetsStream.Write(new BatchedAddSet
                {
                    addSet       = workState.childAddSet,
                    chunkOrder   = childStorageInfo.Chunk.GetHashCode(),
                    indexInChunk = childStorageInfo.IndexInChunk,
                });

                if (workState.parentClassification.role == TreeKernels.TreeClassification.TreeRole.Solo ||
                    workState.parentClassification.role == TreeKernels.TreeClassification.TreeRole.Root)
                {
                    var addSet         = GetParentComponentsToAdd(workState.parent, workState.parentClassification.role, workState.childAddSet, workState.options);
                    workState.addedLeg = addSet.addSet.linkedEntityGroup;
                    batchAddSetsStream.Write(addSet);
                }
                else
                {
                    var hierarchy =
                        (workState.parentClassification.isRootAlive ? hierarchyLookup[workState.parentClassification.root] : cleanupLookup[workState.parentClassification.root].
                         Reinterpret<EntityInHierarchy>()).AsNativeArray();
                    GetAncestorComponentsToAdd(hierarchy, workState.parentClassification, workState.childAddSet, workState.options, out workState.addedLeg);
                }

                children[i] = workState;

                batchAddSetsStream.EndForEachIndex();
            }

            TreeKernels.ComponentAddSet GetChildComponentsToAdd(Entity child,
                                                                TreeKernels.TreeClassification.TreeRole role,
                                                                InheritanceFlags flags,
                                                                bool hasWorldTransform,
                                                                bool hasTickedTransform)
            {
                TreeKernels.ComponentAddSet addSet = default;
                addSet.entity                      = child;
                if (role == TreeKernels.TreeClassification.TreeRole.Solo || role == TreeKernels.TreeClassification.TreeRole.Root)
                    addSet.rootReference = true;

                var childChunk = esil[child].Chunk;
#if UNITY_EDITOR
                if (liveBakedChecker[childChunk] && !liveAddedParentChecker[childChunk])
                    addSet.liveAddedParent = true;
#endif
                var isTicked = tickedEntityChecker[childChunk];
                var isNormal = hasWorldTransform;
                if (!isTicked && !isNormal)
                {
                    addSet.isNormal            = true;
                    addSet.setNormalToIdentity = true;
                    addSet.worldTransform      = true;
                }
                else
                {
                    if (isTicked)
                    {
                        addSet.isTicked             = true;
                        addSet.tickedWorldTransform = !hasTickedTransform;
                        if (addSet.tickedWorldTransform && isNormal)
                            addSet.copyNormalToTicked = true;
                        else if (addSet.tickedWorldTransform)
                            addSet.setTickedToIdentity = true;
                    }
                    if (isNormal)
                    {
                        addSet.isNormal = true;
                    }
                }
                return addSet;
            }

            BatchedAddSet GetParentComponentsToAdd(Entity parent,
                                                   TreeKernels.TreeClassification.TreeRole role,
                                                   TreeKernels.ComponentAddSet childAddSet,
                                                   AddChildOptions options,
                                                   bool considerTransforms = true)
            {
                TreeKernels.ComponentAddSet addSet = default;
                addSet.entity                      = parent;
                addSet.isTicked                    = childAddSet.isTicked;
                addSet.isNormal                    = childAddSet.isNormal;
                if (role == TreeKernels.TreeClassification.TreeRole.Solo)
                    addSet.entityInHierarchy = true;

                if (considerTransforms)
                {
                    bool hasNormal   = transformLookup.HasComponent(parent);
                    bool hasTicked   = tickedTransformLookup.HasComponent(parent);
                    addSet.isNormal |= hasNormal;
                    addSet.isTicked |= hasTicked;
                    if (childAddSet.isNormal && !hasNormal)
                    {
                        addSet.worldTransform = true;
                        if (hasTicked)
                            addSet.copyTickedToNormal = true;
                        else
                            addSet.setNormalToIdentity = true;
                    }
                    if (childAddSet.isTicked && !hasTicked)
                    {
                        addSet.tickedWorldTransform = true;
                        if (hasNormal)
                            addSet.copyNormalToTicked = true;
                        else
                            addSet.setTickedToIdentity = true;
                    }
                }

                var esi = esil[parent];

                if (role == TreeKernels.TreeClassification.TreeRole.Solo || role == TreeKernels.TreeClassification.TreeRole.Root)
                {
                    if (options != AddChildOptions.IgnoreLinkedEntityGroup && !legChecker[esi.Chunk])
                        addSet.linkedEntityGroup = true;
                    if (options == AddChildOptions.IgnoreLinkedEntityGroup)
                        addSet.entityInHierarchyCleanup = true;
                }
                return new BatchedAddSet
                {
                    addSet       = addSet,
                    chunkOrder   = esi.Chunk.GetHashCode(),
                    indexInChunk = esi.IndexInChunk,
                };
            }

            void GetAncestorComponentsToAdd(ReadOnlySpan<EntityInHierarchy> hierarchy,
                                            TreeKernels.TreeClassification parentClassification,
                                            TreeKernels.ComponentAddSet childAddSet,
                                            AddChildOptions options,
                                            out bool addedLeg)
            {
                var  parentAddSet         = GetParentComponentsToAdd(hierarchy[parentClassification.indexInHierarchy].entity, parentClassification.role, childAddSet, options);
                bool allTransformsPresent = false;
                if (!parentAddSet.addSet.noChange)
                {
                    batchAddSetsStream.Write(parentAddSet);

                    for (int index = hierarchy[parentClassification.indexInHierarchy].parentIndex; index > 0; index = hierarchy[index].parentIndex)
                    {
                        var newAddSet = GetParentComponentsToAdd(hierarchy[index].entity, TreeKernels.TreeClassification.TreeRole.InternalWithChildren, childAddSet, options);
                        if (newAddSet.addSet.noChange)
                        {
                            allTransformsPresent = true;
                            break;
                        }
                        if (hierarchy[index].m_flags.HasCopyParent())
                            newAddSet.addSet.isCopyParent = true;
                        newAddSet.addSet.indexInHierarchy = index;
                        newAddSet.addSet.parent           = hierarchy[hierarchy[index].parentIndex].entity;
                        batchAddSetsStream.Write(newAddSet);
                    }
                }

                var rootAddSet = GetParentComponentsToAdd(parentClassification.root, TreeKernels.TreeClassification.TreeRole.Root, childAddSet, options, !allTransformsPresent);
                addedLeg       = rootAddSet.addSet.linkedEntityGroup;
                batchAddSetsStream.Write(rootAddSet);
            }

            [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
            void CheckDeadRootLegRules(in TreeKernels.TreeClassification parentClassification, AddChildOptions options)
            {
                if (options != AddChildOptions.IgnoreLinkedEntityGroup &&
                    (parentClassification.role == TreeKernels.TreeClassification.TreeRole.InternalNoChildren ||
                     parentClassification.role == TreeKernels.TreeClassification.TreeRole.InternalWithChildren))
                {
                    if (!esil.IsAlive(parentClassification.root))
                        throw new InvalidOperationException(
                            $"Cannot add LinkedEntityGroup to a new hierarchy whose root has been destroyed. Root: {parentClassification.root.ToFixedString()}");
                }
            }
        }

        [BurstCompile]
        struct SortAndMergeBatchAddSetsJob : IJob
        {
            [ReadOnly] public NativeStream   batchAddSetsStream;
            public NativeList<BatchedAddSet> outputBatchAddSets;

            public void Execute()
            {
                var addSets        = batchAddSetsStream.ToNativeArray<BatchedAddSet>(Allocator.Temp);
                var hashToOrderMap = new UnsafeHashMap<int, int>(addSets.Length, Allocator.Temp);
                for (int i = 0; i < addSets.Length; i++)
                {
                    var addSet = addSets[i];
                    if (addSet.addSet.entity == Entity.Null)
                        addSet.chunkOrder = -1;
                    else if (hashToOrderMap.TryGetValue(addSet.chunkOrder, out var order))
                        addSet.chunkOrder = order;
                    else
                    {
                        var hash          = addSet.chunkOrder;
                        addSet.chunkOrder = hashToOrderMap.Count;
                        hashToOrderMap.Add(hash, addSet.chunkOrder);
                    }
                    addSets[i] = addSet;
                }
                addSets.Sort(new EntitySorter());

                // Matching entities should now be adjacent in memory. Merge the flags.
                outputBatchAddSets.Capacity = addSets.Length;
                for (int i = 0; i < addSets.Length; )
                {
                    var baseAddSet = addSets[i];
                    for (i = i + 1; i < addSets.Length; i++)
                    {
                        var addSet = addSets[i];
                        if (addSet.chunkOrder != baseAddSet.chunkOrder || addSet.indexInChunk != baseAddSet.indexInChunk)
                            break;

                        baseAddSet.addSet.packed |= addSet.addSet.packed;
                    }
                    outputBatchAddSets.Add(baseAddSet);
                }

                outputBatchAddSets.Sort();
            }

            struct EntitySorter : IComparer<BatchedAddSet>
            {
                public int Compare(BatchedAddSet x, BatchedAddSet y)
                {
                    var result = x.chunkOrder.CompareTo(y.chunkOrder);
                    if (result == 0)
                        result = x.indexInChunk.CompareTo(y.indexInChunk);
                    return result;
                }
            }
        }

        [BurstCompile]
        struct GroupRootsJob : IJob
        {
            public NativeArray<int>                       childIndices;
            public NativeList<RootWorkState>              rootWorkStates;
            [ReadOnly] public NativeArray<ChildWorkState> childWorkStates;

            public void Execute()
            {
                rootWorkStates.Capacity = childWorkStates.Length;
                var rootToIndexMap      = new UnsafeHashMap<Entity, int>(childWorkStates.Length, Allocator.Temp);
                var rootToIndexArray    = new NativeArray<int>(childWorkStates.Length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                for (int i = 0; i < childWorkStates.Length; i++)
                {
                    var    parentClassification = childWorkStates[i].parentClassification;
                    Entity root;
                    if (parentClassification.role == TreeKernels.TreeClassification.TreeRole.Solo || parentClassification.role == TreeKernels.TreeClassification.TreeRole.Root)
                        root = childWorkStates[i].parent;
                    else
                        root = parentClassification.root;

                    if (!rootToIndexMap.TryGetValue(root, out var index))
                    {
                        index                                             = rootWorkStates.Length;
                        rootWorkStates.Add(new RootWorkState { childStart = 0, childCount = 1 });
                        rootToIndexMap.Add(root, index);
                    }
                    else
                        rootWorkStates.ElementAt(index).childCount++;
                    rootToIndexArray[i] = index;
                }

                // Prefix sum
                int running = 0;
                for (int i = 0; i < rootWorkStates.Length; i++)
                {
                    ref var state     = ref rootWorkStates.ElementAt(i);
                    state.childStart  = running;
                    running          += state.childCount;
                    state.childCount  = 0;
                }

                // Write output
                for (int i = 0; i < rootToIndexArray.Length; i++)
                {
                    ref var state     = ref rootWorkStates.ElementAt(rootToIndexArray[i]);
                    var     dst       = state.childStart + state.childCount;
                    childIndices[dst] = i;
                    state.childCount++;
                }
            }
        }

        [BurstCompile]
        struct ProcessBuffersJob : IJobParallelForDefer
        {
            [ReadOnly] public NativeArray<int>                                       childIndices;
            [ReadOnly] public NativeStream.Reader                                    batchAddSetStream;
            public NativeArray<RootWorkState>                                        rootWorkStates;
            [NativeDisableParallelForRestriction] public NativeArray<ChildWorkState> childWorkStates;

            [ReadOnly] public EntityStorageInfoLookup                                          esil;
            public BufferTypeHandle<EntityInHierarchy>                                         hierarchyHandle;
            public BufferTypeHandle<EntityInHierarchyCleanup>                                  cleanupHandle;
            public BufferTypeHandle<LinkedEntityGroup>                                         legHandle;
            [NativeDisableParallelForRestriction] public ComponentLookup<WorldTransform>       worldTransformLookup;
            [NativeDisableParallelForRestriction] public ComponentLookup<TickedWorldTransform> tickedWorldTransformLookup;

            public void Execute(int rootIndex)
            {
                var rootWorkState       = rootWorkStates[rootIndex];
                var rootChildrenIndices = childIndices.GetSubArray(rootWorkState.childStart, rootWorkState.childCount);

                Entity root;
                bool   rootIsAlive;
                var    firstClassification = childWorkStates[rootChildrenIndices[0]].parentClassification;
                if (firstClassification.role == TreeKernels.TreeClassification.TreeRole.Solo || firstClassification.role == TreeKernels.TreeClassification.TreeRole.Root)
                {
                    root        = childWorkStates[rootChildrenIndices[0]].parent;
                    rootIsAlive = true;
                    // If the parent was destroyed, skip.
                    if (root == Entity.Null)
                        return;
                }
                else
                {
                    root        = firstClassification.root;
                    rootIsAlive = firstClassification.isRootAlive;
                }

                var                              esi = esil[root];
                DynamicBuffer<EntityInHierarchy> hierarchy;
                if (rootIsAlive)
                    hierarchy = esi.Chunk.GetBufferAccessorRW(ref hierarchyHandle)[esi.IndexInChunk];
                else
                    hierarchy = esi.Chunk.GetBufferAccessorRW(ref cleanupHandle)[esi.IndexInChunk].Reinterpret<EntityInHierarchy>();

                var tsa = ThreadStackAllocator.GetAllocator();
                foreach (var childIndex in rootChildrenIndices)
                {
                    int elementCount    = batchAddSetStream.BeginForEachIndex(childIndex);
                    var ancestryAddSets = tsa.AllocateAsSpan<TreeKernels.ComponentAddSet>(elementCount);
                    for (int i = 0; i < elementCount; i++)
                        ancestryAddSets[i] = batchAddSetStream.Read<BatchedAddSet>().addSet;
                    // We need to apply this backwards since the order is stored leaf-to-root and we want to apply root-to-leaf
                    for (int i = elementCount - 1; i >= 0; i--)
                        ApplyAddComponentsBatchedPostProcess(ancestryAddSets[i]);

                    TreeKernels.UpdateLocalTransformsOfNewAncestorComponents(ancestryAddSets, hierarchy.AsNativeArray());
                    batchAddSetStream.EndForEachIndex();
                }
                tsa.Dispose();
            }

            void ApplyAddComponentsBatchedPostProcess(TreeKernels.ComponentAddSet addSet)
            {
                if (addSet.noChange)
                    return;
                if (addSet.copyNormalToTicked)
                    tickedWorldTransformLookup[addSet.entity] = worldTransformLookup[addSet.entity].ToTicked();
                else if (addSet.copyTickedToNormal)
                    worldTransformLookup[addSet.entity] = tickedWorldTransformLookup[addSet.entity].ToUnticked();
                else
                {
                    if (addSet.parent != Entity.Null)
                    {
                        if (addSet.setNormalToIdentity)
                        {
                            var parentTransform = worldTransformLookup[addSet.parent];
                            if (!addSet.isCopyParent)
                                parentTransform.worldTransform.stretch = new float3(1f, 1f, 1f);
                            worldTransformLookup[addSet.entity]        = parentTransform;
                        }
                        if (addSet.setTickedToIdentity)
                        {
                            var parentTransform = tickedWorldTransformLookup[addSet.parent];
                            if (!addSet.isCopyParent)
                                parentTransform.worldTransform.stretch = new float3(1f, 1f, 1f);
                            tickedWorldTransformLookup[addSet.entity]  = parentTransform;
                        }
                    }
                    else
                    {
                        if (addSet.setNormalToIdentity)
                            worldTransformLookup[addSet.entity] = new WorldTransform { worldTransform = TransformQvvs.identity };
                        if (addSet.setTickedToIdentity)
                            tickedWorldTransformLookup[addSet.entity] = new TickedWorldTransform { worldTransform = TransformQvvs.identity };
                    }
                }

                if (addSet.linkedEntityGroup)
                {
                    var esi                               = esil[addSet.entity];
                    var leg                               = esi.Chunk.GetBufferAccessorRW(ref legHandle)[esi.IndexInChunk];
                    leg.Add(new LinkedEntityGroup { Value = addSet.entity });
                }
            }
        }
        #endregion

        #region Main Thread
        static unsafe void AddChild(EntityManager em, ref ChildWorkState childWorkState)
        {
            var parentClassification = childWorkState.parentClassification;
            var childClassification  = childWorkState.childClassification;
            var parent               = childWorkState.parent;
            var child                = childWorkState.child;
            var flags                = childWorkState.flags;

            switch (childClassification.role, parentClassification.role)
            {
                case (TreeKernels.TreeClassification.TreeRole.Solo, TreeKernels.TreeClassification.TreeRole.Solo):
                    AddSoloChildToSoloParent(em, ref childWorkState);
                    break;
                case (TreeKernels.TreeClassification.TreeRole.Solo, TreeKernels.TreeClassification.TreeRole.Root):
                    AddSoloChildToRootParent(em, ref childWorkState);
                    break;
                case (TreeKernels.TreeClassification.TreeRole.Solo, TreeKernels.TreeClassification.TreeRole.InternalNoChildren):
                case (TreeKernels.TreeClassification.TreeRole.Solo, TreeKernels.TreeClassification.TreeRole.InternalWithChildren):
                    AddSoloChildToInternalParent(em, ref childWorkState);
                    break;
                case (TreeKernels.TreeClassification.TreeRole.Root, TreeKernels.TreeClassification.TreeRole.Solo):
                    AddRootChildToSoloParent(em, ref childWorkState);
                    break;
                case (TreeKernels.TreeClassification.TreeRole.Root, TreeKernels.TreeClassification.TreeRole.Root):
                    AddRootChildToRootParent(em, ref childWorkState);
                    break;
                case (TreeKernels.TreeClassification.TreeRole.Root, TreeKernels.TreeClassification.TreeRole.InternalNoChildren):
                case (TreeKernels.TreeClassification.TreeRole.Root, TreeKernels.TreeClassification.TreeRole.InternalWithChildren):
                    TreeChangeSafetyChecks.CheckNotAssigningRootChildToDescendant(childWorkState.parent, childWorkState.child, parentClassification);
                    AddRootChildToInternalParent(em, ref childWorkState);
                    break;
                case (TreeKernels.TreeClassification.TreeRole.InternalNoChildren, TreeKernels.TreeClassification.TreeRole.Solo):
                    AddInternalChildWithoutSubtreeToSoloParent(em, ref childWorkState);
                    break;
                case (TreeKernels.TreeClassification.TreeRole.InternalNoChildren, TreeKernels.TreeClassification.TreeRole.Root):
                    if (childWorkState.parent == childClassification.root)
                        AddInternalChildWithoutSubtreeToRootParentSameRoot(em, ref childWorkState);
                    else
                        AddInternalChildWithoutSubtreeToRootParentDifferentRoot(em, ref childWorkState);
                    break;
                case (TreeKernels.TreeClassification.TreeRole.InternalNoChildren, TreeKernels.TreeClassification.TreeRole.InternalNoChildren):
                case (TreeKernels.TreeClassification.TreeRole.InternalNoChildren, TreeKernels.TreeClassification.TreeRole.InternalWithChildren):
                    if (parentClassification.root == childClassification.root)
                        AddInternalChildWithoutSubtreeToInternalParentSameRoot(em, ref childWorkState);
                    else
                        AddInternalChildWithoutSubtreeToInternalParentDifferentRoots(em, ref childWorkState);
                    break;
                case (TreeKernels.TreeClassification.TreeRole.InternalWithChildren, TreeKernels.TreeClassification.TreeRole.Solo):
                    AddInternalChildWithSubtreeToSoloParent(em, ref childWorkState);
                    break;
                case (TreeKernels.TreeClassification.TreeRole.InternalWithChildren, TreeKernels.TreeClassification.TreeRole.Root):
                    if (childWorkState.parent == childClassification.root)
                        AddInternalChildWithSubtreeToRootParentSameRoot(em, ref childWorkState);
                    else
                        AddInternalChildWithSubtreeToRootParentDifferentRoot(em, ref childWorkState);
                    break;
                case (TreeKernels.TreeClassification.TreeRole.InternalWithChildren, TreeKernels.TreeClassification.TreeRole.InternalNoChildren):
                case (TreeKernels.TreeClassification.TreeRole.InternalWithChildren, TreeKernels.TreeClassification.TreeRole.InternalWithChildren):
                    if (parentClassification.root == childClassification.root)
                        AddInternalChildWithSubtreeToInternalParentSameRoot(em, ref childWorkState);
                    else
                        AddInternalChildWithSubtreeToInternalParentDifferentRoots(em, ref childWorkState);
                    break;
            }

            var childHandle = em.GetComponentData<RootReference>(child).ToHandle(em);
            if (flags.HasCopyParent())
            {
                // Set WorldTransform of child and propagate.
                Span<Propagate.WriteCommand> command = stackalloc Propagate.WriteCommand[1];
                command[0]                           = new Propagate.WriteCommand
                {
                    indexInHierarchy = childHandle.indexInHierarchy,
                    writeType        = Propagate.WriteCommand.WriteType.CopyParentParentChanged
                };
                Span<TransformQvvs> dummy = stackalloc TransformQvvs[1];
                em.CompleteDependencyBeforeRW<WorldTransform>();
                var transformLookup = em.GetComponentLookup<WorldTransform>(false);
                if (em.HasComponent<WorldTransform>(child))
                {
                    var ema = new EntityManagerAccess(em);
                    Propagate.WriteAndPropagate(childHandle.m_hierarchy, childHandle.m_extraHierarchy, dummy, command, ref ema, ref ema);
                }
                if (em.HasComponent<TickedWorldTransform>(child))
                {
                    var ema = new TickedEntityManagerAccess(em);
                    Propagate.WriteAndPropagate(childHandle.m_hierarchy, childHandle.m_extraHierarchy, dummy, command, ref ema, ref ema);
                }
            }
            else
            {
                // Compute new local transforms (and propagate if necessary)
                if (em.HasComponent<WorldTransform>(child))
                {
                    var childTransform = em.GetComponentData<WorldTransform>(child);
                    SetWorldTransform(child, in childTransform.worldTransform, em);
                }
                if (em.HasComponent<TickedWorldTransform>(child))
                {
                    var childTransform = em.GetComponentData<TickedWorldTransform>(child);
                    SetTickedWorldTransform(child, in childTransform.worldTransform, em);
                }
            }
        }

        #region Solo Children
        static void AddSoloChildToSoloParent(EntityManager em, ref ChildWorkState childWorkState)
        {
            var parent  = childWorkState.parent;
            var child   = childWorkState.child;
            var flags   = childWorkState.flags;
            var options = childWorkState.options;

            // Construct the hierarchy and copy it to cleanup if needed.
            var hierarchy = em.GetBuffer<EntityInHierarchy>(parent, false);
            TreeKernels.BuildOriginalParentChildHierarchy(ref hierarchy, parent, child, flags);
            if (options == AddChildOptions.IgnoreLinkedEntityGroup)
            {
                var cleanup = em.GetBuffer<EntityInHierarchyCleanup>(parent, false);
                TreeKernels.CopyHierarchyToCleanup(in hierarchy, ref cleanup);
            }
            TreeKernels.UpdateRootReferencesFromDiff(hierarchy.AsNativeArray(), default, em);

            // If we need LEG, add the child to the parent. Then optionally remove the child's LEG.
            if (options != AddChildOptions.IgnoreLinkedEntityGroup)
            {
                var leg = em.GetBuffer<LinkedEntityGroup>(parent, false);
                TreeKernels.AddEntityToLeg(ref leg, child);
                if (options == AddChildOptions.TransferLinkedEntityGroup && em.HasBuffer<LinkedEntityGroup>(child))
                {
                    var childLeg = em.GetBuffer<LinkedEntityGroup>(child, true);
                    if (childLeg.Length < 2)
                        em.RemoveComponent<LinkedEntityGroup>(child);
                }
            }

            Validate(em, parent, child);
        }

        static void AddSoloChildToRootParent(EntityManager em, ref ChildWorkState childWorkState)
        {
            var parent  = childWorkState.parent;
            var child   = childWorkState.child;
            var flags   = childWorkState.flags;
            var options = childWorkState.options;

            var tsa = ThreadStackAllocator.GetAllocator();

            // Clean the root parent, then add the child to it. We can handle cleanup now too since all components have been added.
            var hierarchy = em.GetBuffer<EntityInHierarchy>(parent, false);
            var old       = TreeKernels.CopyHierarchyEntities(ref tsa, hierarchy.AsNativeArray());
            CleanHierarchy(ref tsa, em, parent, ref hierarchy, !childWorkState.addedLeg, out var removeParentLeg);
            TreeKernels.InsertSoloEntityIntoHierarchy(ref hierarchy, 0, child, flags);
            if (options == AddChildOptions.IgnoreLinkedEntityGroup || em.HasBuffer<EntityInHierarchyCleanup>(parent))
            {
                var cleanup = em.GetBuffer<EntityInHierarchyCleanup>(parent, false);
                TreeKernels.CopyHierarchyToCleanup(in hierarchy, ref cleanup);
            }
            TreeKernels.UpdateRootReferencesFromDiff(hierarchy.AsNativeArray(), old, em);

            // Add child to parent's LEG, then optionally remove LEG from child. Also, cleaning might cause parent to drop LEG.
            if (options != AddChildOptions.IgnoreLinkedEntityGroup)
            {
                var leg = em.GetBuffer<LinkedEntityGroup>(parent, false);
                TreeKernels.AddEntityToLeg(ref leg, child);
                if (options == AddChildOptions.TransferLinkedEntityGroup && em.HasBuffer<LinkedEntityGroup>(child))
                {
                    var childLeg = em.GetBuffer<LinkedEntityGroup>(child, true);
                    if (childLeg.Length < 2)
                        em.RemoveComponent<LinkedEntityGroup>(child);
                }
            }
            else if (removeParentLeg)
                em.RemoveComponent<LinkedEntityGroup>(parent);

            Validate(em, parent, child);

            tsa.Dispose();
        }

        static void AddSoloChildToInternalParent(EntityManager em, ref ChildWorkState childWorkState)
        {
            var parentClassification = childWorkState.parentClassification;
            var childClassification  = childWorkState.childClassification;
            var parent               = childWorkState.parent;
            var child                = childWorkState.child;
            var flags                = childWorkState.flags;
            var options              = childWorkState.options;

            var tsa = ThreadStackAllocator.GetAllocator();

            // For this case, we know upfront whether we need LEG or Cleanup. Apply structural changes immediately.
            var childAddSet = childWorkState.childAddSet;
            var root        = parentClassification.root;
            var hierarchy   = GetRootHierarchy(em, parentClassification, true);

            // We insert the new entity into the hierarchy before cleaning, because otherwise we lose where the parent it.
            hierarchy = GetRootHierarchy(em, parentClassification, false);
            var old   = TreeKernels.CopyHierarchyEntities(ref tsa, hierarchy.AsNativeArray());
            TreeKernels.InsertSoloEntityIntoHierarchy(ref hierarchy, parentClassification.indexInHierarchy, child, flags);
            CleanHierarchy(ref tsa, em, parentClassification.root, ref hierarchy, !childWorkState.addedLeg, out var removeRootLeg);
            if (options == AddChildOptions.IgnoreLinkedEntityGroup || (parentClassification.isRootAlive && em.HasBuffer<EntityInHierarchyCleanup>(root)))
            {
                var cleanup = em.GetBuffer<EntityInHierarchyCleanup>(root, false);
                TreeKernels.CopyHierarchyToCleanup(in hierarchy, ref cleanup);
            }
            TreeKernels.UpdateRootReferencesFromDiff(hierarchy.AsNativeArray(), old, em);

            // Add child to parent's LEG, then optionally remove LEG from child. Also, cleaning might cause root to drop LEG.
            if (options != AddChildOptions.IgnoreLinkedEntityGroup)
            {
                var leg = em.GetBuffer<LinkedEntityGroup>(root, false);
                TreeKernels.AddEntityToLeg(ref leg, child);
                if (options == AddChildOptions.TransferLinkedEntityGroup && em.HasBuffer<LinkedEntityGroup>(child))
                {
                    var childLeg = em.GetBuffer<LinkedEntityGroup>(child, true);
                    if (childLeg.Length < 2)
                        em.RemoveComponent<LinkedEntityGroup>(child);
                }
            }
            else if (removeRootLeg)
                em.RemoveComponent<LinkedEntityGroup>(root);

            Validate(em, parent, child);

            tsa.Dispose();
        }
        #endregion

        #region Root Children
        static void AddRootChildToSoloParent(EntityManager em, ref ChildWorkState childWorkState)
        {
            var parent  = childWorkState.parent;
            var child   = childWorkState.child;
            var flags   = childWorkState.flags;
            var options = childWorkState.options;

            var tsa = ThreadStackAllocator.GetAllocator();

            // Clean child hierarchy
            var oldChildHierarchy = em.GetBuffer<EntityInHierarchy>(child);
            CleanHierarchy(ref tsa, em, child, ref oldChildHierarchy, true, out var removeChildLeg);

            // Extract LEG entities
            ProcessRootChildLeg(ref tsa, em, child, oldChildHierarchy.AsNativeArray(), options, out var removeChildLeg2, out var dstHierarchyNeedsCleanup,
                                out var childLegEntities);
            removeChildLeg |= removeChildLeg2;

            // Build new hierarchy
            var hierarchy     = em.GetBuffer<EntityInHierarchy>(parent, false);
            oldChildHierarchy = em.GetBuffer<EntityInHierarchy>(child, true);
            TreeKernels.BuildOriginalParentWithDescendantHierarchy(ref hierarchy, parent, oldChildHierarchy.AsNativeArray(), flags);
            if (options == AddChildOptions.IgnoreLinkedEntityGroup)
            {
                var cleanup = em.GetBuffer<EntityInHierarchyCleanup>(parent, false);
                TreeKernels.CopyHierarchyToCleanup(in hierarchy, ref cleanup);
            }
            TreeKernels.UpdateRootReferencesFromDiff(hierarchy.AsNativeArray(), default, em);

            // Add LEG entities
            if (options != AddChildOptions.IgnoreLinkedEntityGroup)
            {
                var leg = em.GetBuffer<LinkedEntityGroup>(parent, false);
                if (childLegEntities.Length > 0)
                    TreeKernels.AddEntitiesToLeg(ref leg, childLegEntities);
                else
                    TreeKernels.AddEntityToLeg(ref leg, child);
            }

            // Remove old root components from child
            TreeKernels.RemoveRootComponents(em, child, removeChildLeg);

            Validate(em, parent, child);

            tsa.Dispose();
        }

        static void AddRootChildToRootParent(EntityManager em, ref ChildWorkState childWorkState)
        {
            var parent  = childWorkState.parent;
            var child   = childWorkState.child;
            var flags   = childWorkState.flags;
            var options = childWorkState.options;

            var tsa = ThreadStackAllocator.GetAllocator();

            // We know the parent is a root with a valid hierarchy, so we can apply the hierarchy changes and cleaning now.
            var oldChildHierarchy = em.GetBuffer<EntityInHierarchy>(child);
            CleanHierarchy(ref tsa, em, child,  ref oldChildHierarchy, true,                     out var removeChildLeg);
            var hierarchy = em.GetBuffer<EntityInHierarchy>(parent, false);
            var old       = TreeKernels.CopyHierarchyEntities(ref tsa, hierarchy.AsNativeArray());
            CleanHierarchy(ref tsa, em, parent, ref hierarchy,         !childWorkState.addedLeg, out var removeParentLeg);
            TreeKernels.InsertSubtreeIntoHierarchy(ref hierarchy, 0, oldChildHierarchy.AsNativeArray().AsReadOnlySpan(), flags);
            TreeKernels.UpdateRootReferencesFromDiff(hierarchy.AsNativeArray(), old, em);

            // Extract LEG entities
            ProcessRootChildLeg(ref tsa, em, child, oldChildHierarchy.AsNativeArray(), options, out var removeChildLeg2, out var dstHierarchyNeedsCleanup,
                                out var childLegEntities);
            removeChildLeg |= removeChildLeg2;

            // Now we can process cleanup
            if (options == AddChildOptions.IgnoreLinkedEntityGroup || em.HasBuffer<EntityInHierarchyCleanup>(parent))
            {
                var cleanup = em.GetBuffer<EntityInHierarchyCleanup>(parent, false);
                TreeKernels.CopyHierarchyToCleanup(in hierarchy, ref cleanup);
            }

            // Add LEG entities, and maybe remove the parent LEG after cleanup
            if (options != AddChildOptions.IgnoreLinkedEntityGroup)
            {
                var leg = em.GetBuffer<LinkedEntityGroup>(parent, false);
                if (childLegEntities.Length > 0)
                    TreeKernels.AddEntitiesToLeg(ref leg, childLegEntities);
                else
                    TreeKernels.AddEntityToLeg(ref leg, child);
            }
            else if (removeParentLeg)
                em.RemoveComponent<LinkedEntityGroup>(parent);

            // Remove old root components from child
            TreeKernels.RemoveRootComponents(em, child, removeChildLeg);

            Validate(em, parent, child);

            tsa.Dispose();
        }

        static void AddRootChildToInternalParent(EntityManager em, ref ChildWorkState childWorkState)
        {
            var parentClassification = childWorkState.parentClassification;
            var parent               = childWorkState.parent;
            var child                = childWorkState.child;
            var flags                = childWorkState.flags;
            var options              = childWorkState.options;

            var tsa = ThreadStackAllocator.GetAllocator();

            // Get the components to add, but only apply them to the child, since we don't know yet if the parent's root needs cleanup.
            var childAddSet = childWorkState.childAddSet;
            var root        = parentClassification.root;
            var hierarchy   = GetRootHierarchy(em, parentClassification, true);

            // Clean the old hierarchy, then insert it into the new hierarchy while we know the new hierarchy's parent index, and then clean the new hierarchy.
            // Todo: We redundantly clean the entities that move between hierarchies. This could be improved.
            hierarchy             = GetRootHierarchy(em, parentClassification, false);
            var oldChildHierarchy = em.GetBuffer<EntityInHierarchy>(child);
            CleanHierarchy(ref tsa, em, child, ref oldChildHierarchy, true, out var removeChildLeg);
            var old = TreeKernels.CopyHierarchyEntities(ref tsa, hierarchy.AsNativeArray());
            TreeKernels.InsertSubtreeIntoHierarchy(ref hierarchy, parentClassification.indexInHierarchy, oldChildHierarchy.AsNativeArray().AsReadOnlySpan(), flags);
            CleanHierarchy(ref tsa, em, root, ref hierarchy, !childWorkState.addedLeg, out var removeRootLeg);
            TreeKernels.UpdateRootReferencesFromDiff(hierarchy.AsNativeArray(), old, em);

            // Extract LEG entities
            ProcessRootChildLeg(ref tsa, em, child, oldChildHierarchy.AsNativeArray(), options, out var removeChildLeg2, out var dstHierarchyNeedsCleanup,
                                out var childLegEntities);
            removeChildLeg |= removeChildLeg2;

            // Now we can add perform cleanup.
            if (options == AddChildOptions.IgnoreLinkedEntityGroup || (parentClassification.isRootAlive && em.HasBuffer<EntityInHierarchyCleanup>(parent)))
            {
                hierarchy   = em.GetBuffer<EntityInHierarchy>(root, true);
                var cleanup = em.GetBuffer<EntityInHierarchyCleanup>(root, false);
                TreeKernels.CopyHierarchyToCleanup(in hierarchy, ref cleanup);
            }

            // Add LEG entities. Also, cleaning might result in us removing LEG from the root.
            if (options != AddChildOptions.IgnoreLinkedEntityGroup)
            {
                var leg = em.GetBuffer<LinkedEntityGroup>(root, false);
                if (childLegEntities.Length > 0)
                    TreeKernels.AddEntitiesToLeg(ref leg, childLegEntities);
                else
                    TreeKernels.AddEntityToLeg(ref leg, child);
            }
            else if (removeRootLeg)
                em.RemoveComponent<LinkedEntityGroup>(root);

            // Remove old root components from child
            TreeKernels.RemoveRootComponents(em, child, removeChildLeg);

            Validate(em, parent, child);

            tsa.Dispose();
        }

        #endregion

        #region Internal Children without Subtrees
        static void AddInternalChildWithoutSubtreeToSoloParent(EntityManager em, ref ChildWorkState childWorkState)
        {
            var parentClassification = childWorkState.parentClassification;
            var childClassification  = childWorkState.childClassification;
            var parent               = childWorkState.parent;
            var child                = childWorkState.child;
            var flags                = childWorkState.flags;
            var options              = childWorkState.options;

            var tsa = ThreadStackAllocator.GetAllocator();

            // We are only moving one entity, so we know the components to add up front.
            var oldRoot = childClassification.root;

            // We remove the child from the old hierarchy, clean the old hierarchy, and dispatch root references.
            // Note: ProcessInternalChildLegNoSubtree can make structural changes and invalidate buffers.
            var oldChildHierarchy   = GetRootHierarchy(em, childClassification, false);
            var oldRootEntities     = TreeKernels.CopyHierarchyEntities(ref tsa, oldChildHierarchy.AsNativeArray());
            var oldAncestorEntities = GetAncestorEntitiesIfNeededForLeg(ref tsa, oldChildHierarchy.AsNativeArray(), childClassification.indexInHierarchy, options);
            TreeKernels.RemoveSoloFromHierarchy(ref oldChildHierarchy, childClassification.indexInHierarchy);
            CleanHierarchy(ref tsa, em, oldRoot, ref oldChildHierarchy, true, out var removeOldRootLeg);
            TreeKernels.UpdateRootReferencesFromDiff(oldChildHierarchy.AsNativeArray(), oldRootEntities, em);
            bool convertOldRootToSolo = oldChildHierarchy.Length < 2;

            // And then we construct the new hierarchy, and optionally apply cleanup.
            var hierarchy = em.GetBuffer<EntityInHierarchy>(parent, false);
            TreeKernels.BuildOriginalParentChildHierarchy(ref hierarchy, parent, child, flags);
            if (options == AddChildOptions.IgnoreLinkedEntityGroup)
            {
                var cleanup = em.GetBuffer<EntityInHierarchyCleanup>(parent, false);
                TreeKernels.CopyHierarchyToCleanup(in hierarchy, ref cleanup);
            }
            TreeKernels.UpdateRootReferencesFromDiff(hierarchy.AsNativeArray(), default, em);

            // Now process LEG
            ProcessInternalChildLegNoSubtree(em, oldRoot, childClassification.isRootAlive, child, oldAncestorEntities, options, out bool removeChildLeg,
                                             out bool removeOldRootLeg2);
            removeOldRootLeg |= removeOldRootLeg2;
            if (options != AddChildOptions.IgnoreLinkedEntityGroup)
            {
                var leg = em.GetBuffer<LinkedEntityGroup>(parent, false);
                TreeKernels.AddEntityToLeg(ref leg, child);
            }

            // Remove old root components
            if (convertOldRootToSolo)
                TreeKernels.RemoveRootComponents(em, oldRoot, removeOldRootLeg);
            else if (removeOldRootLeg)
                em.RemoveComponent<LinkedEntityGroup>(oldRoot);

            if (removeChildLeg)
                em.RemoveComponent<LinkedEntityGroup>(child);

            Validate(em, parent, child);

            tsa.Dispose();
        }

        static void AddInternalChildWithoutSubtreeToRootParentSameRoot(EntityManager em, ref ChildWorkState childWorkState)
        {
            var childClassification = childWorkState.childClassification;
            var parent              = childWorkState.parent;
            var child               = childWorkState.child;
            var flags               = childWorkState.flags;

            var tsa = ThreadStackAllocator.GetAllocator();

            // We do not need to account for ticked vs unticked in the ancestry, because the root should already have everything
            var hierarchy = em.GetBuffer<EntityInHierarchy>(parent, false);
            var old       = TreeKernels.CopyHierarchyEntities(ref tsa, hierarchy.AsNativeArray());
            TreeKernels.RemoveSoloFromHierarchy(ref hierarchy, childClassification.indexInHierarchy);
            TreeKernels.InsertSoloEntityIntoHierarchy(ref hierarchy, 0, child, flags);
            CleanHierarchy(ref tsa, em, parent, ref hierarchy, true, out var removeLeg);
            if (em.HasBuffer<EntityInHierarchyCleanup>(parent))
            {
                var cleanup = em.GetBuffer<EntityInHierarchyCleanup>(parent, false);
                TreeKernels.CopyHierarchyToCleanup(in hierarchy, ref cleanup);
            }
            TreeKernels.UpdateRootReferencesFromDiff(hierarchy.AsNativeArray(), old, em);

            // Cleaning can still result in LEG being removed.
            if (removeLeg)
                em.RemoveComponent<LinkedEntityGroup>(parent);

            Validate(em, parent, child);

            tsa.Dispose();
        }

        static void AddInternalChildWithoutSubtreeToRootParentDifferentRoot(EntityManager em, ref ChildWorkState childWorkState)
        {
            var childClassification = childWorkState.childClassification;
            var parent              = childWorkState.parent;
            var child               = childWorkState.child;
            var flags               = childWorkState.flags;
            var options             = childWorkState.options;

            var tsa = ThreadStackAllocator.GetAllocator();

            // We are only moving one entity, so we know the components to add up front.
            var oldRoot = childClassification.root;

            // We remove the child from the old hierarchy, clean the old hierarchy, and dispatch root references.
            // Note: ProcessInternalChildLegNoSubtree can make structural changes and invalidate buffers.
            var oldChildHierarchy   = GetRootHierarchy(em, childClassification, false);
            var oldRootEntities     = TreeKernels.CopyHierarchyEntities(ref tsa, oldChildHierarchy.AsNativeArray());
            var oldAncestorEntities = GetAncestorEntitiesIfNeededForLeg(ref tsa, oldChildHierarchy.AsNativeArray(), childClassification.indexInHierarchy, options);
            TreeKernels.RemoveSoloFromHierarchy(ref oldChildHierarchy, childClassification.indexInHierarchy);
            CleanHierarchy(ref tsa, em, oldRoot, ref oldChildHierarchy, true, out var removeOldRootLeg);
            TreeKernels.UpdateRootReferencesFromDiff(oldChildHierarchy.AsNativeArray(), oldRootEntities, em);
            bool convertOldRootToSolo = oldChildHierarchy.Length < 2;

            // And then we insert the child into the new hierarchy. Since the parent is the root, we do so after cleaning.
            var hierarchy = em.GetBuffer<EntityInHierarchy>(parent, false);
            var old       = TreeKernels.CopyHierarchyEntities(ref tsa, hierarchy.AsNativeArray());
            CleanHierarchy(ref tsa, em, parent, ref hierarchy, !childWorkState.addedLeg, out var removeParentLeg);
            TreeKernels.InsertSoloEntityIntoHierarchy(ref hierarchy, 0, child, flags);
            if (options == AddChildOptions.IgnoreLinkedEntityGroup || em.HasBuffer<EntityInHierarchyCleanup>(parent))
            {
                var cleanup = em.GetBuffer<EntityInHierarchyCleanup>(parent, false);
                TreeKernels.CopyHierarchyToCleanup(in hierarchy, ref cleanup);
            }
            TreeKernels.UpdateRootReferencesFromDiff(hierarchy.AsNativeArray(), old, em);

            // Now process LEG
            ProcessInternalChildLegNoSubtree(em, oldRoot, childClassification.isRootAlive, child, oldAncestorEntities, options, out bool removeChildLeg,
                                             out bool removeOldRootLeg2);
            removeOldRootLeg |= removeOldRootLeg2;
            if (options != AddChildOptions.IgnoreLinkedEntityGroup)
            {
                var leg = em.GetBuffer<LinkedEntityGroup>(parent, false);
                TreeKernels.AddEntityToLeg(ref leg, child);
            }

            // Remove old root components
            if (convertOldRootToSolo)
                TreeKernels.RemoveRootComponents(em, oldRoot, removeOldRootLeg);
            else if (removeOldRootLeg)
                em.RemoveComponent<LinkedEntityGroup>(oldRoot);

            if (removeChildLeg)
                em.RemoveComponent<LinkedEntityGroup>(child);
            if (removeParentLeg)
                em.RemoveComponent<LinkedEntityGroup>(parent);

            Validate(em, parent, child);

            tsa.Dispose();
        }

        static void AddInternalChildWithoutSubtreeToInternalParentSameRoot(EntityManager em, ref ChildWorkState childWorkState)
        {
            var parentClassification = childWorkState.parentClassification;
            var childClassification  = childWorkState.childClassification;
            var parent               = childWorkState.parent;
            var child                = childWorkState.child;
            var flags                = childWorkState.flags;

            var tsa = ThreadStackAllocator.GetAllocator();

            // We still need to account for ticked vs unticked in the ancestry
            var childAddSet = childWorkState.childAddSet;
            var hierarchy   = GetRootHierarchy(em, parentClassification, false);

            hierarchy = GetRootHierarchy(em, parentClassification, false);
            var old   = TreeKernels.CopyHierarchyEntities(ref tsa, hierarchy.AsNativeArray());
            TreeKernels.RemoveSoloFromHierarchy(ref hierarchy, childClassification.indexInHierarchy);
            // When we remove from the hierarchy, our parent's index might have shifted by an index if the child preceeded the parent
            if (parentClassification.indexInHierarchy >= hierarchy.Length || hierarchy[parentClassification.indexInHierarchy].entity != parent)
                parentClassification.indexInHierarchy--;
            TreeKernels.InsertSoloEntityIntoHierarchy(ref hierarchy, parentClassification.indexInHierarchy, child, flags);
            CleanHierarchy(ref tsa, em, parentClassification.root, ref hierarchy, parentClassification.isRootAlive, out var removeLeg);
            if (parentClassification.isRootAlive && em.HasBuffer<EntityInHierarchyCleanup>(parentClassification.root))
            {
                var cleanup = em.GetBuffer<EntityInHierarchyCleanup>(parentClassification.root, false);
                TreeKernels.CopyHierarchyToCleanup(in hierarchy, ref cleanup);
            }
            TreeKernels.UpdateRootReferencesFromDiff(hierarchy.AsNativeArray(), old, em);

            // Cleaning can still result in LEG being removed.
            if (removeLeg)
                em.RemoveComponent<LinkedEntityGroup>(parentClassification.root);

            Validate(em, parent, child);

            tsa.Dispose();
        }

        static void AddInternalChildWithoutSubtreeToInternalParentDifferentRoots(EntityManager em, ref ChildWorkState childWorkState)
        {
            var parentClassification = childWorkState.parentClassification;
            var childClassification  = childWorkState.childClassification;
            var parent               = childWorkState.parent;
            var child                = childWorkState.child;
            var flags                = childWorkState.flags;
            var options              = childWorkState.options;

            var tsa = ThreadStackAllocator.GetAllocator();

            // We are only moving one entity, so we know the components to add up front.
            var oldRoot     = childClassification.root;
            var root        = parentClassification.root;
            var childAddSet = childWorkState.childAddSet;
            var hierarchy   = GetRootHierarchy(em, parentClassification, false);

            // We remove the child from the old hierarchy, clean the old hierarchy, and dispatch root references.
            // Note: ProcessInternalChildLegNoSubtree can make structural changes and invalidate buffers.
            var oldChildHierarchy   = GetRootHierarchy(em, childClassification, false);
            var oldRootEntities     = TreeKernels.CopyHierarchyEntities(ref tsa, oldChildHierarchy.AsNativeArray());
            var oldAncestorEntities = GetAncestorEntitiesIfNeededForLeg(ref tsa, oldChildHierarchy.AsNativeArray(), childClassification.indexInHierarchy, options);
            TreeKernels.RemoveSoloFromHierarchy(ref oldChildHierarchy, childClassification.indexInHierarchy);
            CleanHierarchy(ref tsa, em, oldRoot, ref oldChildHierarchy, true, out var removeOldRootLeg);
            TreeKernels.UpdateRootReferencesFromDiff(oldChildHierarchy.AsNativeArray(), oldRootEntities, em);
            bool convertOldRootToSolo = oldChildHierarchy.Length < 2;

            // And then we insert the child into the new hierarchy. We do this before cleaning while we know the index of the parent.
            hierarchy = GetRootHierarchy(em, parentClassification, false);
            var old   = TreeKernels.CopyHierarchyEntities(ref tsa, hierarchy.AsNativeArray());
            TreeKernels.InsertSoloEntityIntoHierarchy(ref hierarchy, parentClassification.indexInHierarchy, child, flags);
            CleanHierarchy(ref tsa, em, root, ref hierarchy, !childWorkState.addedLeg, out var removeRootLeg);
            if (options == AddChildOptions.IgnoreLinkedEntityGroup || em.HasBuffer<EntityInHierarchyCleanup>(parent))
            {
                var cleanup = em.GetBuffer<EntityInHierarchyCleanup>(root, false);
                TreeKernels.CopyHierarchyToCleanup(in hierarchy, ref cleanup);
            }
            TreeKernels.UpdateRootReferencesFromDiff(hierarchy.AsNativeArray(), old, em);

            // Now process LEG
            ProcessInternalChildLegNoSubtree(em, oldRoot, childClassification.isRootAlive, child, oldAncestorEntities, options, out bool removeChildLeg,
                                             out bool removeOldRootLeg2);
            removeOldRootLeg |= removeOldRootLeg2;
            if (options != AddChildOptions.IgnoreLinkedEntityGroup)
            {
                var leg = em.GetBuffer<LinkedEntityGroup>(root, false);
                TreeKernels.AddEntityToLeg(ref leg, child);
            }

            // Remove old root components
            if (convertOldRootToSolo)
                TreeKernels.RemoveRootComponents(em, oldRoot, removeOldRootLeg);
            else if (removeOldRootLeg)
                em.RemoveComponent<LinkedEntityGroup>(oldRoot);

            if (removeChildLeg)
                em.RemoveComponent<LinkedEntityGroup>(child);
            if (removeRootLeg)
                em.RemoveComponent<LinkedEntityGroup>(root);

            Validate(em, parent, child);

            tsa.Dispose();
        }
        #endregion

        #region Internal Children with Subtrees
        static void AddInternalChildWithSubtreeToSoloParent(EntityManager em, ref ChildWorkState childWorkState)
        {
            var childClassification = childWorkState.childClassification;
            var parent              = childWorkState.parent;
            var child               = childWorkState.child;
            var flags               = childWorkState.flags;
            var options             = childWorkState.options;

            var tsa = ThreadStackAllocator.GetAllocator();

            // Add only the components for the child. We don't yet know if the new root needs cleanup or not.
            var oldRoot = childClassification.root;

            // We need the hierarchy index to extract the subtree, but we also need a clean subtree to get accurate LEG list.
            // Thus, we clean the hierarchy first, then find our entity in it. Then we can extract the subtree.
            var oldHierarchy        = GetRootHierarchy(em, childClassification, false);
            var oldChildEntities    = TreeKernels.CopyHierarchyEntities(ref tsa, oldHierarchy.AsNativeArray());
            var oldAncestorEntities = GetAncestorEntitiesIfNeededForLeg(ref tsa, oldHierarchy.AsNativeArray(), childClassification.indexInHierarchy, options);
            CleanHierarchy(ref tsa, em, oldRoot, ref oldHierarchy, childClassification.isRootAlive, out bool removeOldRootLeg);
            childClassification.indexInHierarchy = TreeKernels.FindEntityAfterCleaning(oldHierarchy.AsNativeArray(), child, childClassification.indexInHierarchy);
            var subtree                          = TreeKernels.ExtractSubtree(ref tsa, oldHierarchy.AsNativeArray(), childClassification.indexInHierarchy);
            TreeKernels.RemoveSubtreeFromHierarchy(ref tsa, ref oldHierarchy, childClassification.indexInHierarchy, subtree);
            TreeKernels.UpdateRootReferencesFromDiff(oldHierarchy.AsNativeArray(), oldChildEntities, em);
            bool convertOldRootToSolo = oldHierarchy.Length < 2;

            // Next, we need to remove the LEG from the old hierarchy
            ProcessInternalChildLegWithSubtree(ref tsa,
                                               em,
                                               oldRoot,
                                               childClassification.isRootAlive,
                                               child,
                                               oldAncestorEntities,
                                               subtree,
                                               options,
                                               out var removeChildLeg,
                                               out var removeOldRootLeg2,
                                               out var dstHierarchyNeedsCleanup,
                                               out var legEntitiesToAddToDst);
            removeOldRootLeg |= removeOldRootLeg2;

            // Build new hierarchy
            var hierarchy = em.GetBuffer<EntityInHierarchy>(parent, false);
            TreeKernels.BuildOriginalParentWithDescendantHierarchy(ref hierarchy, parent, subtree, flags);
            if (options == AddChildOptions.IgnoreLinkedEntityGroup)
            {
                var cleanup = em.GetBuffer<EntityInHierarchyCleanup>(parent, false);
                TreeKernels.CopyHierarchyToCleanup(in hierarchy, ref cleanup);
            }
            TreeKernels.UpdateRootReferencesFromDiff(hierarchy.AsNativeArray(), default, em);

            if (options != AddChildOptions.IgnoreLinkedEntityGroup)
            {
                var leg = em.GetBuffer<LinkedEntityGroup>(parent, false);
                if (legEntitiesToAddToDst.IsEmpty)
                    TreeKernels.AddEntityToLeg(ref leg, child);
                else
                    TreeKernels.AddEntitiesToLeg(ref leg, legEntitiesToAddToDst);
            }

            // Remove old root components
            if (convertOldRootToSolo)
                TreeKernels.RemoveRootComponents(em, oldRoot, removeOldRootLeg);
            else if (removeOldRootLeg)
                em.RemoveComponent<LinkedEntityGroup>(oldRoot);

            if (removeChildLeg)
                em.RemoveComponent<LinkedEntityGroup>(child);
            if (removeOldRootLeg)
                em.RemoveComponent<LinkedEntityGroup>(oldRoot);

            Validate(em, parent, child);

            tsa.Dispose();
        }

        static void AddInternalChildWithSubtreeToRootParentSameRoot(EntityManager em, ref ChildWorkState childWorkState)
        {
            var childClassification = childWorkState.childClassification;
            var parent              = childWorkState.parent;
            var child               = childWorkState.child;
            var flags               = childWorkState.flags;

            var tsa = ThreadStackAllocator.GetAllocator();

            // We do not need to account for ticked vs unticked in the ancestry, because the root should already have everything
            var hierarchy = em.GetBuffer<EntityInHierarchy>(parent, false);
            var old       = TreeKernels.CopyHierarchyEntities(ref tsa, hierarchy.AsNativeArray());
            var subtree   = TreeKernels.ExtractSubtree(ref tsa, hierarchy.AsNativeArray(), childClassification.indexInHierarchy);
            TreeKernels.RemoveSubtreeFromHierarchy(ref tsa, ref hierarchy, childClassification.indexInHierarchy, subtree);
            TreeKernels.InsertSubtreeIntoHierarchy(ref hierarchy, 0, subtree, flags);
            CleanHierarchy(ref tsa, em, parent, ref hierarchy, true, out var removeLeg);
            if (em.HasBuffer<EntityInHierarchyCleanup>(parent))
            {
                var cleanup = em.GetBuffer<EntityInHierarchyCleanup>(parent, false);
                TreeKernels.CopyHierarchyToCleanup(in hierarchy, ref cleanup);
            }
            TreeKernels.UpdateRootReferencesFromDiff(hierarchy.AsNativeArray(), old, em);

            // Cleaning can still result in LEG being removed.
            if (removeLeg)
                em.RemoveComponent<LinkedEntityGroup>(parent);

            Validate(em, parent, child);

            tsa.Dispose();
        }

        static void AddInternalChildWithSubtreeToRootParentDifferentRoot(EntityManager em, ref ChildWorkState childWorkState)
        {
            var childClassification = childWorkState.childClassification;
            var parent              = childWorkState.parent;
            var child               = childWorkState.child;
            var flags               = childWorkState.flags;
            var options             = childWorkState.options;

            var tsa = ThreadStackAllocator.GetAllocator();

            // Add only the components for the child. We don't yet know if the new root needs cleanup or not.
            var oldRoot = childClassification.root;

            // We need the hierarchy index to extract the subtree, but we also need a clean subtree to get accurate LEG list.
            // Thus, we clean the hierarchy first, then find our entity in it. Then we can extract the subtree.
            var oldHierarchy        = GetRootHierarchy(em, childClassification, false);
            var oldChildEntities    = TreeKernels.CopyHierarchyEntities(ref tsa, oldHierarchy.AsNativeArray());
            var oldAncestorEntities = GetAncestorEntitiesIfNeededForLeg(ref tsa, oldHierarchy.AsNativeArray(), childClassification.indexInHierarchy, options);
            CleanHierarchy(ref tsa, em, oldRoot, ref oldHierarchy, childClassification.isRootAlive, out bool removeOldRootLeg);
            childClassification.indexInHierarchy = TreeKernels.FindEntityAfterCleaning(oldHierarchy.AsNativeArray(), child, childClassification.indexInHierarchy);
            var subtree                          = TreeKernels.ExtractSubtree(ref tsa, oldHierarchy.AsNativeArray(), childClassification.indexInHierarchy);
            TreeKernels.RemoveSubtreeFromHierarchy(ref tsa, ref oldHierarchy, childClassification.indexInHierarchy, subtree);
            TreeKernels.UpdateRootReferencesFromDiff(oldHierarchy.AsNativeArray(), oldChildEntities, em);
            bool convertOldRootToSolo = oldHierarchy.Length < 2;

            // Next, we need to remove the LEG from the old hierarchy
            ProcessInternalChildLegWithSubtree(ref tsa,
                                               em,
                                               oldRoot,
                                               childClassification.isRootAlive,
                                               child,
                                               oldAncestorEntities,
                                               subtree,
                                               options,
                                               out var removeChildLeg,
                                               out var removeOldRootLeg2,
                                               out var dstHierarchyNeedsCleanup,
                                               out var legEntitiesToAddToDst);
            removeOldRootLeg |= removeOldRootLeg2;

            // Build new hierarchy
            var hierarchy = em.GetBuffer<EntityInHierarchy>(parent, false);
            var old       = TreeKernels.CopyHierarchyEntities(ref tsa, hierarchy.AsNativeArray());
            CleanHierarchy(ref tsa, em, parent, ref hierarchy, !childWorkState.addedLeg, out var removeParentLeg);
            TreeKernels.InsertSubtreeIntoHierarchy(ref hierarchy, 0, subtree, flags);
            if (options == AddChildOptions.IgnoreLinkedEntityGroup)
            {
                var cleanup = em.GetBuffer<EntityInHierarchyCleanup>(parent, false);
                TreeKernels.CopyHierarchyToCleanup(in hierarchy, ref cleanup);
            }
            TreeKernels.UpdateRootReferencesFromDiff(hierarchy.AsNativeArray(), old, em);

            if (options != AddChildOptions.IgnoreLinkedEntityGroup)
            {
                var leg = em.GetBuffer<LinkedEntityGroup>(parent, false);
                if (legEntitiesToAddToDst.IsEmpty)
                    TreeKernels.AddEntityToLeg(ref leg, child);
                else
                    TreeKernels.AddEntitiesToLeg(ref leg, legEntitiesToAddToDst);
            }

            // Remove old root components
            if (convertOldRootToSolo)
                TreeKernels.RemoveRootComponents(em, oldRoot, removeOldRootLeg);
            else if (removeOldRootLeg)
                em.RemoveComponent<LinkedEntityGroup>(oldRoot);

            if (removeChildLeg)
                em.RemoveComponent<LinkedEntityGroup>(child);
            if (removeOldRootLeg)
                em.RemoveComponent<LinkedEntityGroup>(oldRoot);
            if (removeParentLeg)
                em.RemoveComponent<LinkedEntityGroup>(parent);

            Validate(em, parent, child);

            tsa.Dispose();
        }

        static void AddInternalChildWithSubtreeToInternalParentSameRoot(EntityManager em, ref ChildWorkState childWorkState)
        {
            var parentClassification = childWorkState.parentClassification;
            var childClassification  = childWorkState.childClassification;
            var parent               = childWorkState.parent;
            var child                = childWorkState.child;
            var flags                = childWorkState.flags;

            var tsa = ThreadStackAllocator.GetAllocator();

            // We still need to account for ticked vs unticked in the ancestry
            var childAddSet = childWorkState.childAddSet;
            var hierarchy   = GetRootHierarchy(em, parentClassification, false);

            hierarchy   = GetRootHierarchy(em, parentClassification, false);
            var old     = TreeKernels.CopyHierarchyEntities(ref tsa, hierarchy.AsNativeArray());
            var subtree = TreeKernels.ExtractSubtree(ref tsa, hierarchy.AsNativeArray(), childClassification.indexInHierarchy);
            TreeKernels.RemoveSubtreeFromHierarchy(ref tsa, ref hierarchy, childClassification.indexInHierarchy, subtree);
            // When we remove from the hierarchy, our parent's index might have moved and we need to refind it
            parentClassification.indexInHierarchy = TreeKernels.FindEntityAfterCleaning(hierarchy.AsNativeArray(), parent, parentClassification.indexInHierarchy);
            TreeKernels.InsertSubtreeIntoHierarchy(ref hierarchy, parentClassification.indexInHierarchy, subtree, flags);
            CleanHierarchy(ref tsa, em, parentClassification.root, ref hierarchy, parentClassification.isRootAlive, out var removeLeg);
            if (parentClassification.isRootAlive && em.HasBuffer<EntityInHierarchyCleanup>(parentClassification.root))
            {
                var cleanup = em.GetBuffer<EntityInHierarchyCleanup>(parentClassification.root, false);
                TreeKernels.CopyHierarchyToCleanup(in hierarchy, ref cleanup);
            }
            TreeKernels.UpdateRootReferencesFromDiff(hierarchy.AsNativeArray(), old, em);

            // Cleaning can still result in LEG being removed.
            if (removeLeg)
                em.RemoveComponent<LinkedEntityGroup>(parentClassification.root);

            Validate(em, parent, child);

            tsa.Dispose();
        }

        static void AddInternalChildWithSubtreeToInternalParentDifferentRoots(EntityManager em, ref ChildWorkState childWorkState)
        {
            var parentClassification = childWorkState.parentClassification;
            var childClassification  = childWorkState.childClassification;
            var parent               = childWorkState.parent;
            var child                = childWorkState.child;
            var flags                = childWorkState.flags;
            var options              = childWorkState.options;

            var tsa = ThreadStackAllocator.GetAllocator();

            // Add only the components for the child. We don't yet know if the new root needs cleanup or not.
            var oldRoot     = childClassification.root;
            var root        = parentClassification.root;
            var childAddSet = childWorkState.childAddSet;
            var hierarchy   = GetRootHierarchy(em, parentClassification, true);

            // We need the hierarchy index to extract the subtree, but we also need a clean subtree to get accurate LEG list.
            // Thus, we clean the hierarchy first, then find our entity in it. Then we can extract the subtree.
            var oldHierarchy        = GetRootHierarchy(em, childClassification, false);
            var oldChildEntities    = TreeKernels.CopyHierarchyEntities(ref tsa, oldHierarchy.AsNativeArray());
            var oldAncestorEntities = GetAncestorEntitiesIfNeededForLeg(ref tsa, oldHierarchy.AsNativeArray(), childClassification.indexInHierarchy, options);
            CleanHierarchy(ref tsa, em, oldRoot, ref oldHierarchy, childClassification.isRootAlive, out bool removeOldRootLeg);
            childClassification.indexInHierarchy = TreeKernels.FindEntityAfterCleaning(oldHierarchy.AsNativeArray(), child, childClassification.indexInHierarchy);
            var subtree                          = TreeKernels.ExtractSubtree(ref tsa, oldHierarchy.AsNativeArray(), childClassification.indexInHierarchy);
            TreeKernels.RemoveSubtreeFromHierarchy(ref tsa, ref oldHierarchy, childClassification.indexInHierarchy, subtree);
            TreeKernels.UpdateRootReferencesFromDiff(oldHierarchy.AsNativeArray(), oldChildEntities, em);
            bool convertOldRootToSolo = oldHierarchy.Length < 2;

            // Next, we need to remove the LEG from the old hierarchy
            ProcessInternalChildLegWithSubtree(ref tsa,
                                               em,
                                               oldRoot,
                                               childClassification.isRootAlive,
                                               child,
                                               oldAncestorEntities,
                                               subtree,
                                               options,
                                               out var removeChildLeg,
                                               out var removeOldRootLeg2,
                                               out var dstHierarchyNeedsCleanup,
                                               out var legEntitiesToAddToDst);
            removeOldRootLeg |= removeOldRootLeg2;

            // Build new hierarchy
            hierarchy = GetRootHierarchy(em, parentClassification, false);
            var old   = TreeKernels.CopyHierarchyEntities(ref tsa, hierarchy.AsNativeArray());
            CleanHierarchy(ref tsa, em, root, ref hierarchy, !childWorkState.addedLeg, out var removeRootLeg);
            TreeKernels.InsertSubtreeIntoHierarchy(ref hierarchy, parentClassification.indexInHierarchy, subtree, flags);
            if (options == AddChildOptions.IgnoreLinkedEntityGroup)
            {
                var cleanup = em.GetBuffer<EntityInHierarchyCleanup>(root, false);
                TreeKernels.CopyHierarchyToCleanup(in hierarchy, ref cleanup);
            }
            TreeKernels.UpdateRootReferencesFromDiff(hierarchy.AsNativeArray(), old, em);

            if (options != AddChildOptions.IgnoreLinkedEntityGroup)
            {
                var leg = em.GetBuffer<LinkedEntityGroup>(root, false);
                if (legEntitiesToAddToDst.IsEmpty)
                    TreeKernels.AddEntityToLeg(ref leg, child);
                else
                    TreeKernels.AddEntitiesToLeg(ref leg, legEntitiesToAddToDst);
            }

            // Remove old root components
            if (convertOldRootToSolo)
                TreeKernels.RemoveRootComponents(em, oldRoot, removeOldRootLeg);
            else if (removeOldRootLeg)
                em.RemoveComponent<LinkedEntityGroup>(oldRoot);

            if (removeChildLeg)
                em.RemoveComponent<LinkedEntityGroup>(child);
            if (removeOldRootLeg)
                em.RemoveComponent<LinkedEntityGroup>(oldRoot);
            if (removeRootLeg)
                em.RemoveComponent<LinkedEntityGroup>(root);

            Validate(em, parent, child);

            tsa.Dispose();
        }
        #endregion

        #region Subprocesses
        static DynamicBuffer<EntityInHierarchy> GetRootHierarchy(EntityManager em, TreeKernels.TreeClassification classification, bool isReadOnly)
        {
            if (classification.isRootAlive)
                return em.GetBuffer<EntityInHierarchy>(classification.root, isReadOnly);
            else
                return em.GetBuffer<EntityInHierarchyCleanup>(classification.root, isReadOnly).Reinterpret<EntityInHierarchy>();
        }

        static void CleanHierarchy(ref ThreadStackAllocator parentTsa,
                                   EntityManager em,
                                   Entity rootToClean,
                                   ref DynamicBuffer<EntityInHierarchy> hierarchy,
                                   bool checkForLeg,
                                   out bool removeLeg)
        {
            removeLeg = false;
            if (checkForLeg && em.HasBuffer<LinkedEntityGroup>(rootToClean))
            {
                var leg = em.GetBuffer<LinkedEntityGroup>(rootToClean, false);
                TreeKernels.RemoveDeadDescendantsFromHierarchyAndLeg(ref parentTsa, ref hierarchy, ref leg, em);
                removeLeg = leg.Length < 2;
            }
            else
                TreeKernels.RemoveDeadDescendantsFromHierarchy(ref parentTsa, ref hierarchy, em);
        }

        static void ProcessRootChildLeg(ref ThreadStackAllocator tsa,
                                        EntityManager em,
                                        Entity child,
                                        in ReadOnlySpan<EntityInHierarchy> hierarchy,
                                        AddChildOptions options,
                                        out bool removeLeg,
                                        out bool dstHierarchyNeedsCleanup,
                                        out Span<Entity>                   entitiesInLegAndHierarchy)
        {
            dstHierarchyNeedsCleanup = false;
            removeLeg                = false;
            if (options != AddChildOptions.IgnoreLinkedEntityGroup && em.HasBuffer<LinkedEntityGroup>(child))
            {
                var  childLeg = em.GetBuffer<LinkedEntityGroup>(child, options == AddChildOptions.AttachLinkedEntityGroup);
                bool matchedAll;
                if (options == AddChildOptions.AttachLinkedEntityGroup)
                    entitiesInLegAndHierarchy = TreeKernels.GetHierarchyEntitiesInLeg(ref tsa, hierarchy, childLeg.Reinterpret<Entity>().AsNativeArray(), out matchedAll);
                else
                {
                    entitiesInLegAndHierarchy = TreeKernels.GetAndRemoveHierarchyEntitiesFromLeg(ref tsa, ref childLeg, hierarchy, out matchedAll);
                    if (childLeg.Length < 2)
                        removeLeg = true;
                }
                dstHierarchyNeedsCleanup = !matchedAll;
            }
            else
            {
                dstHierarchyNeedsCleanup  = true;
                entitiesInLegAndHierarchy = default;
            }
        }

        static Span<Entity> GetAncestorEntitiesIfNeededForLeg(ref ThreadStackAllocator tsa, in ReadOnlySpan<EntityInHierarchy> hierarchy, int childIndex, AddChildOptions options)
        {
            if (options == AddChildOptions.IgnoreLinkedEntityGroup)
                return default;
            return TreeKernels.GetAncestryEntitiesExcludingRoot(ref tsa, hierarchy, childIndex);
        }

        static void ProcessInternalChildLegNoSubtree(EntityManager em,
                                                     Entity root,
                                                     bool isRootAlive,
                                                     Entity child,
                                                     in ReadOnlySpan<Entity> ancestorEntities,
                                                     AddChildOptions options,
                                                     out bool removeLegFromChild,
                                                     out bool removeLegFromRoot)
        {
            removeLegFromChild = false;
            removeLegFromRoot  = false;
            if (options == AddChildOptions.IgnoreLinkedEntityGroup)
                return;

            if (options == AddChildOptions.TransferLinkedEntityGroup && em.HasBuffer<LinkedEntityGroup>(child))
            {
                if (em.GetBuffer<LinkedEntityGroup>(child, true).Length < 2)
                    removeLegFromChild = true;
            }

            if (isRootAlive && em.HasBuffer<LinkedEntityGroup>(root))
            {
                var rootLeg = em.GetBuffer<LinkedEntityGroup>(root, false);
                TreeKernels.RemoveEntityFromLeg(ref rootLeg, child, out var matched);
                removeLegFromRoot = rootLeg.Length < 2;
            }

            foreach (var e in ancestorEntities)
            {
                if (em.IsAlive(e) && em.HasBuffer<LinkedEntityGroup>(e))
                {
                    var leg = em.GetBuffer<LinkedEntityGroup>(e);
                    TreeKernels.RemoveEntityFromLeg(ref leg, child, out _);
                    if (leg.Length < 2)
                        em.RemoveComponent<LinkedEntityGroup>(e);
                }
            }
        }

        static void ProcessInternalChildLegWithSubtree(ref ThreadStackAllocator tsa,
                                                       EntityManager em,
                                                       Entity root,
                                                       bool isRootAlive,
                                                       Entity child,
                                                       in ReadOnlySpan<Entity>            ancestorEntities,
                                                       in ReadOnlySpan<EntityInHierarchy> subtree,
                                                       AddChildOptions options,
                                                       out bool removeLegFromChild,
                                                       out bool removeLegFromRoot,
                                                       out bool dstHierarchyNeedsCleanup,
                                                       out Span<Entity>                   legEntitiesToAddToDst)
        {
            removeLegFromChild       = false;
            removeLegFromRoot        = false;
            dstHierarchyNeedsCleanup = true;
            legEntitiesToAddToDst    = default;
            if (options == AddChildOptions.IgnoreLinkedEntityGroup)
                return;

            if (options == AddChildOptions.TransferLinkedEntityGroup && em.HasBuffer<LinkedEntityGroup>(child))
            {
                var childLeg = em.GetBuffer<LinkedEntityGroup>(child, false);
                TreeKernels.RemoveHierarchyEntitiesFromLeg(ref childLeg, subtree);
                if (childLeg.Length < 2)
                    removeLegFromChild = true;
            }

            if (isRootAlive && em.HasBuffer<LinkedEntityGroup>(root))
            {
                var rootLeg              = em.GetBuffer<LinkedEntityGroup>(root, false);
                legEntitiesToAddToDst    = TreeKernels.GetAndRemoveHierarchyEntitiesFromLeg(ref tsa, ref rootLeg, subtree, out bool matchedAll);
                removeLegFromRoot        = rootLeg.Length < 2;
                dstHierarchyNeedsCleanup = !matchedAll;
            }

            foreach (var e in ancestorEntities)
            {
                if (em.IsAlive(e) && em.HasBuffer<LinkedEntityGroup>(e))
                {
                    var leg = em.GetBuffer<LinkedEntityGroup>(e);
                    TreeKernels.RemoveHierarchyEntitiesFromLeg(ref leg, subtree);
                    if (leg.Length < 2)
                        em.RemoveComponent<LinkedEntityGroup>(e);
                }
            }
        }
        #endregion

        [Conditional("VALIDATE")]
        static void Validate(EntityManager em, Entity parent, Entity child)
        {
            var rootRef = em.GetComponentData<RootReference>(child);
            var handle  = rootRef.ToHandle(em);
            if (handle.entity != child)
                throw new System.InvalidOperationException("Child handle is invalid.");
            var parentHandle = handle.bloodParent;
            if (parentHandle.entity != parent)
                throw new System.InvalidOperationException("Parent handle is invalid.");
            var last = handle.GetFromIndexInHierarchy(handle.totalInHierarchy - 1);
            if (last.m_hierarchy[last.indexInHierarchy].firstChildIndex != last.totalInHierarchy)
            {
                throw new System.InvalidOperationException($"Bad things happened during validation. Last did not match hierarchy length. root: {handle.root.entity}");
            }
            for (int i = 1; i < last.m_hierarchy.Length; i++)
            {
                var b = last.m_hierarchy[i];
                var a = last.m_hierarchy[i - 1];
                if (b.firstChildIndex != a.firstChildIndex + a.childCount)
                    throw new System.InvalidOperationException($"Bad things happened during validation. Index {i} has bad indexing. root: {handle.root.entity}");
            }
            if (handle.entity != child || parentHandle.entity != parent)
            {
                throw new System.InvalidOperationException("Our entities got mixed up.");
            }
        }
        #endregion
    }
}

