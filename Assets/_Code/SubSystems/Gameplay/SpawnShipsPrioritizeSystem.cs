using Latios;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.UniversalDelegates;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace Lsss
{
    public class SpawnShipsPrioritizeSystem : SubSystem
    {
        protected override void OnUpdate()
        {
            var spawnQueues = sceneBlackboardEntity.GetCollectionComponent<SpawnQueues>(false);

            Job.WithCode(() =>
            {
                var runningWeights = new NativeArray<float>(spawnQueues.factionRanges.Length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                for (int i = 0; i < runningWeights.Length; i++)
                {
                    runningWeights[i] = spawnQueues.factionRanges[i].weight;
                }

                for (int spawnsRemaining = spawnQueues.newAiEntitiesToPrioritize.Length; spawnsRemaining > 0; spawnsRemaining--)
                {
                    int   targetFaction = -1;
                    float bestWeight    = float.MaxValue;
                    for (int i = 0; i < runningWeights.Length; i++)
                    {
                        bool isBetter = runningWeights[i] < bestWeight && spawnQueues.factionRanges[i].count > 0;
                        bestWeight    = math.select(bestWeight, runningWeights[i], isBetter);
                        targetFaction = math.select(targetFaction, i, isBetter);
                    }
                    var factionRange = spawnQueues.factionRanges[targetFaction];
                    spawnQueues.aiQueue.Enqueue(spawnQueues.newAiEntitiesToPrioritize[factionRange.start]);
                    factionRange.start++;
                    factionRange.count--;
                    runningWeights[targetFaction]            += factionRange.weight;
                    spawnQueues.factionRanges[targetFaction]  = factionRange;
                }

                spawnQueues.newAiEntitiesToPrioritize.Clear();
                spawnQueues.factionRanges.Clear();
            }).Schedule();
        }
    }
}

