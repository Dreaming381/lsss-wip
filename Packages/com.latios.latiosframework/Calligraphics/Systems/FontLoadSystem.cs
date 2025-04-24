using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace Latios.Calligraphics.Systems
{
    [DisableAutoCreation]
    [BurstCompile]
    public partial struct FontLoadSystem : ISystem
    {
        LatiosWorldUnmanaged latiosWorld;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            latiosWorld = state.GetLatiosWorldUnmanaged();

            state.Fluent().With<CalliByte, CalliByteChangedFlag, TextBaseConfiguration>(true).Build();

            var fontTable = new FontTable
            {
                faceEntries         = new NativeList<FontTable.FaceEntry>(32, Allocator.Persistent),
                perThreadFontCaches = new NativeArray<UnsafeList<IntPtr> >(Unity.Jobs.LowLevel.Unsafe.JobsUtility.ThreadIndexCount,
                                                                           Allocator.Persistent,
                                                                           NativeArrayOptions.UninitializedMemory)
            };
            for (int i = 0; i < fontTable.perThreadFontCaches.Length; i++)
            {
                fontTable.perThreadFontCaches[i] = new UnsafeList<IntPtr>(32, Allocator.Persistent);
            }
            latiosWorld.worldBlackboardEntity.AddOrSetCollectionComponentAndDisposeOld(fontTable);
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var fontTable = latiosWorld.worldBlackboardEntity.GetCollectionComponent<FontTable>(false);
        }
    }
}

