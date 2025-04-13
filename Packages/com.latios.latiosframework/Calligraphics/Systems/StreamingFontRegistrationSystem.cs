using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

using static Unity.Entities.SystemAPI;

namespace Latios.Calligraphics.Systems
{
    [RequireMatchingQueriesForUpdate]
    [DisableAutoCreation]
    [BurstCompile]
    public partial struct StreamingFontRegistrationSystem : ISystem
    {
        LatiosWorldUnmanaged                                   latiosWorld;
        EntityQuery                                            m_query;
        NativeHashSet<BlobAssetReference<FontCollectionBlob> > blobsAlreadyProcessed;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            latiosWorld = state.GetLatiosWorldUnmanaged();
            m_query     = state.Fluent().With<FontCollectionBlobReference>(true).IncludePrefabs().IncludeDisabledEntities().Build();
            m_query.AddChangedVersionFilter(ComponentType.ReadOnly<FontCollectionBlobReference>());
            blobsAlreadyProcessed = new NativeHashSet<BlobAssetReference<FontCollectionBlob> >(16, Allocator.Persistent);
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state) => blobsAlreadyProcessed.Dispose();

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.Dependency = new Job
            {
                fontsHandle           = GetComponentTypeHandle<FontCollectionBlobReference>(true),
                blobsAlreadyProcessed = blobsAlreadyProcessed,
                fontTable             = latiosWorld.worldBlackboardEntity.GetCollectionComponent<FontTable>(false)
            }.Schedule(m_query, state.Dependency);
        }

        [BurstCompile]
        struct Job : IJobChunk
        {
            [ReadOnly] public ComponentTypeHandle<FontCollectionBlobReference> fontsHandle;

            public NativeHashSet<BlobAssetReference<FontCollectionBlob> > blobsAlreadyProcessed;
            public FontTable                                              fontTable;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                var blobs = chunk.GetNativeArray(ref fontsHandle);
                foreach (var fontCollection in blobs)
                {
                    if (blobsAlreadyProcessed.Contains(fontCollection.blob))
                        continue;

                    // Todo: Add all fonts in blob to table.
                }
            }
        }
    }
}

