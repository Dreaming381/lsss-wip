using System.Diagnostics;
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

    public struct TempArchetypeEnumerator
    {
        internal TempQuery query;
        internal int       currentIndex;

        public TempArchetypeEnumerator GetEnumerator() => this;

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
}

