using Latios;
using Latios.Psyshock;
using Latios.Transforms;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace Lsss
{
    [BurstCompile]
    public partial struct BuildBulletsCollisionLayerSystem : ISystem, ILatiosApi, ISystemNewScene
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            this.OnCreateForLatios(ref state);
        }

        public void OnNewScene(ref SystemState state) => this.GetApi(ref state).sceneBlackboardEntity.AddOrSetCollectionComponentAndDisposeOld(new BulletCollisionLayer());

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var                    api = this.GetApi(ref state);
            CollisionLayerSettings settings;
            if (api.sceneBlackboardEntity.HasComponent<ArenaCollisionSettings>())
                settings = api.sceneBlackboardEntity.GetComponentData<ArenaCollisionSettings>().settings;
            else
                settings = BuildCollisionLayerConfig.defaultSettings;

            var query  = api.GetDefaultQuery<Job>();
            var count  = query.CalculateEntityCount();
            var bodies = CollectionHelper.CreateNativeArray<ColliderBody>(count, state.WorldUpdateAllocator, NativeArrayOptions.UninitializedMemory);
            var aabbs  = CollectionHelper.CreateNativeArray<Aabb>(count, state.WorldUpdateAllocator, NativeArrayOptions.UninitializedMemory);

            new Job { bodies = bodies, aabbs = aabbs, dt = api.deltaTime }.ScheduleParallel(api);

            state.Dependency = Physics.BuildCollisionLayer(bodies, aabbs).WithSettings(settings).ScheduleParallel(out CollisionLayer layer, Allocator.Persistent, state.Dependency);
            var bcl          = new BulletCollisionLayer { layer = layer };
            api.sceneBlackboardEntity.SetCollectionComponentAndDisposeOld(bcl);
        }

        [With(typeof(BulletTag))]
        [BurstCompile]
        [RequireEntityIndexInQuery]
        partial struct Job : IJobEach
        {
            [NativeDisableParallelForRestriction] public NativeArray<ColliderBody> bodies;
            [NativeDisableParallelForRestriction] public NativeArray<Aabb>         aabbs;
            public float                                                           dt;

            public void Execute(Entity entity,
                                in IJobEach.JobContext context,
                                in WorldTransform worldTransform,
                                in BulletCollider collider,
                                //in PreviousTransform previousPosition)
                                in Speed speed)
            {
                var             pointB  = new float3(0f, 0f, collider.headOffsetZ);
                CapsuleCollider capsule = new CapsuleCollider(pointB, pointB, collider.radius);
                // Recalculating from Speed results in less memory bandwidth, and makes this job measurably faster.
                //float           tailLength  = math.distance(worldTransform.position, previousPosition.position);
                float tailLength  = dt * speed.speed;
                capsule.pointA.z -= math.max(tailLength, math.EPSILON);

                bodies[context.entityIndexInQuery] = new ColliderBody
                {
                    collider  = capsule,
                    entity    = entity,
                    transform = worldTransform.worldTransform
                };
                aabbs[context.entityIndexInQuery] = Physics.AabbFrom(capsule, worldTransform.worldTransform);
            }
        }
    }

    public partial class DebugDrawBulletCollisionLayersSystem : SubSystem
    {
        protected override void OnUpdate()
        {
            var layer = sceneBlackboardEntity.GetCollectionComponent<BulletCollisionLayer>(true).layer;
            CompleteDependency();
            PhysicsDebug.DrawLayer(layer).Run();
            //UnityEngine.Debug.Log("Bullets in layer: " + layer.count);
        }
    }
}

