using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

using static Unity.Entities.SystemAPI;
using static UnityEngine.Rendering.VirtualTexturing.Debugging;

namespace Latios.Transforms.Systems
{
    [UpdateInGroup(typeof(Latios.Systems.TickedArchetypeCorrectionSystemGroup))]
    [RequireMatchingQueriesForUpdate]
    [DisableAutoCreation]
    [BurstCompile]
    public partial struct TickedTransformsArchetypeFixupSystem : ISystem
    {
        LatiosWorldUnmanaged latiosWorld;

        EntityQuery m_rootMissingTickedQuery;
        EntityQuery m_rootMissingNormalQuery;
        EntityQuery m_soloRemoveTickedQuery;
        EntityQuery m_soloRemoveNormalQuery;
        EntityQuery m_rootWithHierarchyRemoveTickedQuery;
        EntityQuery m_rootWithHierarchyRemoveNormalQuery;
        EntityQuery m_childAddTickedQuery;
        EntityQuery m_childAddNormalQuery;
        EntityQuery m_childRemoveTickedQuery;
        EntityQuery m_childRemoveNormalQuery;
        EntityQuery m_childMissingTickedCacheQuery;
        EntityQuery m_removeTickedCacheQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            latiosWorld = state.GetLatiosWorldUnmanaged();

            m_rootMissingTickedQuery = state.Fluent().With<TickedEntityTag, WorldTransform>(true).Without<TickedWorldTransform, RootReference>()
                                       .IncludeDisabledEntities().IncludePrefabs().Build();
            m_rootMissingNormalQuery = state.Fluent().With<TickedWorldTransform>(true).Without<WorldTransform, RootReference, TickingOnlyEntityTag>()
                                       .IncludeDisabledEntities().IncludePrefabs().Build();
            m_soloRemoveTickedQuery = state.Fluent().With<TickedWorldTransform>(true).Without<TickedEntityTag, RootReference, EntityInHierarchy>()
                                      .IncludeDisabledEntities().IncludePrefabs().Build();
            m_soloRemoveNormalQuery = state.Fluent().With<WorldTransform, TickingOnlyEntityTag, TickedEntityTag>(true).Without<RootReference, EntityInHierarchy>()
                                      .IncludeDisabledEntities().IncludePrefabs().Build();

            m_rootWithHierarchyRemoveTickedQuery = state.Fluent().With<EntityInHierarchy, TickedWorldTransform>(true).Without<TickedEntityTag>()
                                                   .IncludeDisabledEntities().IncludePrefabs().Build();
            m_rootWithHierarchyRemoveNormalQuery = state.Fluent().With<EntityInHierarchy, WorldTransform, TickingOnlyEntityTag>(true).With<TickedEntityTag>(true)
                                                   .IncludeDisabledEntities().IncludePrefabs().Build();
            m_childAddTickedQuery = state.Fluent().With<RootReference, WorldTransform, TickedEntityTag>(true).Without<TickedWorldTransform>()
                                    .IncludeDisabledEntities().IncludePrefabs().Build();
            m_childAddNormalQuery = state.Fluent().With<RootReference, TickedWorldTransform>(true).Without<TickingOnlyEntityTag>()
                                    .IncludeDisabledEntities().IncludePrefabs().Build();
            m_childRemoveTickedQuery = state.Fluent().With<RootReference, TickedWorldTransform>(true).Without<TickedEntityTag>()
                                       .IncludeDisabledEntities().IncludePrefabs().Build();
            m_childRemoveNormalQuery = state.Fluent().With<RootReference, WorldTransform, TickingOnlyEntityTag>(true)
                                       .IncludeDisabledEntities().IncludePrefabs().Build();

            m_childMissingTickedCacheQuery = state.Fluent().With<RootReference, TickedPreviousTransform>(true).Without<TickedPreviousLocalTransformCache>()
                                             .IncludeDisabledEntities().IncludePrefabs().Build();
            m_removeTickedCacheQuery = state.Fluent().With<TickedPreviousLocalTransformCache>(true).Without<RootReference>().IncludeDisabledEntities().IncludePrefabs().Build();

            m_rootWithHierarchyRemoveTickedQuery.AddOrderVersionFilter();
            m_rootWithHierarchyRemoveNormalQuery.AddOrderVersionFilter();
            m_childAddTickedQuery.AddOrderVersionFilter();
            m_childAddNormalQuery.AddOrderVersionFilter();
            m_childRemoveTickedQuery.AddOrderVersionFilter();
            m_childRemoveNormalQuery.AddOrderVersionFilter();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!m_rootMissingTickedQuery.IsEmptyIgnoreFilter)
            {
                var entities = m_rootMissingTickedQuery.ToEntityArray(state.WorldUpdateAllocator);
                state.EntityManager.AddComponent(m_rootMissingTickedQuery, new TypePack<TickedWorldTransform, TickedPreviousTransform>());
                foreach (var entity in entities)
                {
                    var qvvs                                                          = GetComponent<WorldTransform>(entity).worldTransform;
                    SetComponent(entity, new TickedWorldTransform { worldTransform    = qvvs });
                    SetComponent(entity, new TickedPreviousTransform { worldTransform = qvvs });
                }
            }
            if (!m_rootMissingNormalQuery.IsEmptyIgnoreFilter)
            {
                var qvvsArray = m_rootMissingNormalQuery.ToComponentDataArray<TickedWorldTransform>(state.WorldUpdateAllocator);
                state.EntityManager.AddComponentData(m_rootMissingNormalQuery, qvvsArray.Reinterpret<WorldTransform>());
            }
            if (!m_soloRemoveTickedQuery.IsEmptyIgnoreFilter)
            {
                state.EntityManager.RemoveComponent(m_soloRemoveTickedQuery, new TypePack<TickedWorldTransform, TickedPreviousTransform, TickedTwoAgoTransform>());
            }
            if (!m_soloRemoveNormalQuery.IsEmptyIgnoreFilter)
            {
                state.EntityManager.RemoveComponent(m_soloRemoveNormalQuery, new TypePack<WorldTransform, PreviousTransform, TwoAgoTransform>());
            }
            var rootsToEvaluate = new NativeList<Entity>(state.WorldUpdateAllocator);
            var rootsSet        = new NativeHashSet<Entity>(128, state.WorldUpdateAllocator);
            if (!m_rootWithHierarchyRemoveTickedQuery.IsEmptyIgnoreFilter)
            {
                var entities = m_rootWithHierarchyRemoveTickedQuery.ToEntityArray(state.WorldUpdateAllocator);
                rootsToEvaluate.AddRange(entities);
                foreach (var entity in entities)
                    rootsSet.Add(entity);
            }
            if (!m_rootWithHierarchyRemoveNormalQuery.IsEmptyIgnoreFilter)
            {
                var entities = m_rootWithHierarchyRemoveNormalQuery.ToEntityArray(state.WorldUpdateAllocator);
                rootsToEvaluate.AddRange(entities);
                foreach (var entity in entities)
                    rootsSet.Add(entity);
            }
            if (!m_childAddTickedQuery.IsEmptyIgnoreFilter)
            {
                var rootReferences = m_childAddTickedQuery.ToComponentDataArray<RootReference>(state.WorldUpdateAllocator);
                foreach (var rr in rootReferences)
                {
                    if (rootsSet.Add(rr.rootEntity))
                        rootsToEvaluate.Add(rr.rootEntity);
                }
            }
            if (!m_childAddNormalQuery.IsEmptyIgnoreFilter)
            {
                var rootReferences = m_childAddNormalQuery.ToComponentDataArray<RootReference>(state.WorldUpdateAllocator);
                foreach (var rr in rootReferences)
                {
                    if (rootsSet.Add(rr.rootEntity))
                        rootsToEvaluate.Add(rr.rootEntity);
                }
            }
            if (!m_childRemoveTickedQuery.IsEmptyIgnoreFilter)
            {
                var rootReferences = m_childRemoveTickedQuery.ToComponentDataArray<RootReference>(state.WorldUpdateAllocator);
                foreach (var rr in rootReferences)
                {
                    if (rootsSet.Add(rr.rootEntity))
                        rootsToEvaluate.Add(rr.rootEntity);
                }
            }
            if (!m_childRemoveNormalQuery.IsEmptyIgnoreFilter)
            {
                var rootReferences = m_childRemoveNormalQuery.ToComponentDataArray<RootReference>(state.WorldUpdateAllocator);
                foreach (var rr in rootReferences)
                {
                    if (rootsSet.Add(rr.rootEntity))
                        rootsToEvaluate.Add(rr.rootEntity);
                }
            }

            var        ecb            = new EntityCommandBuffer(state.WorldUpdateAllocator);
            const byte hasNormalBit   = 1;
            const byte hasTickedBit   = 2;
            const byte needsNormalBit = 4;
            const byte needsTickedBit = 8;
            var        bitArray       = new NativeList<byte>(state.WorldUpdateAllocator);
            foreach (var root in rootsToEvaluate)
            {
                var rootHandle = TransformTools.GetHierarchyHandle(root, state.EntityManager);
                bitArray.Clear();
                bitArray.AddReplicate(0, rootHandle.totalInHierarchy);

                for (int i = 0; i < bitArray.Length; i++)
                {
                    var currentHandle = rootHandle.GetFromIndexInHierarchy(i);
                    if (!state.EntityManager.IsAlive(currentHandle.entity))
                        continue;
                    var hasNormal   = state.EntityManager.HasComponent<WorldTransform>(currentHandle.entity);
                    var hasTicked   = state.EntityManager.HasComponent<TickedWorldTransform>(currentHandle.entity);
                    var needsTicked = state.EntityManager.HasComponent<TickedEntityTag>(currentHandle.entity);
                    var needsNormal = !needsTicked || !state.EntityManager.HasComponent<TickingOnlyEntityTag>(currentHandle.entity);
                    if (hasNormal)
                        bitArray[i] += hasNormalBit;
                    if (hasTicked)
                        bitArray[i] += hasTickedBit;
                    if (needsNormal)
                        bitArray[i] += needsNormalBit;
                    if (needsTicked)
                        bitArray[i] += needsTickedBit;

                    while (!currentHandle.isRoot)
                    {
                        currentHandle            = currentHandle.bloodParent;
                        var  parentIndex         = currentHandle.indexInHierarchy;
                        bool wasMissingSomething = false;
                        if (needsNormal && (bitArray[parentIndex] & needsNormalBit) == 0)
                        {
                            wasMissingSomething    = true;
                            bitArray[parentIndex] += needsNormalBit;
                        }
                        if (needsTicked && (bitArray[parentIndex] & hasTickedBit) == 0)
                        {
                            wasMissingSomething    = true;
                            bitArray[parentIndex] += needsTickedBit;
                        }
                        if (!wasMissingSomething)
                            break;
                    }
                }

                var rootNormalBits = bitArray[0] & (hasNormalBit + needsNormalBit);
                if (rootNormalBits == hasNormalBit)
                    ecb.RemoveComponent(root, new TypePack<WorldTransform, PreviousTransform, TwoAgoTransform>());
                else if (rootNormalBits == needsNormalBit)
                {
                    var qvvs                                                   = state.EntityManager.GetComponentData<TickedWorldTransform>(root).worldTransform;
                    ecb.AddComponent(root, new WorldTransform { worldTransform = qvvs });
                }
                var rootTickedBits = bitArray[0] & (hasTickedBit + needsTickedBit);
                if (rootTickedBits == hasTickedBit)
                    ecb.RemoveComponent(root, new TypePack<TickedWorldTransform, TickedPreviousTransform, TickedTwoAgoTransform>());
                else if (rootTickedBits == needsTickedBit)
                {
                    var qvvs                                                          = state.EntityManager.GetComponentData<WorldTransform>(root).worldTransform;
                    var previousQvvs                                                  = state.EntityManager.HasComponent<Prefab>(root) ? qvvs : default;
                    ecb.AddComponents(root, new TickedWorldTransform { worldTransform = qvvs }, new TickedPreviousTransform { worldTransform = previousQvvs});
                }

                for (int i = 1; i < bitArray.Length; i++)
                {
                    var handle     = rootHandle.GetFromIndexInHierarchy(i);
                    var normalBits = bitArray[i] & (hasNormalBit + needsNormalBit);
                    if (normalBits == hasNormalBit)
                        ecb.RemoveComponent(handle.entity, new TypePack<WorldTransform, PreviousTransform, TwoAgoTransform>());
                    else if (normalBits == needsNormalBit)
                    {
                        var qvvs =
                            state.EntityManager.GetComponentData<TickedWorldTransform>(handle.entity).worldTransform;
                        ecb.AddComponent(handle.entity, new WorldTransform { worldTransform = qvvs });
                        WorldLocalOps.CopyLocal(in handle, false);
                    }
                    var tickedBits = bitArray[i] & (hasTickedBit + needsTickedBit);
                    if (tickedBits == hasTickedBit)
                        ecb.RemoveComponent(handle.entity, new TypePack<TickedWorldTransform, TickedPreviousTransform, TickedPreviousLocalTransformCache, TickedTwoAgoTransform>());
                    else if (tickedBits == needsTickedBit)
                    {
                        var qvvs         = state.EntityManager.GetComponentData<WorldTransform>(handle.entity).worldTransform;
                        var previousQvvs = state.EntityManager.HasComponent<Prefab>(handle.entity) ? qvvs : default;
                        WorldLocalOps.CopyLocal(in handle, true);
                        ecb.AddComponents(handle.entity,
                                          new TickedWorldTransform { worldTransform    = qvvs},
                                          new TickedPreviousTransform { worldTransform = previousQvvs},
                                          WorldLocalOps.CopyTickedLocalToCache(in handle));
                    }
                }
            }

            ecb.Playback(state.EntityManager);

            if (!m_childMissingTickedCacheQuery.IsEmptyIgnoreFilter)
            {
                var rootRefs    = m_childMissingTickedCacheQuery.ToComponentDataArray<RootReference>(state.WorldUpdateAllocator);
                var localCaches = CollectionHelper.CreateNativeArray<TickedPreviousLocalTransformCache>(rootRefs.Length,
                                                                                                        state.WorldUpdateAllocator,
                                                                                                        NativeArrayOptions.UninitializedMemory);
                for (int i = 0; i < rootRefs.Length; i++)
                {
                    var handle     = rootRefs[i].ToHandle(state.EntityManager);
                    localCaches[i] = new TickedPreviousLocalTransformCache
                    {
                        position = handle.m_hierarchy[handle.indexInHierarchy].m_tickedLocalPosition,
                        scale    = handle.m_hierarchy[handle.indexInHierarchy].m_tickedLocalScale
                    };
                }
                state.EntityManager.AddComponentData(m_childMissingTickedCacheQuery, localCaches);
            }
            if (!m_removeTickedCacheQuery.IsEmptyIgnoreFilter)
            {
                state.EntityManager.RemoveComponent<TickedPreviousLocalTransformCache>(m_removeTickedCacheQuery);
            }
        }
    }
}

