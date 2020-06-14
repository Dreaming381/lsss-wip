using Latios;
using Latios.PhysicsEngine;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine.Profiling;

namespace Lsss
{
    public class CheckSpawnPointIsSafeSystem : SubSystem
    {
        protected override void OnUpdate()
        {
            Profiler.BeginSample("CheckSpawnPointIsSafe_OnUpdate");

            Profiler.BeginSample("Clear safe");
            Entities.WithAll<SpawnPoint>().ForEach((ref SafeToSpawn safeToSpawn) =>
            {
                safeToSpawn.safe = true;
            }).ScheduleParallel();
            Profiler.EndSample();

            var processor = new SpawnPointIsNotSafeProcessor
            {
                safeToSpawnCdfe = this.GetPhysicsComponentDataFromEntity<SafeToSpawn>()
            };

            var closeProcessor = new SpawnPointsAreTooCloseProcessor
            {
                safeToSpawnCdfe = processor.safeToSpawnCdfe
            };

            Profiler.BeginSample("Fetch");
            var spawnLayer = sceneGlobalEntity.GetCollectionComponent<SpawnPointCollisionLayer>(true).layer;
            Profiler.EndSample();
            Profiler.BeginSample("Schedule");
            Dependency = Physics.FindPairs(spawnLayer, closeProcessor).ScheduleParallel(Dependency);
            Profiler.EndSample();

            Profiler.BeginSample("Fetch");
            var wallLayer = sceneGlobalEntity.GetCollectionComponent<WallCollisionLayer>(true).layer;
            Profiler.EndSample();
            Profiler.BeginSample("Schedule");
            Dependency = Physics.FindPairs(spawnLayer, wallLayer, processor).ScheduleParallel(Dependency);
            Profiler.EndSample();

            Profiler.BeginSample("Fetch");
            var bulletLayer = sceneGlobalEntity.GetCollectionComponent<BulletCollisionLayer>(true).layer;
            Profiler.EndSample();
            Profiler.BeginSample("Schedule");
            Dependency = Physics.FindPairs(spawnLayer, bulletLayer, processor).ScheduleParallel(Dependency);
            Profiler.EndSample();

            Profiler.BeginSample("Fetch");
            var explosionLayer = sceneGlobalEntity.GetCollectionComponent<ExplosionCollisionLayer>(true).layer;
            Profiler.EndSample();
            Profiler.BeginSample("Schedule");
            Dependency = Physics.FindPairs(spawnLayer, explosionLayer, processor).ScheduleParallel(Dependency);
            Profiler.EndSample();

            Profiler.BeginSample("Fetch");
            var wormholeLayer = sceneGlobalEntity.GetCollectionComponent<WormholeCollisionLayer>(true).layer;
            Profiler.EndSample();
            Profiler.BeginSample("Schedule");
            Dependency = Physics.FindPairs(spawnLayer, wormholeLayer, processor).ScheduleParallel(Dependency);
            Profiler.EndSample();

            //Todo: Remove hack
            //This hack exists because the .Run forces Dependency to complete first. But we don't want that.
            var backupDependency = Dependency;
            Dependency           = default;

            Entities.WithAll<FactionTag>().ForEach((Entity entity, int entityInQueryIndex) =>
            {
                if (entityInQueryIndex == 0)
                    Dependency = backupDependency;

                Profiler.BeginSample("Fetch2");
                var shipLayer = EntityManager.GetCollectionComponent<FactionShipsCollisionLayer>(entity, true).layer;
                Profiler.EndSample();
                Profiler.BeginSample("Schedule2");
                Dependency = Physics.FindPairs(spawnLayer, shipLayer, processor).ScheduleParallel(Dependency);
                Profiler.EndSample();
            }).WithoutBurst().Run();

            Profiler.EndSample();
        }

        //Assumes A is SpawnPoint
        struct SpawnPointIsNotSafeProcessor : IFindPairsProcessor
        {
            public PhysicsComponentDataFromEntity<SafeToSpawn> safeToSpawnCdfe;

            public void Execute(FindPairsResult result)
            {
                //No need to check narrow phase. AABB check is good enough
                safeToSpawnCdfe[result.entityA] = new SafeToSpawn { safe = false };
            }
        }

        struct SpawnPointsAreTooCloseProcessor : IFindPairsProcessor
        {
            public PhysicsComponentDataFromEntity<SafeToSpawn> safeToSpawnCdfe;

            public void Execute(FindPairsResult result)
            {
                safeToSpawnCdfe[result.entityA] = new SafeToSpawn { safe = false };
                safeToSpawnCdfe[result.entityB]                          = new SafeToSpawn { safe = false };
            }
        }
    }
}

