//#define VALIDATE

using System;
using System.Diagnostics;
using Latios.Unsafe;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Entities.Exposed;
using Unity.Mathematics;

namespace Latios.Transforms
{
    public static partial class TransformTools
    {
        #region API
        /// <summary>
        /// Assigns a new parent to the entity, updating all hierarchy information between the two entities involved.
        /// If the child entity is missing its WorldTransform (or TickedWorldTransform if it has TickedEntityTag),
        /// then that component will be added. The parent and all ancestry will have WorldTransform and/or
        /// TickedWorldTransform added to match what is present on the child.
        /// </summary>
        /// <param name="parent">The target parent</param>
        /// <param name="child">The entity which should have its parent assigned</param>
        /// <param name="inheritanceFlags">The inheritance flags the child will use</param>
        /// <param name="transferLinkedEntityGroup">If the child entity is a standalone entity or is a hierarchy root,
        /// then its entire LinkedEntityGroup (or itself if it doesn't have one) will be appended to the destination
        /// hierarchy root. If the entity is already a child of a different hierarchy, then only entities within its
        /// subtree which are in its original hierarchy's LinkedEntityGroup will be transferred. If the child already
        /// belonged to the destination hierarchy, the LinkedEntityGroup buffer will not be touched.</param>
        public static unsafe void AddChild(this EntityManager em,
                                           Entity parent,
                                           Entity child,
                                           InheritanceFlags inheritanceFlags          = InheritanceFlags.Normal,
                                           bool transferLinkedEntityGroup = true)
        {
            CheckChangeParent(em, parent, child, inheritanceFlags, transferLinkedEntityGroup);

            bool parentHasRootRef   = em.HasComponent<RootReference>(parent);
            bool childHasRootRef    = em.HasComponent<RootReference>(child);
            bool parentHasHierarchy = !parentHasRootRef && em.HasBuffer<EntityInHierarchy>(parent);
            bool childHasHierarchy  = !childHasRootRef && em.HasBuffer<EntityInHierarchy>(child);

            if (!parentHasRootRef && !childHasRootRef && !parentHasHierarchy && !childHasHierarchy)
                AddSoloChildToSoloParent(em, parent, child, inheritanceFlags, transferLinkedEntityGroup);
            else if (parentHasHierarchy && !childHasRootRef && !childHasHierarchy)
                AddSoloChildToRootParent(em, parent, child, inheritanceFlags, transferLinkedEntityGroup);
            else if (parentHasRootRef && !childHasRootRef && !childHasHierarchy)
                AddSoloChildToInternalParent(em, parent, child, inheritanceFlags, transferLinkedEntityGroup);
            else if (!parentHasRootRef && !parentHasHierarchy && childHasHierarchy)
                AddRootChildToSoloParent(em, parent, child, inheritanceFlags, transferLinkedEntityGroup);
            else if (parentHasHierarchy && childHasHierarchy)
                AddRootChildToRootParent(em, parent, child, inheritanceFlags, transferLinkedEntityGroup);
            else if (parentHasRootRef && childHasHierarchy)
            {
                CheckNotAssigningRootChildToDescendant(em, parent, child);
                AddRootChildToInternalParent(em, parent, child, inheritanceFlags, transferLinkedEntityGroup);
            }
            else if (!parentHasRootRef && !parentHasHierarchy && childHasRootRef)
                AddInternalChildToSoloParent(em, parent, child, inheritanceFlags, transferLinkedEntityGroup);
            else if (parentHasHierarchy && childHasRootRef)
            {
                var childRootRef = em.GetComponentData<RootReference>(child);
                if (childRootRef.rootEntity == parent)
                    AddInternalChildToRootParentSameRoot(em, parent, child, inheritanceFlags);
                else
                    AddInternalChildToRootParentSeparateRoot(em, parent, child, inheritanceFlags, transferLinkedEntityGroup);
            }
            else if (parentHasRootRef && childHasRootRef)
            {
                var childRootRef  = em.GetComponentData<RootReference>(child);
                var parentRootRef = em.GetComponentData<RootReference>(parent);
                if (childRootRef.rootEntity == parentRootRef.rootEntity)
                {
                    CheckNotAssigningChildToDescendant(em, parent, child, parentRootRef, childRootRef);
                    AddInternalChildToInternalParentSameRoot(em, parent, child, inheritanceFlags);
                }
                else
                    AddInternalChildToInternalParentSeparateRoot(em, parent, child, inheritanceFlags, transferLinkedEntityGroup);
            }

            if (inheritanceFlags.HasCopyParent())
            {
                // Set WorldTransform of child and propagate.
                var                          rootReference = em.GetComponentData<RootReference>(child);
                Span<Propagate.WriteCommand> command       = stackalloc Propagate.WriteCommand[1];
                command[0]                                 = new Propagate.WriteCommand
                {
                    indexInHierarchy = rootReference.indexInHierarchy,
                    writeType        = Propagate.WriteCommand.WriteType.CopyParentParentChanged
                };
                Span<TransformQvvs> dummy  = stackalloc TransformQvvs[1];
                var                 handle = rootReference.ToHandle(em);
                em.CompleteDependencyBeforeRW<WorldTransform>();
                var transformLookup = em.GetComponentLookup<WorldTransform>(false);
                if (em.HasComponent<WorldTransform>(child))
                {
                    var ema = new EntityManagerAccess(em);
                    Propagate.WriteAndPropagate(handle.m_hierarchy, dummy, command, ref ema, ref ema);
                }
                if (em.HasComponent<TickedWorldTransform>(child))
                {
                    var ema = new TickedEntityManagerAccess(em);
                    Propagate.WriteAndPropagate(handle.m_hierarchy, dummy, command, ref ema, ref ema);
                }
            }
        }

        /// <summary>
        /// Removes the entity handle from its current hierarchy to be either a standalone transform entity or the root
        /// of a new hierarchy. This method is valid even if the entity is no longer alive.
        /// </summary>
        /// <param name="entityToRemove">The handle to remove from its current hierarchy</param>
        /// <param name="leaveDescendantsBehind">If true, the descendants of this handle will be left in the original hierarchy,
        /// and will be reparented to the nearest alive ancestor</param>
        /// <param name="transferLinkedEntityGroup">If true, any alive entities which are forced to become roots will have their
        /// LinkedEntityGroup populated with their descendants, and those descendants will be removed from the old hierarchy's
        /// LinkedEntityGroup.</param>
        public static unsafe void RemoveFromHierarchy(this EntityManager em,
                                                      EntityInHierarchyHandle entityToRemove,
                                                      bool leaveDescendantsBehind    = false,
                                                      bool transferLinkedEntityGroup = true)
        {
            bool isAlive = em.IsAlive(entityToRemove.entity);
            bool isRoot  = entityToRemove.isRoot;

            if (isRoot && !leaveDescendantsBehind)
                return;
            if (isRoot)
            {
                DetachAllFromRoot(em, entityToRemove, transferLinkedEntityGroup);
                return;
            }
            if (isAlive && !leaveDescendantsBehind)
                UnparentAliveSubtree(em, entityToRemove, transferLinkedEntityGroup);
            else if (isAlive && leaveDescendantsBehind)
                UnparentAliveWithoutDescendants(em, entityToRemove, transferLinkedEntityGroup);
            else if (!isAlive && !leaveDescendantsBehind)
                UnparentSubtreeWithDeadSubtreeRoot(em, entityToRemove, transferLinkedEntityGroup);
            else
                UnparentDeadWithoutDescendants(em, entityToRemove, transferLinkedEntityGroup);
        }
        #endregion

        #region Add Processes
        static unsafe void AddSoloChildToSoloParent(EntityManager em, Entity parent, Entity child, InheritanceFlags flags, bool createOrAppendLEG)
        {
            bool addTickedToChild  = em.HasComponent<TickedEntityTag>(child);
            bool addNormalToChild  = !addTickedToChild || em.HasComponent<WorldTransform>(child);
            bool addTickedToParent = addTickedToChild || em.HasComponent<TickedEntityTag>(parent);
            bool addNormalToParent = addNormalToChild || em.HasComponent<WorldTransform>(parent);
            AddRootComponents(em, parent, createOrAppendLEG, false, addNormalToParent, addTickedToParent);
            AddChildComponents(em, parent, child, addNormalToChild, addTickedToChild);

            em.SetComponentData(child, new RootReference { m_rootEntity = parent, m_indexInHierarchy = 1 });
            var hierarchy                                                                            = em.GetBuffer<EntityInHierarchy>(parent);
            hierarchy.Add(new EntityInHierarchy
            {
                m_childCount       = 1,
                m_descendantEntity = parent,
                m_firstChildIndex  = 1,
                m_flags            = InheritanceFlags.Normal,
                m_parentIndex      = -1
            });
            hierarchy.Add(new EntityInHierarchy
            {
                m_childCount       = 0,
                m_descendantEntity = child,
                m_firstChildIndex  = 2,
                m_flags            = flags,
                m_parentIndex      = 0
            });

            if (createOrAppendLEG)
                TransferFullLEG(em, parent, child);
            UpdateCleanup(em, parent);
            Validate(em, parent, child);
        }

        static unsafe void AddSoloChildToRootParent(EntityManager em, Entity parent, Entity child, InheritanceFlags flags, bool createOrAppendLEG)
        {
            bool addTickedToChild                                                    = em.HasComponent<TickedEntityTag>(child);
            bool addNormalToChild                                                    = !addTickedToChild || em.HasComponent<WorldTransform>(child);
            bool addTickedToParent                                                   = addTickedToChild || em.HasComponent<TickedEntityTag>(parent);
            bool addNormalToParent                                                   = addNormalToChild || em.HasComponent<WorldTransform>(parent);
            AssureAncestryHasComponents(em, parent, new RootReference { m_rootEntity = parent, m_indexInHierarchy = 0 }, addNormalToParent, addTickedToParent);
            AddChildComponents(em, parent, child, addNormalToChild, addTickedToChild);

            var hierarchy = em.GetBuffer<EntityInHierarchy>(parent);
            AddSingleChild(em, hierarchy, 0, child, flags);

            if (createOrAppendLEG)
                TransferFullLEG(em, parent, child);
            UpdateCleanup(em, parent);
            Validate(em, parent, child);
        }

        static unsafe void AddSoloChildToInternalParent(EntityManager em, Entity parent, Entity child, InheritanceFlags flags, bool createOrAppendLEG)
        {
            bool addTickedToChild  = em.HasComponent<TickedEntityTag>(child);
            bool addNormalToChild  = !addTickedToChild || em.HasComponent<WorldTransform>(child);
            bool addTickedToParent = addTickedToChild || em.HasComponent<TickedEntityTag>(parent);
            bool addNormalToParent = addNormalToChild || em.HasComponent<WorldTransform>(parent);
            AddChildComponents(em, parent, child, addNormalToChild, addTickedToChild);

            var rootRef = em.GetComponentData<RootReference>(parent);
            AssureAncestryHasComponents(em, parent, rootRef, addNormalToParent, addTickedToParent);

            var hierarchy = GetHierarchy(em, rootRef.rootEntity, out var rootIsAlive);
            AddSingleChild(em, hierarchy, rootRef.indexInHierarchy, child, flags);

            if (createOrAppendLEG)
                TransferFullLEG(em, rootRef.rootEntity, child);
            UpdateCleanup(em, rootRef.rootEntity);
            Validate(em, parent, child);
        }

        static unsafe void AddRootChildToSoloParent(EntityManager em, Entity parent, Entity child, InheritanceFlags flags, bool createOrAppendLEG)
        {
            bool addTickedToChild  = em.HasComponent<TickedEntityTag>(child);
            bool addNormalToChild  = !addTickedToChild || em.HasComponent<WorldTransform>(child);
            bool addTickedToParent = addTickedToChild || em.HasComponent<TickedEntityTag>(parent);
            bool addNormalToParent = addNormalToChild || em.HasComponent<WorldTransform>(parent);
            bool childHasCleanup   = em.HasBuffer<EntityInHierarchyCleanup>(child);
            AddRootComponents(em, parent, createOrAppendLEG, childHasCleanup, addNormalToParent, addTickedToParent);
            AddChildComponents(em, parent, child, addNormalToChild, addTickedToChild);

            var dstHierarchy = em.GetBuffer<EntityInHierarchy>(parent);
            var srcHierarchy = em.GetBuffer<EntityInHierarchy>(child);
            dstHierarchy.ResizeUninitialized(srcHierarchy.Length + 1);
            var dst = dstHierarchy.AsNativeArray();
            var src = srcHierarchy.AsNativeArray();
            dst[0]  = new EntityInHierarchy
            {
                m_childCount       = 1,
                m_descendantEntity = parent,
                m_firstChildIndex  = 1,
                m_flags            = InheritanceFlags.Normal,
                m_parentIndex      = -1,
            };
            for (int i = 0; i < src.Length; i++)
            {
                var temp = src[i];
                temp.m_parentIndex++;
                temp.m_firstChildIndex++;
                if (em.IsAlive(temp.entity))
                    em.SetComponentData(temp.entity, new RootReference { m_indexInHierarchy = i + 1, m_rootEntity = parent });
                dst[i + 1]                                                                                        = temp;
            }

            if (createOrAppendLEG)
                TransferFullLEG(em, parent, child);
            UpdateCleanup(em, parent);
            em.RemoveComponent(child, new TypePack<EntityInHierarchy, EntityInHierarchyCleanup>());
            Validate(em, parent, child);
        }

        static unsafe void AddRootChildToRootParent(EntityManager em, Entity parent, Entity child, InheritanceFlags flags, bool createOrAppendLEG)
        {
            bool addTickedToChild                                                    = em.HasComponent<TickedEntityTag>(child);
            bool addNormalToChild                                                    = !addTickedToChild || em.HasComponent<WorldTransform>(child);
            bool addTickedToParent                                                   = addTickedToChild || em.HasComponent<TickedEntityTag>(parent);
            bool addNormalToParent                                                   = addNormalToChild || em.HasComponent<WorldTransform>(parent);
            AssureAncestryHasComponents(em, parent, new RootReference { m_rootEntity = parent, m_indexInHierarchy = 0 }, addNormalToParent, addTickedToParent);
            AddChildComponents(em, parent, child, addNormalToChild, addTickedToChild);

            var dstHierarchy = em.GetBuffer<EntityInHierarchy>(parent);
            var srcHierarchy = em.GetBuffer<EntityInHierarchy>(child).AsNativeArray().AsReadOnlySpan();

            InsertSubtree(em, dstHierarchy, 0, srcHierarchy, flags);

            if (createOrAppendLEG)
                TransferFullLEG(em, parent, child);
            UpdateCleanup(em, parent);
            em.RemoveComponent(child, new TypePack<EntityInHierarchy, EntityInHierarchyCleanup>());
            Validate(em, parent, child);
        }

        static unsafe void AddRootChildToInternalParent(EntityManager em, Entity parent, Entity child, InheritanceFlags flags, bool createOrAppendLEG)
        {
            bool addTickedToChild  = em.HasComponent<TickedEntityTag>(child);
            bool addNormalToChild  = !addTickedToChild || em.HasComponent<WorldTransform>(child);
            bool addTickedToParent = addTickedToChild || em.HasComponent<TickedEntityTag>(parent);
            bool addNormalToParent = addNormalToChild || em.HasComponent<WorldTransform>(parent);
            var  rootRef           = em.GetComponentData<RootReference>(parent);
            AssureAncestryHasComponents(em, parent, rootRef, addNormalToParent, addTickedToParent);
            AddChildComponents(em, parent, child, addNormalToChild, addTickedToChild);

            var dstHierarchy = GetHierarchy(em, rootRef.rootEntity, out var rootIsAlive);
            var srcHierarchy = em.GetBuffer<EntityInHierarchy>(child).AsNativeArray().AsReadOnlySpan();

            InsertSubtree(em, dstHierarchy, rootRef.indexInHierarchy, srcHierarchy, flags);

            if (createOrAppendLEG)
                TransferFullLEG(em, rootRef.rootEntity, child);
            UpdateCleanup(em, parent);
            em.RemoveComponent(child, new TypePack<EntityInHierarchy, EntityInHierarchyCleanup>());
            Validate(em, parent, child);
        }

        static unsafe void AddInternalChildToSoloParent(EntityManager em, Entity parent, Entity child, InheritanceFlags flags, bool createOrAppendLEG)
        {
            bool addTickedToChild  = em.HasComponent<TickedEntityTag>(child);
            bool addNormalToChild  = !addTickedToChild || em.HasComponent<WorldTransform>(child);
            bool addTickedToParent = addTickedToChild || em.HasComponent<TickedEntityTag>(parent);
            bool addNormalToParent = addNormalToChild || em.HasComponent<WorldTransform>(parent);
            AddRootComponents(em, parent, createOrAppendLEG, false, addNormalToParent, addTickedToParent);

            var childRootRef    = em.GetComponentData<RootReference>(child);
            var childHierarchy  = GetHierarchy(em, childRootRef.rootEntity, out var childRootIsAlive);
            var parentHierarchy = em.GetBuffer<EntityInHierarchy>(parent);

            if (childHierarchy[childRootRef.indexInHierarchy].childCount == 0)
            {
                parentHierarchy.ResizeUninitialized(2);
                parentHierarchy[0] = new EntityInHierarchy
                {
                    m_childCount       = 1,
                    m_descendantEntity = parent,
                    m_firstChildIndex  = 1,
                    m_flags            = InheritanceFlags.Normal,
                    m_parentIndex      = -1,
                };
                parentHierarchy[1] = new EntityInHierarchy
                {
                    m_childCount       = 0,
                    m_descendantEntity = child,
                    m_firstChildIndex  = 2,
                    m_flags            = flags,
                    m_parentIndex      = 0
                };
                em.SetComponentData(child, new RootReference { m_indexInHierarchy = 1, m_rootEntity = parent });

                RemoveSingleDescendant(em, childHierarchy, childRootRef.indexInHierarchy);

                if (createOrAppendLEG)
                    TransferSubtreeLEG(em, parent, childRootRef.rootEntity, stackalloc EntityInHierarchy[] { new EntityInHierarchy { m_descendantEntity = child } });
                UpdateCleanup(em, parent);
                Validate(em, parent, child);
                ValidateRemoval(em, childRootRef.rootEntity);
            }
            else
            {
                var tsa     = ThreadStackAllocator.GetAllocator();
                var subtree = ExtractSubtree(ref tsa, childHierarchy.AsNativeArray().AsReadOnlySpan(), childRootRef.indexInHierarchy);
                parentHierarchy.ResizeUninitialized(subtree.Length + 1);
                var dst = parentHierarchy.AsNativeArray();
                dst[0]  = new EntityInHierarchy
                {
                    m_childCount       = 1,
                    m_descendantEntity = parent,
                    m_firstChildIndex  = 1,
                    m_flags            = InheritanceFlags.Normal,
                    m_parentIndex      = -1,
                };
                for (int i = 0; i < subtree.Length; i++)
                {
                    var temp = subtree[i];
                    temp.m_parentIndex++;
                    temp.m_firstChildIndex++;
                    if (em.IsAlive(temp.entity))
                        em.SetComponentData(temp.entity, new RootReference { m_indexInHierarchy = i + 1, m_rootEntity = parent });
                    dst[i + 1]                                                                                        = temp;
                }

                RemoveSubtree(ref tsa, em, childHierarchy, childRootRef.indexInHierarchy, subtree);

                if (createOrAppendLEG)
                    TransferSubtreeLEG(em, parent, childRootRef.rootEntity, subtree);
                UpdateCleanup(em, parent);

                tsa.Dispose();
                Validate(em, parent, child);
                ValidateRemoval(em, childRootRef.rootEntity);
            }
        }

        static unsafe void AddInternalChildToRootParentSameRoot(EntityManager em, Entity parent, Entity child, InheritanceFlags flags)
        {
            bool addTickedToChild                                                    = em.HasComponent<TickedEntityTag>(child);
            bool addNormalToChild                                                    = !addTickedToChild || em.HasComponent<WorldTransform>(child);
            bool addTickedToParent                                                   = addTickedToChild || em.HasComponent<TickedEntityTag>(parent);
            bool addNormalToParent                                                   = addNormalToChild || em.HasComponent<WorldTransform>(parent);
            AssureAncestryHasComponents(em, parent, new RootReference { m_rootEntity = parent, m_indexInHierarchy = 0}, addNormalToParent, addTickedToParent);

            var childRootRef = em.GetComponentData<RootReference>(child);
            var hierarchy    = GetHierarchy(em, parent, out var rootIsAlive);
            if (hierarchy[childRootRef.indexInHierarchy].childCount == 0)
            {
                RemoveSingleDescendant(em, hierarchy, childRootRef.indexInHierarchy, true);
                AddSingleChild(em, hierarchy, 0, child, flags);
                ValidateRemoval(em, childRootRef.rootEntity);
            }
            else
            {
                var tsa     = ThreadStackAllocator.GetAllocator();
                var subtree = ExtractSubtree(ref tsa, hierarchy.AsNativeArray().AsReadOnlySpan(), childRootRef.indexInHierarchy);
                RemoveSubtree(ref tsa, em, hierarchy, childRootRef.indexInHierarchy, subtree, true);
                InsertSubtree(em, hierarchy, 0, subtree, flags);
                tsa.Dispose();
                ValidateRemoval(em, childRootRef.rootEntity);
            }
            Validate(em, parent, child);
        }

        static unsafe void AddInternalChildToRootParentSeparateRoot(EntityManager em, Entity parent, Entity child, InheritanceFlags flags, bool createOrAppendLEG)
        {
            bool addTickedToChild                                                    = em.HasComponent<TickedEntityTag>(child);
            bool addNormalToChild                                                    = !addTickedToChild || em.HasComponent<WorldTransform>(child);
            bool addTickedToParent                                                   = addTickedToChild || em.HasComponent<TickedEntityTag>(parent);
            bool addNormalToParent                                                   = addNormalToChild || em.HasComponent<WorldTransform>(parent);
            AssureAncestryHasComponents(em, parent, new RootReference { m_rootEntity = parent, m_indexInHierarchy = 0 }, addNormalToParent, addTickedToParent);

            var childRootRef    = em.GetComponentData<RootReference>(child);
            var childHierarchy  = GetHierarchy(em, childRootRef.rootEntity, out var childRootIsAlive);
            var parentHierarchy = em.GetBuffer<EntityInHierarchy>(parent);
            if (childHierarchy[childRootRef.indexInHierarchy].childCount == 0)
            {
                AddSingleChild(em, parentHierarchy, 0, child, flags);

                RemoveSingleDescendant(em, childHierarchy, childRootRef.indexInHierarchy);

                if (createOrAppendLEG)
                    TransferSubtreeLEG(em, parent, childRootRef.rootEntity, stackalloc EntityInHierarchy[] { new EntityInHierarchy { m_descendantEntity = child } });
                UpdateCleanup(em, parent);
                Validate(em, parent, child);
                ValidateRemoval(em, childRootRef.rootEntity);
            }
            else
            {
                var tsa     = ThreadStackAllocator.GetAllocator();
                var subtree = ExtractSubtree(ref tsa, childHierarchy.AsNativeArray().AsReadOnlySpan(), childRootRef.indexInHierarchy);

                InsertSubtree(em, parentHierarchy, 0, subtree, flags);
                RemoveSubtree(ref tsa, em, childHierarchy, childRootRef.indexInHierarchy, subtree);

                if (createOrAppendLEG)
                    TransferSubtreeLEG(em, parent, childRootRef.rootEntity, subtree);
                UpdateCleanup(em, parent);

                tsa.Dispose();
                Validate(em, parent, child);
                ValidateRemoval(em, childRootRef.rootEntity);
            }
        }

        static unsafe void AddInternalChildToInternalParentSameRoot(EntityManager em, Entity parent, Entity child, InheritanceFlags flags)
        {
            bool addTickedToChild  = em.HasComponent<TickedEntityTag>(child);
            bool addNormalToChild  = !addTickedToChild || em.HasComponent<WorldTransform>(child);
            bool addTickedToParent = addTickedToChild || em.HasComponent<TickedEntityTag>(parent);
            bool addNormalToParent = addNormalToChild || em.HasComponent<WorldTransform>(parent);
            var  parentRootRef     = em.GetComponentData<RootReference>(parent);
            AssureAncestryHasComponents(em, parent, parentRootRef, addNormalToParent, addTickedToParent);

            var childRootRef = em.GetComponentData<RootReference>(child);
            var hierarchy    = GetHierarchy(em, childRootRef.rootEntity, out var rootIsAlive);
            if (hierarchy[childRootRef.indexInHierarchy].childCount == 0)
            {
                RemoveSingleDescendant(em, hierarchy, childRootRef.indexInHierarchy, true);
                parentRootRef = em.GetComponentData<RootReference>(parent);
                AddSingleChild(em, hierarchy, parentRootRef.indexInHierarchy, child, flags);
                Validate(em, parent, child);
                ValidateRemoval(em, childRootRef.rootEntity);
            }
            else
            {
                var tsa     = ThreadStackAllocator.GetAllocator();
                var subtree = ExtractSubtree(ref tsa, hierarchy.AsNativeArray().AsReadOnlySpan(), childRootRef.indexInHierarchy);
                RemoveSubtree(ref tsa, em, hierarchy, childRootRef.indexInHierarchy, subtree, true);

                parentRootRef = em.GetComponentData<RootReference>(parent);
                InsertSubtree(em, hierarchy, parentRootRef.indexInHierarchy, subtree, flags);
                tsa.Dispose();
                Validate(em, parent, child);
                ValidateRemoval(em, childRootRef.rootEntity);
            }
        }

        static unsafe void AddInternalChildToInternalParentSeparateRoot(EntityManager em, Entity parent, Entity child, InheritanceFlags flags, bool createOrAppendLEG)
        {
            var childRootRef  = em.GetComponentData<RootReference>(child);
            var parentRootRef = em.GetComponentData<RootReference>(parent);

            bool addTickedToChild  = em.HasComponent<TickedEntityTag>(child);
            bool addNormalToChild  = !addTickedToChild || em.HasComponent<WorldTransform>(child);
            bool addTickedToParent = addTickedToChild || em.HasComponent<TickedEntityTag>(parent);
            bool addNormalToParent = addNormalToChild || em.HasComponent<WorldTransform>(parent);
            AssureAncestryHasComponents(em, parent, parentRootRef, addNormalToParent, addTickedToParent);

            var childHierarchy  = GetHierarchy(em, childRootRef.rootEntity, out var childRootIsAlive);
            var parentHierarchy = GetHierarchy(em, parentRootRef.rootEntity, out var parentRootIsAlive);
            if (childHierarchy[childRootRef.indexInHierarchy].childCount == 0)
            {
                AddSingleChild(em, parentHierarchy, parentRootRef.indexInHierarchy, child, flags);
                RemoveSingleDescendant(em, childHierarchy, childRootRef.indexInHierarchy);

                if (createOrAppendLEG)
                    TransferSubtreeLEG(em, parentRootRef.rootEntity, childRootRef.rootEntity, stackalloc EntityInHierarchy[] { new EntityInHierarchy {
                                                                                                                                   m_descendantEntity = child
                                                                                                                               } });
                UpdateCleanup(em, parent);
                Validate(em, parent, child);
                ValidateRemoval(em, childRootRef.rootEntity);
            }
            else
            {
                var tsa     = ThreadStackAllocator.GetAllocator();
                var subtree = ExtractSubtree(ref tsa, childHierarchy.AsNativeArray().AsReadOnlySpan(), childRootRef.indexInHierarchy);

                InsertSubtree(em, parentHierarchy, parentRootRef.indexInHierarchy, subtree, flags);
                RemoveSubtree(ref tsa, em, childHierarchy, childRootRef.indexInHierarchy, subtree);

                if (createOrAppendLEG)
                    TransferSubtreeLEG(em, parentRootRef.rootEntity, childRootRef.rootEntity, subtree);
                UpdateCleanup(em, parent);

                tsa.Dispose();
                Validate(em, parent, child);
                ValidateRemoval(em, childRootRef.rootEntity);
            }
        }
        #endregion

        #region Remove Processes
        static unsafe void UnparentAliveSubtree(EntityManager em, EntityInHierarchyHandle child, bool transferLEG)
        {
            var oldRoot             = child.root.entity;
            var oldIndexInHierarchy = child.indexInHierarchy;
            var oldHierarchy        = GetHierarchy(em, oldRoot, out var oldRootIsAlive);
            if (child.bloodChildren.length == 0)
            {
                var childEntity = child.entity;
                RemoveSingleDescendant(em, oldHierarchy, oldIndexInHierarchy);
                if (oldRootIsAlive && transferLEG && em.HasBuffer<LinkedEntityGroup>(oldRoot))
                {
                    var leg           = em.GetBuffer<LinkedEntityGroup>(oldRoot);
                    var indexToRemove = leg.AsNativeArray().Reinterpret<Entity>().IndexOf(childEntity);
                    if (indexToRemove >= 0)
                        leg.RemoveAtSwapBack(indexToRemove);
                    if (leg.Length <= 1)
                        em.RemoveComponent<LinkedEntityGroup>(oldRoot);
                }
                em.RemoveComponent<RootReference>(childEntity);
            }
            else
            {
                var tsa     = ThreadStackAllocator.GetAllocator();
                var subtree = ExtractSubtree(ref tsa, oldHierarchy.AsNativeArray().AsReadOnlySpan(), oldIndexInHierarchy);
                RemoveSubtree(ref tsa, em, oldHierarchy, oldIndexInHierarchy, subtree);

                em.RemoveComponent<RootReference>(subtree[0].entity);
                bool oldHierarchyHadLEG = em.HasBuffer<LinkedEntityGroup>(oldRoot);
                AddRootComponents(em, subtree[0].entity, transferLEG && oldHierarchyHadLEG, !oldHierarchyHadLEG, false, false);
                var newHierarchy = em.GetBuffer<EntityInHierarchy>(subtree[0].entity);
                //newHierarchy.AddRange()
                newHierarchy.EnsureCapacity(subtree.Length);
                for (int i = 0; i < subtree.Length; i++)
                {
                    if (i > 0 && em.IsAlive(subtree[i].entity))
                        em.SetComponentData(subtree[i].entity, new RootReference { m_rootEntity = subtree[0].entity, m_indexInHierarchy = i });
                    newHierarchy.Add(subtree[i]);
                }
                if (oldHierarchyHadLEG && transferLEG)
                    TransferSubtreeLEG(em, subtree[0].entity, oldRoot, subtree);
                UpdateCleanup(em, subtree[0].entity);

                tsa.Dispose();
            }
            ValidateRemoval(em, oldRoot);
        }

        static unsafe void UnparentAliveWithoutDescendants(EntityManager em, EntityInHierarchyHandle child, bool transferLEG)
        {
            var rootEntity = child.root.entity;

            var          childEntity = child.entity;
            var          children    = child.bloodChildren;
            Span<Entity> descendants = stackalloc Entity[children.length];
            for (int i = 0; i < descendants.Length; i++)
                descendants[i] = children[i].entity;
            var newParent      = child.FindParent(em);
            if (newParent.isNull)
            {
                for (int i = 0; i < descendants.Length; i++)
                {
                    var rr = em.GetComponentData<RootReference>(descendants[i]);
                    em.RemoveFromHierarchy(rr.ToHandle(em), false, transferLEG);
                }
            }
            else
            {
                var parentEntity = newParent.entity;
                for (int i = 0; i < descendants.Length; i++)
                {
                    var h     = em.GetComponentData<RootReference>(descendants[i]).ToHandle(em);
                    var flags = h.inheritanceFlags;
                    em.AddChild(parentEntity, descendants[i], flags, transferLEG);
                }
            }
            child = em.GetComponentData<RootReference>(childEntity).ToHandle(em);
            UnparentAliveSubtree(em, child, transferLEG);
            ValidateRemoval(em, rootEntity);
        }

        static unsafe void UnparentSubtreeWithDeadSubtreeRoot(EntityManager em, EntityInHierarchyHandle deadChild, bool transferLEG)
        {
            var oldRoot             = deadChild.root.entity;
            var oldIndexInHierarchy = deadChild.indexInHierarchy;
            var oldHierarchy        = GetHierarchy(em, oldRoot, out var oldRootIsAlive);
            if (deadChild.bloodChildren.length == 0)
            {
                RemoveSingleDescendant(em, oldHierarchy, oldIndexInHierarchy);
            }
            else
            {
                var          tsa             = ThreadStackAllocator.GetAllocator();
                var          descendantCount = deadChild.CountBloodDescendants();
                Span<Entity> descendants     = tsa.AllocateAsSpan<Entity>(descendantCount);
                Span<int>    deadIndices     = tsa.AllocateAsSpan<int>(descendantCount);
                descendantCount              = 0;
                int deadIndicesCurrent       = 0;
                int deadIndicesCount         = 1;
                deadIndices[0]               = deadChild.indexInHierarchy;

                while (deadIndicesCurrent < deadIndicesCount)
                {
                    var children = deadChild.GetFromIndexInHierarchy(deadIndices[deadIndicesCurrent]).bloodChildren;
                    for (int i = 0; i < children.length; i++)
                    {
                        var child = children[i];
                        if (em.IsAlive(child.entity))
                        {
                            descendants[descendantCount] = child.entity;
                            descendantCount++;
                        }
                        else
                        {
                            deadIndices[deadIndicesCount] = child.indexInHierarchy;
                            deadIndicesCount++;
                        }
                    }
                    deadIndicesCurrent++;
                }
                descendants = descendants.Slice(0, descendantCount);

                for (int i = 0; i <= descendants.Length; i++)
                {
                    var rr = em.GetComponentData<RootReference>(descendants[i]);
                    UnparentAliveSubtree(em, rr.ToHandle(em), transferLEG);
                }

                oldHierarchy = GetHierarchy(em, oldRoot, out _);
                var subtree  = ExtractSubtree(ref tsa, oldHierarchy.AsNativeArray().AsReadOnlySpan(), oldIndexInHierarchy);
                RemoveSubtree(ref tsa, em, oldHierarchy, oldIndexInHierarchy, subtree);

                tsa.Dispose();
            }
            ValidateRemoval(em, oldRoot);
        }

        static unsafe void UnparentDeadWithoutDescendants(EntityManager em, EntityInHierarchyHandle deadChild, bool transferLEG)
        {
            var          childEntity = deadChild.entity;
            var          children    = deadChild.bloodChildren;
            Span<Entity> descendants = stackalloc Entity[children.length];
            for (int i = 0; i < descendants.Length; i++)
                descendants[i] = children[i].entity;
            var newParent      = deadChild.FindParent(em);
            var rootEntity     = deadChild.root.entity;
            if (newParent.isNull)
            {
                for (int i = 0; i < descendants.Length; i++)
                {
                    var rr = em.GetComponentData<RootReference>(descendants[i]);
                    em.RemoveFromHierarchy(rr.ToHandle(em), false, transferLEG);
                }
            }
            else
            {
                var parentEntity = newParent.entity;
                for (int i = 0; i < descendants.Length; i++)
                {
                    var h     = em.GetComponentData<RootReference>(descendants[i]).ToHandle(em);
                    var flags = h.inheritanceFlags;
                    em.AddChild(parentEntity, descendants[i], flags, transferLEG);
                }
            }
            var hierarchy  = GetHierarchy(em, rootEntity, out _);
            var rootHandle = hierarchy.GetRootHandle();
            for (int i = 0; i < rootHandle.m_hierarchy.Length; i++)
            {
                if (rootHandle.m_hierarchy[i].entity == childEntity)
                {
                    RemoveSingleDescendant(em, hierarchy, i);
                    ValidateRemoval(em, rootEntity);
                    return;
                }
            }
            ValidateRemoval(em, rootEntity);
        }

        static unsafe void DetachAllFromRoot(EntityManager em, EntityInHierarchyHandle root, bool transferLEG)
        {
            var          rootEntity = root.entity;
            Span<Entity> newRoots   = stackalloc Entity[root.bloodChildren.length];
            for (int i = 0; i < newRoots.Length; i++)
                newRoots[i] = root.bloodChildren[i].entity;
            for (int i = 0; i < newRoots.Length; i++)
            {
                var rr = em.GetComponentData<RootReference>(newRoots[i]);
                RemoveFromHierarchy(em, rr.ToHandle(em), false, transferLEG);
            }
            ValidateRemoval(em, rootEntity);
        }
        #endregion

        #region Algorithms
        static void AddChildComponents(EntityManager em, Entity parent, Entity child, bool requireNormal, bool requireTicked)
        {
            FixedList128Bytes<ComponentType> types = new FixedList128Bytes<ComponentType>();
            types.Add(ComponentType.ReadWrite<RootReference>());
            if (requireNormal)
                types.Add(ComponentType.ReadWrite<WorldTransform>());
            if (requireTicked)
            {
                types.Add(ComponentType.ReadWrite<TickedWorldTransform>());
                types.Add(ComponentType.ReadWrite<TickedEntityTag>());
            }
            bool wasMissingNormal = requireNormal && !em.HasComponent<WorldTransform>(child);
            bool wasMissingTicked = requireTicked && !em.HasComponent<TickedWorldTransform>(child);
            em.AddComponent(child, new ComponentTypeSet(in types));

            // Special copy cases
            if (wasMissingNormal && !wasMissingTicked && requireTicked)
            {
                em.SetComponentData(child, em.GetComponentData<TickedWorldTransform>(child).ToUnticked());
                return;
            }
            if (wasMissingTicked && !wasMissingNormal && requireNormal)
            {
                em.SetComponentData(child, em.GetComponentData<WorldTransform>(child).ToTicked());
            }

            if (wasMissingNormal)
                em.SetComponentData(child, new WorldTransform { worldTransform = TransformQvvs.identity });
            if (wasMissingTicked)
                em.SetComponentData(child, new TickedWorldTransform { worldTransform = TransformQvvs.identity });
        }

        static void AddRootComponents(EntityManager em, Entity root, bool requireLEG, bool requireCleanup, bool requireNormal, bool requireTicked)
        {
            FixedList128Bytes<ComponentType> types = new FixedList128Bytes<ComponentType>();
            types.Add(ComponentType.ReadWrite<EntityInHierarchy>());
            if (requireLEG)
                types.Add(ComponentType.ReadWrite<LinkedEntityGroup>());
            if (requireCleanup)
                types.Add(ComponentType.ReadWrite<EntityInHierarchyCleanup>());
            if (requireNormal)
                types.Add(ComponentType.ReadWrite<WorldTransform>());
            if (requireTicked)
            {
                types.Add(ComponentType.ReadWrite<TickedWorldTransform>());
                types.Add(ComponentType.ReadWrite<TickedEntityTag>());
            }
            bool wasMissingNormal = requireNormal && !em.HasComponent<WorldTransform>(root);
            bool wasMissingTicked = requireTicked && !em.HasComponent<TickedWorldTransform>(root);
            bool wasMissingLeg    = requireLEG && !em.HasBuffer<LinkedEntityGroup>(root);
            em.AddComponent(root, new ComponentTypeSet(in types));

            if (wasMissingLeg)
                em.GetBuffer<LinkedEntityGroup>(root).Add(new LinkedEntityGroup { Value = root });

            // Special copy cases
            if (wasMissingNormal && !wasMissingTicked && requireTicked)
            {
                em.SetComponentData(root, em.GetComponentData<TickedWorldTransform>(root).ToUnticked());
                return;
            }
            if (wasMissingTicked && !wasMissingNormal && requireNormal)
            {
                em.SetComponentData(root, em.GetComponentData<WorldTransform>(root).ToTicked());
            }

            if (wasMissingNormal)
                em.SetComponentData(root, new WorldTransform { worldTransform = TransformQvvs.identity });
            if (wasMissingTicked)
                em.SetComponentData(root, new TickedWorldTransform { worldTransform = TransformQvvs.identity });
        }

        static void AssureAncestryHasComponents(EntityManager em, Entity parent, RootReference parentRootRef, bool requireNormal, bool requireTicked)
        {
            FixedList128Bytes<ComponentType> types = new FixedList128Bytes<ComponentType>();
            if (requireNormal)
                types.Add(ComponentType.ReadWrite<WorldTransform>());
            if (requireTicked)
            {
                types.Add(ComponentType.ReadWrite<TickedWorldTransform>());
                types.Add(ComponentType.ReadWrite<TickedEntityTag>());
            }

            var activeEntity  = parent;
            var activeRootRef = parentRootRef;

            while (true)
            {
                bool wasMissingNormal = requireNormal && !em.HasComponent<WorldTransform>(activeEntity);
                bool wasMissingTicked = requireTicked && !em.HasComponent<TickedWorldTransform>(activeEntity);
                if (!wasMissingNormal && !wasMissingTicked)
                    return;
                em.AddComponent(activeEntity, new ComponentTypeSet(in types));

                // Special copy cases
                if (wasMissingNormal && !wasMissingTicked && requireTicked)
                {
                    em.SetComponentData(activeEntity, em.GetComponentData<TickedWorldTransform>(activeEntity).ToUnticked());
                }
                else if (wasMissingTicked && !wasMissingNormal && requireNormal)
                {
                    em.SetComponentData(activeEntity, em.GetComponentData<WorldTransform>(activeEntity).ToTicked());
                }
                var handle       = activeRootRef.ToHandle(em);
                var parentHandle = handle.FindParent(em);
                if (parentHandle.isNull)
                    break;
                activeRootRef.m_indexInHierarchy = parentHandle.indexInHierarchy;
                activeEntity                     = parentHandle.entity;
            }
        }

        // Returns true if the whole subtree exists in the destination LEG
        static void TransferFullLEG(EntityManager em, Entity dstRoot, Entity srcRoot)
        {
            DynamicBuffer<Entity> dstLEG = default;
            if (em.HasBuffer<LinkedEntityGroup>(dstRoot))
                dstLEG = em.GetBuffer<LinkedEntityGroup>(dstRoot).Reinterpret<Entity>();
            else
            {
                dstLEG = em.AddBuffer<LinkedEntityGroup>(dstRoot).Reinterpret<Entity>();
                dstLEG.Add(dstRoot);
            }

            if (em.HasBuffer<LinkedEntityGroup>(srcRoot))
            {
                var srcLEG = em.GetBuffer<LinkedEntityGroup>(srcRoot).Reinterpret<Entity>();
                for (int i = 0; i < srcLEG.Length; i++)
                    dstLEG.Add(srcLEG[i]);
                em.RemoveComponent<LinkedEntityGroup>(srcRoot);
            }
            else
                dstLEG.Add(srcRoot);
        }

        static void TransferSubtreeLEG(EntityManager em, Entity dstRoot, Entity srcRoot, ReadOnlySpan<EntityInHierarchy> subtree)
        {
            if (!em.HasBuffer<LinkedEntityGroup>(srcRoot))
                return;
            var firstIndexToTransfer = -1;
            var srcLEGArray          = em.GetBuffer<LinkedEntityGroup>(srcRoot).Reinterpret<Entity>().AsNativeArray();
            for (int i = 0; i < subtree.Length; i++)
            {
                if (srcLEGArray.Contains(subtree[i].entity))
                {
                    firstIndexToTransfer = i;
                    break;
                }
            }
            if (firstIndexToTransfer == -1)
                return;

            DynamicBuffer<Entity> dstLEG = default;
            if (em.HasBuffer<LinkedEntityGroup>(dstRoot))
                dstLEG = em.GetBuffer<LinkedEntityGroup>(dstRoot).Reinterpret<Entity>();
            else
            {
                dstLEG = em.AddBuffer<LinkedEntityGroup>(dstRoot).Reinterpret<Entity>();
                dstLEG.Add(dstRoot);
            }
            var srcLEG = em.GetBuffer<LinkedEntityGroup>(srcRoot).Reinterpret<Entity>();
            for (int i = firstIndexToTransfer; i < subtree.Length; i++)
            {
                var index = srcLEG.AsNativeArray().IndexOf(subtree[i].entity);
                if (index >= 0)
                {
                    if (subtree[i].entity != dstRoot)
                        dstLEG.Add(subtree[i].entity);
                    if (index > 0)
                        srcLEG.RemoveAtSwapBack(index);
                }
            }
            if (srcLEG.Length <= 1)
                em.RemoveComponent<LinkedEntityGroup>(srcRoot);
        }

        static void UpdateCleanup(EntityManager em, Entity root, bool removed = false)
        {
            if (!em.HasBuffer<EntityInHierarchy>(root))
            {
                return;
            }
            bool needsCleanup = em.HasBuffer<EntityInHierarchyCleanup>(root) || !em.HasBuffer<LinkedEntityGroup>(root);
            if (!needsCleanup)
            {
                var currentLEG = em.GetBuffer<LinkedEntityGroup>(root, true).Reinterpret<Entity>().AsNativeArray();
                var hierarchy  = em.GetBuffer<EntityInHierarchy>(root, true).AsNativeArray();
                foreach (var e in hierarchy)
                {
                    if (!currentLEG.Contains(e.entity))
                    {
                        needsCleanup = true;
                        break;
                    }
                }
            }
            if (!needsCleanup)
                return;
            DynamicBuffer<EntityInHierarchyCleanup> cleanupBuffer;
            if (em.HasBuffer<EntityInHierarchyCleanup>(root))
                cleanupBuffer = em.GetBuffer<EntityInHierarchyCleanup>(root);
            else
                cleanupBuffer = em.AddBuffer<EntityInHierarchyCleanup>(root);
            var cleanup       = cleanupBuffer.Reinterpret<EntityInHierarchy>();
            cleanup.Clear();
            cleanup.AddRange(em.GetBuffer<EntityInHierarchy>(root).AsNativeArray());
        }

        static DynamicBuffer<EntityInHierarchy> GetHierarchy(EntityManager em, Entity root, out bool rootIsAlive)
        {
            if (em.HasBuffer<EntityInHierarchy>(root))
            {
                rootIsAlive = true;
                return em.GetBuffer<EntityInHierarchy>(root);
            }
            rootIsAlive = false;
            return em.GetBuffer<EntityInHierarchyCleanup>(root).Reinterpret<EntityInHierarchy>();
        }

        static unsafe ReadOnlySpan<EntityInHierarchy> ExtractSubtree(ref ThreadStackAllocator tsa, ReadOnlySpan<EntityInHierarchy> srcHierarchy, int subtreeRootIndex)
        {
            var maxDescendantsCount = srcHierarchy.Length - subtreeRootIndex;
            var extractionList      = new UnsafeList<EntityInHierarchy>(tsa.Allocate<EntityInHierarchy>(maxDescendantsCount), maxDescendantsCount);

            extractionList.Clear();  // The list initializer we are using sets both capacity and length.

            //descendantsToMove.Add((child, -1));
            extractionList.Add(new EntityInHierarchy
            {
                m_descendantEntity = srcHierarchy[subtreeRootIndex].entity,
                m_parentIndex      = -1,
                m_childCount       = srcHierarchy[subtreeRootIndex].childCount,
                m_firstChildIndex  = 1,
                m_flags            = default
            });
            // The root is the first level. For each subsequent level, we iterate the entities added during the previous level.
            // And then we add their children.
            int firstParentInLevel               = 0;
            int parentCountInLevel               = 1;
            int firstParentHierarchyIndexInLevel = subtreeRootIndex;
            while (parentCountInLevel > 0)
            {
                var firstParentInNextLevel               = extractionList.Length;
                var parentCountInNextLevel               = 0;
                int firstParentHierarchyIndexInNextLevel = 0;
                for (int parentIndex = 0; parentIndex < parentCountInLevel; parentIndex++)
                {
                    var dstParentIndex    = parentIndex + firstParentInLevel;
                    var parentInHierarchy = srcHierarchy[firstParentHierarchyIndexInLevel + parentIndex];
                    if (parentIndex == 0)
                        firstParentHierarchyIndexInNextLevel  = parentInHierarchy.firstChildIndex;
                    parentCountInNextLevel                   += parentInHierarchy.childCount;
                    for (int i = 0; i < parentInHierarchy.childCount; i++)
                    {
                        var oldElement               = srcHierarchy[parentInHierarchy.firstChildIndex + i];
                        oldElement.m_firstChildIndex = int.MaxValue;
                        oldElement.m_parentIndex     = dstParentIndex;
                        extractionList.Add(oldElement);
                    }
                }
                firstParentInLevel               = firstParentInNextLevel;
                parentCountInLevel               = parentCountInNextLevel;
                firstParentHierarchyIndexInLevel = firstParentHierarchyIndexInNextLevel;
            }

            var result = new Span<EntityInHierarchy>(extractionList.Ptr, extractionList.Length);

            for (int i = 1; i < result.Length; i++)
            {
                ref var child           = ref result[i];
                ref var firstChildIndex = ref result[child.parentIndex].m_firstChildIndex;
                firstChildIndex         = math.min(firstChildIndex, i);
                var previous            = result[i - 1];
                child.m_firstChildIndex = previous.firstChildIndex + previous.childCount;
            }
            return result;
        }

        static unsafe void RemoveSingleDescendant(EntityManager em, DynamicBuffer<EntityInHierarchy> hierarchy, int indexToRemove, bool forceKeepHierarchy = false)
        {
            Span<EntityInHierarchy> copy = stackalloc EntityInHierarchy[hierarchy.Length];
            for (int i = 0; i < hierarchy.Length; i++)
                copy[i] = hierarchy[i];

            var parentIndex = hierarchy[indexToRemove].parentIndex;
            hierarchy.RemoveAt(indexToRemove);

            var hierarchyArray       = hierarchy.AsNativeArray();
            var root                 = hierarchyArray[0].entity;
            var oldParentInHierarchy = hierarchyArray[parentIndex];
            oldParentInHierarchy.m_childCount--;
            hierarchyArray[parentIndex] = oldParentInHierarchy;

            for (int i = parentIndex + 1; i < hierarchyArray.Length; i++)
            {
                var temp = hierarchyArray[i];
                temp.m_firstChildIndex--;
                if (temp.parentIndex > indexToRemove)
                    temp.m_parentIndex--;
                if (i >= indexToRemove && em.IsAlive(temp.entity))
                    em.SetComponentData(temp.entity, new RootReference { m_indexInHierarchy = i, m_rootEntity = root });
                hierarchyArray[i]                                                                             = temp;
            }
            Validate(hierarchy);
            if (!forceKeepHierarchy)
            {
                if (hierarchy.Length < 2)
                    em.RemoveComponent(root, new TypePack<EntityInHierarchy, EntityInHierarchyCleanup>());
                else
                    UpdateCleanup(em, root, true);
            }
        }

        static unsafe void RemoveSubtree(ref ThreadStackAllocator tsa,
                                         EntityManager em,
                                         DynamicBuffer<EntityInHierarchy> hierarchyToRemoveFrom,
                                         int subtreeRootIndex,
                                         ReadOnlySpan<EntityInHierarchy>  extractedSubtree,
                                         bool forceKeepHierarchy = false)
        {
            Span<EntityInHierarchy> copy = stackalloc EntityInHierarchy[hierarchyToRemoveFrom.Length];
            for (int i = 0; i < hierarchyToRemoveFrom.Length; i++)
                copy[i] = hierarchyToRemoveFrom[i];

            // Start by decreasing the old parent's child count
            var oldHierarchyArray    = hierarchyToRemoveFrom.AsNativeArray();
            var subtreeParentIndex   = oldHierarchyArray[subtreeRootIndex].parentIndex;
            var oldParentInHierarchy = oldHierarchyArray[subtreeParentIndex];
            oldParentInHierarchy.m_childCount--;
            oldHierarchyArray[subtreeParentIndex] = oldParentInHierarchy;

            // Next, offset any entity first child indices for entities that are prior to the removed child
            for (int i = subtreeParentIndex + 1; i < subtreeRootIndex; i++)
            {
                var temp = oldHierarchyArray[i];
                temp.m_firstChildIndex--;
                oldHierarchyArray[i] = temp;
            }

            // Filter out the subtree in order.
            var dst                = subtreeRootIndex;
            var match              = 1;
            var root               = oldHierarchyArray[0].entity;
            var modifiedSrcStart   = subtreeRootIndex + 1;
            var srcToDstIndicesMap = tsa.AllocateAsSpan<int>(oldHierarchyArray.Length - modifiedSrcStart + 1);
            for (int src = subtreeRootIndex + 1; src < oldHierarchyArray.Length; src++)
            {
                var srcData                                = oldHierarchyArray[src];
                srcToDstIndicesMap[src - modifiedSrcStart] = dst;
                if (match < extractedSubtree.Length && srcData.entity == extractedSubtree[match].entity)
                {
                    match++;
                    continue;
                }
                oldHierarchyArray[dst] = srcData;
                if (em.IsAlive(srcData.entity))
                    em.SetComponentData(srcData.entity, new RootReference { m_rootEntity = root, m_indexInHierarchy = dst });
                dst++;
            }
            srcToDstIndicesMap[oldHierarchyArray.Length - modifiedSrcStart] = dst;

            // Apply indexing conversions
            for (int i = subtreeRootIndex; i < dst; i++)
            {
                var element = oldHierarchyArray[i];
                if (element.parentIndex >= modifiedSrcStart)
                    element.m_parentIndex = srcToDstIndicesMap[element.parentIndex - modifiedSrcStart];
                element.m_firstChildIndex = srcToDstIndicesMap[element.firstChildIndex - modifiedSrcStart];
                oldHierarchyArray[i]      = element;
            }
            hierarchyToRemoveFrom.Length = dst;
            Validate(hierarchyToRemoveFrom);
            if (!forceKeepHierarchy)
            {
                if (hierarchyToRemoveFrom.Length < 2)
                    em.RemoveComponent(root, new TypePack<EntityInHierarchy, EntityInHierarchyCleanup>());
                else
                    UpdateCleanup(em, root, true);
            }
        }

        static unsafe void AddSingleChild(EntityManager em, DynamicBuffer<EntityInHierarchy> hierarchy, int parentIndex, Entity child, InheritanceFlags flags)
        {
            var     root                 = hierarchy[0].entity;
            ref var newParentInHierarchy = ref hierarchy.ElementAt(parentIndex);
            var     insertionPoint       = newParentInHierarchy.firstChildIndex + newParentInHierarchy.childCount;
            newParentInHierarchy.m_childCount++;
            if (insertionPoint == hierarchy.Length)
            {
                var hierarchyArray = hierarchy.AsNativeArray();
                for (int i = parentIndex + 1; i < insertionPoint; i++)
                {
                    var parentElement = hierarchyArray[i];
                    parentElement.m_firstChildIndex++;
                    hierarchyArray[i] = parentElement;
                }
                hierarchy.Add(new EntityInHierarchy
                {
                    m_childCount       = 0,
                    m_descendantEntity = child,
                    m_firstChildIndex  = insertionPoint + 1,
                    m_flags            = flags,
                    m_parentIndex      = parentIndex
                });
                em.SetComponentData(child, new RootReference { m_rootEntity = root, m_indexInHierarchy = insertionPoint });
            }
            else
            {
                var newFirstChildIndex = hierarchy[insertionPoint].firstChildIndex;
                hierarchy.Insert(insertionPoint, new EntityInHierarchy
                {
                    m_childCount       = 0,
                    m_descendantEntity = child,
                    m_firstChildIndex  = newFirstChildIndex,
                    m_flags            = flags,
                    m_parentIndex      = parentIndex
                });
                em.SetComponentData(child, new RootReference { m_rootEntity = root, m_indexInHierarchy = insertionPoint });
                var hierarchyArray                                                                     = hierarchy.AsNativeArray().AsSpan();
                for (int i = parentIndex + 1; i < hierarchyArray.Length; i++)
                {
                    ref var element = ref hierarchyArray[i];
                    element.m_firstChildIndex++;
                    if (element.parentIndex >= insertionPoint)
                        element.m_parentIndex++;
                    if (i > insertionPoint && em.IsAlive(element.entity))
                        em.SetComponentData(element.entity, new RootReference { m_rootEntity = root, m_indexInHierarchy = i });
                }
            }
        }

        static unsafe void InsertSubtree(EntityManager em,
                                         DynamicBuffer<EntityInHierarchy> hierarchy,
                                         int parentIndex,
                                         ReadOnlySpan<EntityInHierarchy>  subtree,
                                         InheritanceFlags flags)
        {
            var hierarchyOriginalLength  = hierarchy.Length;
            hierarchy.Length            += subtree.Length;
            ref var parentInHierarchy    = ref hierarchy.ElementAt(parentIndex);
            var     insertionPoint       = parentInHierarchy.firstChildIndex + parentInHierarchy.childCount;
            parentInHierarchy.m_childCount++;
            var hierarchyArray = hierarchy.AsNativeArray().AsSpan();
            var root           = hierarchyArray[0].entity;
            var child          = subtree[0].entity;
            if (insertionPoint == hierarchyOriginalLength)
            {
                // We are appending the new child to the end, which means we can just copy the whole hierarchy.
                // But first, we need to push any previous firstChildIndex values one past.
                for (int i = parentIndex + 1; i < hierarchyOriginalLength; i++)
                {
                    var parentElement = hierarchyArray[i];
                    parentElement.m_firstChildIndex++;
                    hierarchyArray[i] = parentElement;
                }
                var childElement                                                   = subtree[0];
                childElement.m_parentIndex                                         = parentIndex;
                childElement.m_firstChildIndex                                    += insertionPoint;
                childElement.m_flags                                               = flags;
                em.SetComponentData(child, new RootReference { m_indexInHierarchy  = insertionPoint, m_rootEntity = root });
                hierarchyArray[insertionPoint]                                     = childElement;
                for (int i = 1; i < subtree.Length; i++)
                {
                    childElement                    = subtree[i];
                    childElement.m_parentIndex     += insertionPoint;
                    childElement.m_firstChildIndex += insertionPoint;
                    if (em.IsAlive(childElement.entity))
                        em.SetComponentData(childElement.entity, new RootReference { m_indexInHierarchy = insertionPoint + i, m_rootEntity = root });
                    hierarchyArray[insertionPoint + i]                                                                                     = childElement;
                }
            }
            else
            {
                // Move elements starting at the insertion point to the back
                for (int i = hierarchyOriginalLength - 1; i >= insertionPoint; i--)
                {
                    var src             = i;
                    var dst             = src + subtree.Length;
                    hierarchyArray[dst] = hierarchyArray[src];
                }

                // Adjust first child index of parents preceeding the inserted child
                int existingChildrenToAdd = 0;
                for (int i = parentIndex + 1; i < insertionPoint; i++)
                {
                    var parentElement = hierarchyArray[i];
                    parentElement.m_firstChildIndex++;
                    existingChildrenToAdd += parentElement.childCount;
                    hierarchyArray[i]      = parentElement;
                }

                // Add the new child
                var newChildElement                                               = subtree[0];
                newChildElement.m_parentIndex                                     = parentIndex;
                newChildElement.m_firstChildIndex                                 = insertionPoint + 1 + existingChildrenToAdd;
                newChildElement.m_flags                                           = flags;
                int newChildrenToAdd                                              = newChildElement.childCount;
                em.SetComponentData(child, new RootReference { m_indexInHierarchy = insertionPoint, m_rootEntity = root });
                hierarchyArray[insertionPoint]                                    = newChildElement;

                // Merge the hierarchies by alternating based on accumulated children batches
                int existingChildrenParentShift = 0;
                int existingChildrenChildShift  = 1 + newChildrenToAdd;
                int existingChildRunningIndex   = insertionPoint + subtree.Length;
                int newChildrenLastAdded        = 1;
                int newChildrenParentShift      = insertionPoint;
                int newChildrenChildShift       = insertionPoint + existingChildrenToAdd;
                int newChildRunningIndex        = 1;
                int runningDst                  = insertionPoint + 1;

                while (newChildrenToAdd + existingChildrenToAdd > 0)
                {
                    int nextExistingChildrenToAdd = 0;
                    for (int i = 0; i < existingChildrenToAdd; i++)
                    {
                        var existingElement                = hierarchyArray[existingChildRunningIndex];
                        existingElement.m_parentIndex     += existingChildrenParentShift;
                        existingElement.m_firstChildIndex += existingChildrenChildShift;
                        nextExistingChildrenToAdd         += existingElement.childCount;
                        if (em.IsAlive(existingElement.entity))
                            em.SetComponentData(existingElement.entity, new RootReference { m_indexInHierarchy = runningDst, m_rootEntity = root });
                        hierarchyArray[runningDst]                                                                                        = existingElement;
                        existingChildRunningIndex++;
                        runningDst++;
                    }
                    newChildrenChildShift       += nextExistingChildrenToAdd;
                    existingChildrenParentShift += newChildrenLastAdded;

                    int nextNewChildrenToAdd = 0;
                    for (int i = 0; i < newChildrenToAdd; i++)
                    {
                        var newElement                = subtree[newChildRunningIndex];
                        newElement.m_parentIndex     += newChildrenParentShift;
                        newElement.m_firstChildIndex += newChildrenChildShift;
                        nextNewChildrenToAdd         += newElement.childCount;
                        if (em.IsAlive(newElement.entity))
                            em.SetComponentData(newElement.entity, new RootReference { m_indexInHierarchy = runningDst, m_rootEntity = root });
                        hierarchyArray[runningDst]                                                                                   = newElement;
                        newChildRunningIndex++;
                        runningDst++;
                    }
                    existingChildrenChildShift += nextNewChildrenToAdd;
                    newChildrenParentShift     += existingChildrenToAdd;
                    newChildrenLastAdded        = newChildrenToAdd;
                    newChildrenToAdd            = nextNewChildrenToAdd;
                    existingChildrenToAdd       = nextExistingChildrenToAdd;
                }
            }
        }
        #endregion

        #region
        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        static void CheckChangeParent(EntityManager em, Entity parent, Entity child, InheritanceFlags flags, bool transferLEG)
        {
            if (parent == child)
                throw new ArgumentException($"Cannot make an entity a child of itself. {parent.ToFixedString()}");
            if (!em.Exists(parent))
                throw new ArgumentException($"The parent does not exist. Parent: {parent.ToFixedString()}  Child: {child.ToFixedString()}");
            if (!em.IsAlive(parent))
                throw new ArgumentException($"The parent has been destroyed. Parent: {parent.ToFixedString()}  Child: {child.ToFixedString()}");
            if (!em.Exists(child))
                throw new ArgumentException($"The child does not exist. Parent: {parent.ToFixedString()}  Child: {child.ToFixedString()}");
            if (!em.IsAlive(child))
                throw new ArgumentException($"The child has been destroyed. Parent: {parent.ToFixedString()}  Child: {child.ToFixedString()}");
            if (transferLEG && em.HasComponent<RootReference>(parent))
            {
                var rootRef = em.GetComponentData<RootReference>(parent);
                if (!em.IsAlive(rootRef.rootEntity))
                    throw new InvalidOperationException(
                        $"Cannot transfer LinkedEntityGroup to a new hierarchy whose root has been destroyed. Root: {rootRef.rootEntity.ToFixedString()}");
            }
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        static void CheckNotAssigningChildToDescendant(EntityManager em, Entity parent, Entity child, RootReference parentRootRef, RootReference childRootRef)
        {
            if (childRootRef.indexInHierarchy > parentRootRef.indexInHierarchy)
                return;
            var handle = parentRootRef.ToHandle(em).bloodParent;
            while (!handle.isRoot)
            {
                if (handle.entity == child)
                    throw new System.ArgumentException(
                        $"Cannot make an entity a child of one of its own descendants. Reassign the descendant's parent first. Parent: {parent.ToFixedString()}  Child: {child.ToFixedString()}");
                handle = handle.bloodParent;
            }
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        static void CheckNotAssigningRootChildToDescendant(EntityManager em, Entity parent, Entity rootChild)
        {
            var rootRef = em.GetComponentData<RootReference>(parent);
            if (rootRef.rootEntity == rootChild)
                throw new System.ArgumentException(
                    $"Cannot make an entity a child of one of its own descendants. Reassign the descendant's parent first. Parent: {parent.ToFixedString()}  Child: {rootChild.ToFixedString()}");
        }

        [Conditional("VALIDATE")]
        static void Validate(EntityManager em, Entity parent, Entity child)
        {
            var rootRef      = em.GetComponentData<RootReference>(child);
            var handle       = rootRef.ToHandle(em);
            var parentHandle = handle.bloodParent;
            var last         = handle.GetFromIndexInHierarchy(handle.totalInHierarchy - 1);
            if (last.m_hierarchy[last.indexInHierarchy].firstChildIndex != last.totalInHierarchy)
            {
                throw new System.InvalidOperationException("Bad things happened during validation.");
            }
            for (int i = 1; i < last.m_hierarchy.Length; i++)
            {
                var b = last.m_hierarchy[i];
                var a = last.m_hierarchy[i - 1];
                if (b.firstChildIndex != a.firstChildIndex + a.childCount)
                    throw new System.InvalidOperationException("Bad things happened during validation.");
            }
            if (handle.entity != child || parentHandle.entity != parent)
            {
                throw new System.InvalidOperationException("Our entities got mixed up.");
            }
        }

        [Conditional("VALIDATE")]
        static void Validate(DynamicBuffer<EntityInHierarchy> hierarchy)
        {
            if (hierarchy[hierarchy.Length - 1].firstChildIndex != hierarchy.Length)
            {
                throw new System.InvalidOperationException("Bad removal state.");
            }
            for (int i = 1; i < hierarchy.Length; i++)
            {
                var b = hierarchy[i];
                var a = hierarchy[i - 1];
                if (b.firstChildIndex != a.firstChildIndex + a.childCount)
                    throw new System.InvalidOperationException("Bad removal state.");
            }
        }

        [Conditional("VALIDATE")]
        static void ValidateRemoval(EntityManager em, Entity oldRoot)
        {
            if (em.HasBuffer<EntityInHierarchy>(oldRoot))
            {
                var hierarchy = em.GetBuffer<EntityInHierarchy>(oldRoot);
                if (hierarchy.Length < 2)
                    throw new System.InvalidOperationException("Residue not cleaned up.");
            }
        }
        #endregion
    }
}

