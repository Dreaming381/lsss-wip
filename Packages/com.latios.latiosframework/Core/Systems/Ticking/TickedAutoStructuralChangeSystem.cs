using System.Reflection;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

// Todo: This system's implementation currently performs structural changes one component at a time.
// This will be improved once setting archetypes on arrays of entities is supported.

namespace Latios.Systems
{
    [UpdateInGroup(typeof(TickedArchetypeCorrectionSystemGroup))]
    [RequireMatchingQueriesForUpdate]
    [DisableAutoCreation]
    [BurstCompile]
    public partial struct TickedAutoStructuralChangeSystem : ISystem
    {
        NativeList<TypePairState> typePairStates;

        public void OnCreate(ref SystemState state)
        {
            typePairStates = new NativeList<TypePairState>(Allocator.Persistent);
            foreach (var typeInfo in TypeManager.AllTypes)
            {
                if (typeInfo.BakingOnlyType || typeInfo.TemporaryBakingType)
                    continue;
                TickedAutoAddAttribute attribute = null;
                try
                {
                    attribute = typeInfo.Type.GetCustomAttribute<TickedAutoAddAttribute>();
                }
                catch
                {
                }
                if (attribute == null)
                    continue;
                if (attribute.nonTickedType == null)
                    continue;

                TypeIndex nonTickedTypeIndex = default;
                try
                {
                    nonTickedTypeIndex = TypeManager.GetTypeIndex(attribute.nonTickedType);
                }
                catch
                {
                    throw new System.InvalidOperationException(
                        $"On {typeInfo.Type.FullName}, the TickedAutoAdd attribute specifies a non-ticked type {attribute.nonTickedType.FullName} which is not a known component type.");
                }

                var nonTickingType = ComponentType.FromTypeIndex(nonTickedTypeIndex);
                var tickingType    = ComponentType.FromTypeIndex(typeInfo.TypeIndex);
                if (attribute.copyData)
                {
                    var nonTickedInfo = TypeManager.GetTypeInfo(nonTickedTypeIndex);
                    if (typeInfo.ElementSize != nonTickedInfo.ElementSize || typeInfo.TypeIndex.IsBuffer != nonTickedTypeIndex.IsBuffer ||
                        typeInfo.TypeIndex.IsEnableable != nonTickedTypeIndex.IsEnableable || typeInfo.TypeIndex.IsSharedComponentType || typeInfo.TypeIndex.IsManagedType)
                    {
                        throw new System.InvalidOperationException(
                            $"On {typeInfo.Type.FullName}, the TickedAutoAdd attribute specifies a non-ticked type {attribute.nonTickedType.FullName} which is not compatible for data copying either due to wrong component type or mismatched size.");
                    }
                }
                typePairStates.Add(new TypePairState
                {
                    nonTickingType      = nonTickingType,
                    tickingType         = tickingType,
                    nonTickingHandle    = attribute.copyData ? state.GetDynamicComponentTypeHandle(nonTickingType) : default,
                    tickingHandle       = attribute.copyData ? state.GetDynamicComponentTypeHandle(tickingType) : default,
                    missingTickingQuery = state.Fluent().With<TickedEntityTag>(true).With(nonTickedTypeIndex, !attribute.copyData).Without(
                        typeInfo.TypeIndex).IncludeDisabledEntities().IncludePrefabs().Build(),
                    missingNormalQuery = state.Fluent().Without<TickingOnlyEntityTag>().With(typeInfo.TypeIndex, !attribute.copyData).Without(
                        nonTickedTypeIndex).IncludeDisabledEntities().IncludePrefabs().Build(),
                    removeTickingQuery = state.Fluent().Without<TickedEntityTag>().With(typeInfo.TypeIndex, true).IncludeDisabledEntities().IncludePrefabs().Build(),
                    removeNormalQuery  = state.Fluent().With<TickingOnlyEntityTag>().With(nonTickedTypeIndex, true).IncludeDisabledEntities().IncludePrefabs().Build(),
                    copyData           = attribute.copyData
                });
            }
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            typePairStates.Dispose();
        }

        [BurstCompile]
        public unsafe void OnUpdate(ref SystemState state)
        {
            foreach (var typePairState in typePairStates)
            {
                if (!typePairState.missingTickingQuery.IsEmptyIgnoreFilter)
                {
                    if (typePairState.copyData)
                    {
                        var entities = typePairState.missingTickingQuery.ToEntityArray(state.WorldUpdateAllocator);
                        state.EntityManager.AddComponent(typePairState.missingTickingQuery, typePairState.tickingType);
                        typePairState.nonTickingHandle.Update(ref state);
                        typePairState.tickingHandle.Update(ref state);
                        var writeHandle = typePairState.tickingHandle;
                        var readHandle  = typePairState.nonTickingHandle.CopyToReadOnly();
                        var typeSize    = TypeManager.GetTypeInfo(typePairState.tickingType.TypeIndex).ElementSize;
                        if (typePairState.tickingType.IsBuffer)
                        {
                            foreach (var entity in entities)
                            {
                                var esi           = state.EntityManager.GetStorageInfo(entity);
                                var readAccessor  = esi.Chunk.GetUntypedBufferAccessor(ref readHandle);
                                var writeAccessor = esi.Chunk.GetUntypedBufferAccessor(ref writeHandle);
                                var readPtr       = readAccessor.GetUnsafeReadOnlyPtrAndLength(esi.IndexInChunk, out var length);
                                writeAccessor.ResizeUninitialized(esi.IndexInChunk, length);
                                var writePtr = writeAccessor.GetUnsafePtr(esi.IndexInChunk);
                                UnsafeUtility.MemCpy(writePtr, readPtr, length * typeSize);
                            }
                        }
                        else if (!typePairState.tickingType.IsZeroSized)
                        {
                            foreach (var entity in entities)
                            {
                                var esi      = state.EntityManager.GetStorageInfo(entity);
                                var readPtr  = (byte*)esi.Chunk.GetDynamicComponentDataArrayReinterpret<byte>(ref readHandle, typeSize).GetUnsafeReadOnlyPtr();
                                var writePtr = (byte*)esi.Chunk.GetDynamicComponentDataArrayReinterpret<byte>(ref writeHandle, typeSize).GetUnsafePtr();
                                UnsafeUtility.MemCpy(writePtr + typeSize * esi.IndexInChunk, readPtr + typeSize * esi.IndexInChunk, typeSize);
                            }
                        }
                        if (typePairState.tickingType.IsEnableable)
                        {
                            foreach (var entity in entities)
                            {
                                bool enabled = state.EntityManager.IsComponentEnabled(entity, typePairState.nonTickingType);
                                state.EntityManager.SetComponentEnabled(entity, typePairState.tickingType, enabled);
                            }
                        }
                    }
                    else
                    {
                        state.EntityManager.AddComponent(typePairState.missingTickingQuery, typePairState.tickingType);
                    }
                }
                if (!typePairState.missingNormalQuery.IsEmptyIgnoreFilter)
                {
                    if (typePairState.copyData)
                    {
                        var entities = typePairState.missingNormalQuery.ToEntityArray(state.WorldUpdateAllocator);
                        state.EntityManager.AddComponent(typePairState.missingNormalQuery, typePairState.nonTickingType);
                        typePairState.nonTickingHandle.Update(ref state);
                        typePairState.tickingHandle.Update(ref state);
                        var writeHandle = typePairState.nonTickingHandle;
                        var readHandle  = typePairState.tickingHandle.CopyToReadOnly();
                        var typeSize    = TypeManager.GetTypeInfo(typePairState.tickingType.TypeIndex).ElementSize;
                        if (typePairState.tickingType.IsBuffer)
                        {
                            foreach (var entity in entities)
                            {
                                var esi           = state.EntityManager.GetStorageInfo(entity);
                                var readAccessor  = esi.Chunk.GetUntypedBufferAccessor(ref readHandle);
                                var writeAccessor = esi.Chunk.GetUntypedBufferAccessor(ref writeHandle);
                                var readPtr       = readAccessor.GetUnsafeReadOnlyPtrAndLength(esi.IndexInChunk, out var length);
                                writeAccessor.ResizeUninitialized(esi.IndexInChunk, length);
                                var writePtr = writeAccessor.GetUnsafePtr(esi.IndexInChunk);
                                UnsafeUtility.MemCpy(writePtr, readPtr, length * typeSize);
                            }
                        }
                        else if (!typePairState.tickingType.IsZeroSized)
                        {
                            foreach (var entity in entities)
                            {
                                var esi      = state.EntityManager.GetStorageInfo(entity);
                                var readPtr  = (byte*)esi.Chunk.GetDynamicComponentDataArrayReinterpret<byte>(ref readHandle, typeSize).GetUnsafeReadOnlyPtr();
                                var writePtr = (byte*)esi.Chunk.GetDynamicComponentDataArrayReinterpret<byte>(ref writeHandle, typeSize).GetUnsafePtr();
                                UnsafeUtility.MemCpy(writePtr + typeSize * esi.IndexInChunk, readPtr + typeSize * esi.IndexInChunk, typeSize);
                            }
                        }
                        if (typePairState.tickingType.IsEnableable)
                        {
                            foreach (var entity in entities)
                            {
                                bool enabled = state.EntityManager.IsComponentEnabled(entity, typePairState.tickingType);
                                state.EntityManager.SetComponentEnabled(entity, typePairState.nonTickingType, enabled);
                            }
                        }
                    }
                    else
                    {
                        state.EntityManager.AddComponent(typePairState.missingTickingQuery, typePairState.tickingType);
                    }
                }
                if (!typePairState.removeTickingQuery.IsEmptyIgnoreFilter)
                {
                    state.EntityManager.RemoveComponent(typePairState.removeTickingQuery, typePairState.tickingType);
                }
                if (!typePairState.removeNormalQuery.IsEmptyIgnoreFilter)
                {
                    state.EntityManager.RemoveComponent(typePairState.removeNormalQuery, typePairState.nonTickingType);
                }
            }
        }

        struct TypePairState
        {
            public ComponentType              nonTickingType;
            public ComponentType              tickingType;
            public EntityQuery                missingTickingQuery;
            public EntityQuery                missingNormalQuery;
            public EntityQuery                removeTickingQuery;
            public EntityQuery                removeNormalQuery;
            public DynamicComponentTypeHandle nonTickingHandle;
            public DynamicComponentTypeHandle tickingHandle;
            public bool                       copyData;
        }
    }
}

