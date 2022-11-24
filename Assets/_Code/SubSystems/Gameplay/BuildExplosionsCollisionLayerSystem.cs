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
    [BurstCompile]
    public partial struct BuildExplosionsCollisionLayerSystem : ISystem, ISystemNewScene
    {
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

        public void OnNewScene(ref SystemState state) => latiosWorld.sceneBlackboardEntity.AddOrSetCollectionComponentAndDisposeOld(new ExplosionCollisionLayer());

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            CollisionLayerSettings settings;
            if (latiosWorld.sceneBlackboardEntity.HasComponent<ArenaCollisionSettings>())
                settings = latiosWorld.sceneBlackboardEntity.GetComponentData<ArenaCollisionSettings>().settings;
            else
                settings = BuildCollisionLayerConfig.defaultSettings;

            var query = QueryBuilder().WithAll<Translation, Scale, ExplosionTag>().Build();

            var bodies =
                CollectionHelper.CreateNativeArray<ColliderBody>(query.CalculateEntityCount(),
                                                                 state.WorldUnmanaged.UpdateAllocator.ToAllocator,
                                                                 NativeArrayOptions.UninitializedMemory);

            new Job { bodies = bodies }.ScheduleParallel(query);

            state.Dependency   = Physics.BuildCollisionLayer(bodies).WithSettings(settings).ScheduleParallel(out CollisionLayer layer, Allocator.Persistent, state.Dependency);
            var explosionLayer = new ExplosionCollisionLayer { layer = layer };
            latiosWorld.sceneBlackboardEntity.SetCollectionComponentAndDisposeOld(explosionLayer);
        }

        [BurstCompile]
        partial struct Job : IJobEntity
        {
            public NativeArray<ColliderBody> bodies;

            public void Execute(Entity entity,
                                [EntityIndexInQuery] int entityInQueryIndex,
                                in Translation translation,
                                in Scale scale)
            {
                var sphere                 = new SphereCollider(0f, scale.Value / 2f);  //convert diameter to radius
                bodies[entityInQueryIndex] = new ColliderBody
                {
                    entity    = entity,
                    transform = new RigidTransform(quaternion.identity, translation.Value),
                    collider  = sphere
                };
            }
        }
    }
}

