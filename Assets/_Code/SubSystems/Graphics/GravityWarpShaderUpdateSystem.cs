using Latios;
using Latios.Psyshock;
using Latios.Transforms;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.Rendering;

namespace Lsss
{
    // Todo: Make this ISystem
    public partial class GravityWarpShaderUpdateSystem : SubSystem
    {
        EntityQuery m_warpZoneQuery;
        EntityQuery m_warpedQuery;

        protected unsafe override void OnUpdate()
        {
            var settings       = sceneBlackboardEntity.GetComponentData<ArenaCollisionSettings>().settings;
            var warpZoneBodies = new NativeArray<ColliderBody>(m_warpZoneQuery.CalculateEntityCount(), Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

            Entities.WithAll<GravityWarpZoneTag>().WithStoreEntityQueryInField(ref m_warpZoneQuery).
            ForEach((Entity entity, int entityInQueryIndex, in WorldTransform worldTransform, in GravityWarpZoneRadius radius) =>
            {
                warpZoneBodies[entityInQueryIndex] = new ColliderBody
                {
                    collider  = new SphereCollider(0f, radius.radius),
                    transform = worldTransform.worldTransform,
                    entity    = entity
                };
            }).ScheduleParallel();

            Dependency = Physics.BuildCollisionLayer(warpZoneBodies).WithSettings(settings).ScheduleParallel(out CollisionLayer warpZoneLayer, Allocator.TempJob, Dependency);

            int warpedCount  = m_warpedQuery.CalculateEntityCount();
            var warpedBodies = new NativeArray<ColliderBody>(warpedCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            var warpedAabbs  = new NativeArray<Aabb>(warpedCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

            Entities.ForEach((ref GravityWarpZoneParamsProperty paramsProp) => { paramsProp = default; }).ScheduleParallel();

            Entities.WithAll<GravityWarpZoneParamsProperty>().ForEach((Entity entity, int entityInQueryIndex, ref RenderBounds renderBounds, in BackupRenderBounds backupBounds,
                                                                       in WorldTransform worldTransform) =>
            {
                renderBounds = backupBounds.bounds;

                warpedAabbs[entityInQueryIndex] = Physics.TransformAabb(worldTransform.worldTransform, new Aabb(renderBounds.Value.Min, renderBounds.Value.Max));

                warpedBodies[entityInQueryIndex] = new ColliderBody
                {
                    collider  = new Collider(),
                    transform = worldTransform.worldTransform,
                    entity    = entity
                };
            }).WithStoreEntityQueryInField(ref m_warpedQuery).ScheduleParallel();

            Dependency = Physics.BuildCollisionLayer(warpedBodies, warpedAabbs)
                         .WithSettings(settings)
                         .ScheduleParallel(out CollisionLayer warpedLayer, Allocator.TempJob, Dependency);

            ApplyWarpZoneDataProcessor processor = new ApplyWarpZoneDataProcessor
            {
                boundsLookup         = GetComponentLookup<RenderBounds>(),
                positionRadiusLookup = GetComponentLookup<GravityWarpZonePositionRadiusProperty>(),
                paramsLookup         = GetComponentLookup<GravityWarpZoneParamsProperty>(),
                warpZoneLookup       = GetComponentLookup<GravityWarpZone>()
            };

            Dependency       = Physics.FindPairs(warpZoneLayer, warpedLayer, processor).ScheduleParallel(Dependency);
            var finalHandles = stackalloc[]
            {
                warpZoneBodies.Dispose(Dependency),
                warpZoneLayer.Dispose(Dependency),
                warpedBodies.Dispose(Dependency),
                warpedAabbs.Dispose(Dependency),
                warpedLayer.Dispose(Dependency)
            };

            Dependency = JobHandleUnsafeUtility.CombineDependencies(finalHandles, 5);
        }

        //Assumes warpZone is A
        struct ApplyWarpZoneDataProcessor : IFindPairsProcessor
        {
            public PhysicsComponentLookup<RenderBounds>                          boundsLookup;
            public PhysicsComponentLookup<GravityWarpZonePositionRadiusProperty> positionRadiusLookup;
            public PhysicsComponentLookup<GravityWarpZoneParamsProperty>         paramsLookup;
            [ReadOnly] public ComponentLookup<GravityWarpZone>                   warpZoneLookup;

            public void Execute(in FindPairsResult result)
            {
                SphereCollider sphere = result.bodyA.collider;
                var            bounds = boundsLookup[result.entityB];

                boundsLookup[result.entityB] = new RenderBounds {
                    Value                    = new AABB {
                        Center               = sphere.center,
                        Extents              = sphere.radius + math.abs(bounds.Value.Center) + bounds.Value.Extents
                    }
                };

                var warpZone                 = warpZoneLookup[result.entityA];
                paramsLookup[result.entityB] = new GravityWarpZoneParamsProperty
                {
                    active             = 1f,
                    swarchschildRadius = warpZone.swarchschildRadius,
                    maxW               = warpZone.maxW,
                };

                positionRadiusLookup[result.entityB] = new GravityWarpZonePositionRadiusProperty { position = result.transformA.position, radius = sphere.radius };
            }
        }
    }
}

