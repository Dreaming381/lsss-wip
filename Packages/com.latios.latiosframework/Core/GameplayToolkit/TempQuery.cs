using System.Diagnostics;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Exposed;
using Unity.Mathematics;

namespace Latios
{
    public struct TempQuery
    {
        #region API
        public TempQuery(NativeArray<EntityArchetype> archetypes,
                         EntityStorageInfoLookup entityStorageInfoLookup,
                         ComponentTypeSet with,
                         ComponentTypeSet withAny = default,
                         ComponentTypeSet without = default,
                         EntityQueryOptions options = EntityQueryOptions.Default)
        {
            esil                    = entityStorageInfoLookup;
            archetypesArray         = archetypes;
            withTypes               = with;
            requiredTypeCount       = (short)with.Length;
            requiredChunkTypeCount  = 0;
            requiredBufferTypeCount = 0;
            bloomMask               = 0;
            for (int i = 0; i < with.Length; i++)
            {
                if (with.GetTypeIndex(i).IsChunkComponent)
                    requiredChunkTypeCount++;
                if (with.GetTypeIndex(i).IsBuffer)
                    requiredBufferTypeCount++;
                bloomMask |= TypeManager.GetTypeInfo(with.GetTypeIndex(i)).BloomFilterMask;
                if (with.GetTypeIndex(i) == TypeManager.GetTypeIndex<Prefab>())
                    options |= EntityQueryOptions.IncludePrefab;
                if (with.GetTypeIndex(i) == TypeManager.GetTypeIndex<ChunkHeader>())
                    options |= EntityQueryOptions.IncludeMetaChunks;
                if (with.GetTypeIndex(i) == TypeManager.GetTypeIndex<Disabled>())
                    options |= EntityQueryOptions.IncludeDisabledEntities;
            }
            withAnyTypes      = withAny;
            bool anyHasChunk  = false;
            bool anyHasBuffer = false;
            for (int i = 0; i < with.Length; i++)
            {
                if (withAny.GetTypeIndex(i).IsChunkComponent)
                    anyHasChunk = true;
                if (withAny.GetTypeIndex(i).IsBuffer)
                    anyHasBuffer = true;
                if (withAny.GetTypeIndex(i) == TypeManager.GetTypeIndex<Prefab>())
                    options |= EntityQueryOptions.IncludePrefab;
                if (withAny.GetTypeIndex(i) == TypeManager.GetTypeIndex<ChunkHeader>())
                    options |= EntityQueryOptions.IncludeMetaChunks;
                if (withAny.GetTypeIndex(i) == TypeManager.GetTypeIndex<Disabled>())
                    options |= EntityQueryOptions.IncludeDisabledEntities;
            }
            if (anyHasChunk)
                requiredChunkTypeCount++;
            if (anyHasBuffer)
                requiredBufferTypeCount++;
            withoutTypes       = without;
            packedQueryOptions = (byte)options;
        }

        public TempArchetypeEnumerator archetypes => new TempArchetypeEnumerator { query = this, currentIndex = -1 };
        public TempChunkEnumerator<TempArchetypeEnumerator> chunks => archetypes.chunks;
        public TempEntityEnumerator<TempMaskedChunkEnumerator<TempChunkEnumerator<TempArchetypeEnumerator>>> entities => chunks.masked.entities;
        #endregion

        #region Fields
        internal EntityStorageInfoLookup      esil;
        internal NativeArray<EntityArchetype> archetypesArray;
        internal ComponentTypeSet             withTypes;
        internal ComponentTypeSet             withoutTypes;
        internal ComponentTypeSet             withAnyTypes;
        internal ulong                        bloomMask;
        internal short                        requiredTypeCount;
        internal short                        requiredChunkTypeCount;
        internal short                        requiredBufferTypeCount;
        internal byte                         packedQueryOptions;

        internal EntityQueryOptions queryOptions
        {
            get => (EntityQueryOptions)packedQueryOptions;
            set => packedQueryOptions = (byte)value;
        }
        #endregion

        #region Safety
        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        internal static void CheckValid(EntityStorageInfoLookup esil) => esil.Exists(Entity.Null);
        #endregion

        #region Helpers

        #endregion
    }

    public interface ITempArchetypeEnumerator
    {
        EntityStorageInfoLookup entityStorageInfoLookup { get; }
        EntityArchetype Current { get; }
        bool MoveNext();
    }

    public struct TempArchetypeEnumerator : ITempArchetypeEnumerator
    {
        internal TempQuery query;
        internal int       currentIndex;

        public TempChunkEnumerator<TempArchetypeEnumerator> chunks => new TempChunkEnumerator<TempArchetypeEnumerator>(this);

        public TempArchetypeEnumerator GetEnumerator() => this;

        public EntityStorageInfoLookup entityStorageInfoLookup => query.esil;

        public EntityArchetype Current
        {
            get
            {
                TempQuery.CheckValid(query.esil);
                return query.archetypesArray[currentIndex];
            }
        }
        public bool MoveNext()
        {
            TempQuery.CheckValid(query.esil);
            while (true)
            {
                currentIndex++;
                if (currentIndex >= query.archetypesArray.Length)
                    return false;

                var archetype = query.archetypesArray[currentIndex];

                if (archetype.ChunkCount == 0)
                    continue;

                if ((archetype.GetBloomMask() & query.bloomMask) != query.bloomMask)
                    continue;

                if (archetype.TypesCount < query.requiredTypeCount)
                    continue;

                if (archetype.Prefab && (query.queryOptions & EntityQueryOptions.IncludePrefab) == EntityQueryOptions.Default)
                    continue;
                if (archetype.Disabled && (query.queryOptions & EntityQueryOptions.IncludeDisabledEntities) == EntityQueryOptions.Default)
                    continue;
                if (archetype.HasChunkHeader() && (query.queryOptions & EntityQueryOptions.IncludeMetaChunks) == EntityQueryOptions.Default)
                    continue;
                if (archetype.HasSystemInstanceComponents() && (query.queryOptions & EntityQueryOptions.IncludeSystems) == EntityQueryOptions.Default)
                    continue;
                if (query.requiredChunkTypeCount > archetype.GetChunkComponentCount())
                    continue;
                if (query.requiredBufferTypeCount > archetype.GetBufferComponentCount())
                    continue;

                // Todo: We could do more early-out checks here, such as checking type flags used in the query for object refs or other specialties.

                // Time to do detailed analysis. First, required types.
                if (query.withTypes.Length > 0)
                {
                    bool missingRequired  = true;
                    int  queryTypeIndex   = 0;
                    var  currentQueryType = query.withTypes.GetTypeIndex(0);
                    for (int i = 0; i < archetype.TypesCount; i++)
                    {
                        var archetypeType = archetype.GetTypeAtIndex(i);
                        if (archetypeType.Value == currentQueryType.Value)
                        {
                            queryTypeIndex++;
                            if (queryTypeIndex >= archetype.TypesCount)
                            {
                                missingRequired = false;
                                break;
                            }
                            currentQueryType = query.withTypes.GetTypeIndex(queryTypeIndex);
                        }
                        else if (archetypeType.Value > currentQueryType.Value)
                        {
                            break;
                        }
                    }
                    if (missingRequired)
                        continue;
                }
                // Next, any types.
                if (query.withAnyTypes.Length > 0)
                {
                    bool found            = false;
                    int  queryTypeIndex   = 0;
                    var  currentQueryType = query.withAnyTypes.GetTypeIndex(0);
                    for (int i = 0; i < archetype.TypesCount; i++)
                    {
                        var archetypeType = archetype.GetTypeAtIndex(i);
                        if (archetypeType.Value == currentQueryType.Value)
                        {
                            found = true;
                            break;
                        }
                        while (archetypeType.Value > currentQueryType.Value)
                        {
                            queryTypeIndex++;
                            if (queryTypeIndex >= archetype.TypesCount)
                            {
                                break;
                            }
                            currentQueryType = query.withAnyTypes.GetTypeIndex(queryTypeIndex);
                        }
                    }
                    if (!found)
                        continue;
                }
                // Finally, without types.
                if (query.withoutTypes.Length > 0)
                {
                    bool found            = false;
                    int  queryTypeIndex   = 0;
                    var  currentQueryType = query.withoutTypes.GetTypeIndex(0);
                    for (int i = 0; i < archetype.TypesCount; i++)
                    {
                        var archetypeType = archetype.GetTypeAtIndex(i);
                        if (archetypeType.Value == currentQueryType.Value)
                        {
                            found = true;
                            break;
                        }
                        while (archetypeType.Value > currentQueryType.Value)
                        {
                            queryTypeIndex++;
                            if (queryTypeIndex >= archetype.TypesCount)
                            {
                                break;
                            }
                            currentQueryType = query.withoutTypes.GetTypeIndex(queryTypeIndex);
                        }
                    }
                    if (found)
                        continue;
                }

                return true;
            }
        }
    }

    public interface ITempChunkEnumerator
    {
        EntityStorageInfoLookup entityStorageInfoLookup { get; }
        ArchetypeChunk Current { get; }
        public bool MoveNext();
    }

    public struct TempChunkEnumerator<TArchetypeEnumerator> : ITempChunkEnumerator where TArchetypeEnumerator : unmanaged, ITempArchetypeEnumerator
    {
        internal TArchetypeEnumerator archetypeEnumerator;
        internal int currentChunkIndexInArchetype;

        public TempMaskedChunkEnumerator<TempChunkEnumerator<TArchetypeEnumerator>> masked => new TempMaskedChunkEnumerator<TempChunkEnumerator<TArchetypeEnumerator>>(this);

        public TempChunkEnumerator(TArchetypeEnumerator archetypes)
        {
            archetypeEnumerator = archetypes;
            currentChunkIndexInArchetype = -1;
        }

        public TempChunkEnumerator<TArchetypeEnumerator> GetEnumerator() => this;

        public EntityStorageInfoLookup entityStorageInfoLookup => archetypeEnumerator.entityStorageInfoLookup;

        public ArchetypeChunk Current
        {
            get
            {
                TempQuery.CheckValid(entityStorageInfoLookup);
                return archetypeEnumerator.Current.GetChunkAtIndex(currentChunkIndexInArchetype);
            }
        }

        public bool MoveNext()
        {
            TempQuery.CheckValid(entityStorageInfoLookup);
            currentChunkIndexInArchetype++;
            while (currentChunkIndexInArchetype >= archetypeEnumerator.Current.ChunkCount)
            {
                currentChunkIndexInArchetype = 0;
                if (!archetypeEnumerator.MoveNext())
                {
                    return false;
                }
            }
            return true;
        }
    }

    public struct MaskedChunk
    {
        public ArchetypeChunk chunk;
        public v128 enabledMask;
        public bool useEnabledMask;
    }

    public interface ITempMaskedChunkEnumerator
    {
        EntityStorageInfoLookup entityStorageInfoLookup { get; }
        MaskedChunk Current { get; }
        bool MoveNext();
    }

    public struct TempMaskedChunkEnumerator<TTempChunkEnumerator> : ITempMaskedChunkEnumerator where TTempChunkEnumerator : unmanaged, ITempChunkEnumerator
    {
        internal TTempChunkEnumerator tempChunkEnumerator;
        internal MaskedChunk currentMaskedChunk;

        public TempEntityEnumerator<TempMaskedChunkEnumerator<TTempChunkEnumerator>> entities => new TempEntityEnumerator<TempMaskedChunkEnumerator<TTempChunkEnumerator>>(this);

        public TempMaskedChunkEnumerator(TTempChunkEnumerator chunks)
        {
            tempChunkEnumerator = chunks;
            currentMaskedChunk = default;
        }

        public TempMaskedChunkEnumerator<TTempChunkEnumerator> GetEnumerator() => this;

        public EntityStorageInfoLookup entityStorageInfoLookup => tempChunkEnumerator.entityStorageInfoLookup;

        public MaskedChunk Current => currentMaskedChunk;
        public bool MoveNext()
        {
            if (tempChunkEnumerator.MoveNext())
            {
                currentMaskedChunk = new MaskedChunk { chunk = tempChunkEnumerator.Current, useEnabledMask = false };
                return true;
            }
            return false;
        }
    }

    public struct TempEntityEnumerator<TTempMaskedChunkEnumerator> where TTempMaskedChunkEnumerator : unmanaged, ITempMaskedChunkEnumerator
    {
        internal TTempMaskedChunkEnumerator chunkEnumerator;
        internal ChunkEntityEnumerator indexEnumerator;
        internal NativeArray<Entity> entities;
        internal int currentIndex;

        public TempEntityEnumerator(TTempMaskedChunkEnumerator maskedChunks)
        {
            chunkEnumerator = maskedChunks;
            indexEnumerator = default;
            entities = default;
            currentIndex = -1;
        }

        public TempEntityEnumerator<TTempMaskedChunkEnumerator> GetEnumerator() => this;

        public Entity Current => entities[currentIndex];
        public bool MoveNext()
        {
            // Note: A default instance will return false here.
            if (!indexEnumerator.NextEntityIndex(out currentIndex))
            {
                if (chunkEnumerator.MoveNext())
                {
                    var chunk = chunkEnumerator.Current;
                    entities = chunk.chunk.GetNativeArray(chunkEnumerator.entityStorageInfoLookup.AsEntityTypeHandle());
                    indexEnumerator = new ChunkEntityEnumerator(chunk.useEnabledMask, chunk.enabledMask, chunk.chunk.Count);
                    indexEnumerator.NextEntityIndex(out currentIndex);
                    return true;
                }
                return false;
            }
            return true;
        }
    }
}

