using Latios;
using Latios.Psyshock;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

using static Unity.Entities.SystemAPI;

namespace Lsss
{
    [RequireMatchingQueriesForUpdate]
    [BurstCompile]
    public partial struct OrbitalSpawnersProcGenSystem : ISystem
    {
        struct NewSpawnerTag : IComponentData { }

        LatiosWorldUnmanaged latiosWorld;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            latiosWorld = state.GetLatiosWorldUnmanaged();
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float arenaRadius = latiosWorld.sceneBlackboardEntity.GetComponentData<ArenaRadius>().radius;

            var populatorQuery  = QueryBuilder().WithAll<OrbitalSpawnPointProcGen>().Build();
            var populators      = populatorQuery.ToComponentDataArray<OrbitalSpawnPointProcGen>(Allocator.Temp);
            var newSpawnerQuery = QueryBuilder().WithAllRW<Translation, Rotation>().WithAllRW<SpawnPointOrbitalPath>().WithAll<NewSpawnerTag>().Build();
            foreach (var populator in populators)
            {
                Collider collider = new SphereCollider(0f, populator.colliderRadius);

                var entity = state.EntityManager.CreateEntity();
                state.EntityManager.AddComponent<Translation>( entity);
                state.EntityManager.AddComponent<Rotation>(    entity);
                state.EntityManager.AddComponent<LocalToWorld>(entity);
                state.EntityManager.AddComponentData(entity, collider);
                state.EntityManager.AddComponentData(entity, new SpawnPoint
                {
                    spawnGraphicPrefab = populator.spawnGraphicPrefab,
                    maxTimeUntilSpawn  = populator.maxTimeUntilSpawn,
                    maxPauseTime       = populator.maxPauseTime
                });
                state.EntityManager.AddComponentData(entity, new SpawnPayload { disabledShip = Entity.Null });
                state.EntityManager.AddComponent<SpawnPointOrbitalPath>(entity);
                state.EntityManager.AddComponent<SafeToSpawn>(          entity);
                state.EntityManager.AddComponent<SpawnTimes>(           entity);
                state.EntityManager.AddComponent<SpawnPointTag>(        entity);
                state.EntityManager.AddComponent<NewSpawnerTag>(        entity);

                state.EntityManager.Instantiate(entity, populator.spawnerCount - 1, Allocator.Temp);
                Randomize(ref state, populator, arenaRadius);
                state.EntityManager.RemoveComponent<NewSpawnerTag>(newSpawnerQuery);
            }
            state.EntityManager.DestroyEntity(populatorQuery);
        }

        void Randomize(ref SystemState state, OrbitalSpawnPointProcGen populator, float radius)
        {
            Random random = new Random(populator.randomSeed);

            foreach((var trans, var rot, var path) in Query<RefRW<Translation>, RefRW<Rotation>, RefRW<SpawnPointOrbitalPath> >().WithAll<NewSpawnerTag>())
            {
                float orbitalRadius           = random.NextFloat(0f, 1f);
                path.ValueRW.radius           = orbitalRadius * orbitalRadius * (radius - populator.colliderRadius);
                path.ValueRW.center           = random.NextFloat3Direction() * random.NextFloat(0f, radius - path.ValueRW.radius);
                path.ValueRW.orbitPlaneNormal = random.NextFloat3Direction();
                path.ValueRW.orbitSpeed       = random.NextFloat(populator.minMaxOrbitSpeed.x, populator.minMaxOrbitSpeed.y);

                rot.ValueRW.Value = quaternion.identity;

                trans.ValueRW.Value = math.forward(quaternion.AxisAngle(path.ValueRW.orbitPlaneNormal, random.NextFloat(-math.PI, math.PI))) * path.ValueRW.radius;
            }
        }
    }
}

