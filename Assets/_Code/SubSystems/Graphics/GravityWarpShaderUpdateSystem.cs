using Latios;
using Latios.PhysicsEngine;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;

namespace Lsss
{
    public class GravityWarpShaderUpdateSystem : SubSystem
    {
        EntityQuery m_warpZoneQuery;
        EntityQuery m_warpedQuery;

        protected unsafe override void OnUpdate()
        {
            var warpZoneBodies = new NativeArray<ColliderBody>(m_warpZoneQuery.CalculateEntityCount(), Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

            Entities.WithAll<GravityWarpZoneTag>().WithStoreEntityQueryInField(ref m_warpZoneQuery).
            ForEach((Entity entity, int entityInQueryIndex, in LocalToWorld ltw, in GravityWarpZoneRadius radius) =>
            {
                warpZoneBodies[entityInQueryIndex] = new ColliderBody
                {
                    collider  = new SphereCollider(0f, radius.radius),
                    transform = new RigidTransform(quaternion.LookRotationSafe(ltw.Forward, ltw.Up), ltw.Position),
                    entity    = entity
                };
            }).ScheduleParallel();

            Dependency = Physics.BuildCollisionLayer(warpZoneBodies).ScheduleParallel(out CollisionLayer warpZoneLayer, Allocator.TempJob, Dependency);

            int warpedCount  = m_warpedQuery.CalculateEntityCount();
            var warpedBodies = new NativeArray<ColliderBody>(warpedCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            var warpedAabbs  = new NativeArray<Aabb>(warpedCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

            Entities.ForEach((ref GravityWarpZoneParamsProperty paramsProp) => { paramsProp = default; }).ScheduleParallel();

            Entities.WithAll<GravityWarpZoneParamsProperty>().ForEach((Entity entity, int entityInQueryIndex, ref RenderBounds renderBounds, in BackupRenderBounds backupBounds,
                                                                       in LocalToWorld ltw) =>
            {
                renderBounds = backupBounds.bounds;

                warpedAabbs[entityInQueryIndex] = Physics.TransformAabb(ltw.Value, renderBounds.Value.Center, renderBounds.Value.Extents);

                warpedBodies[entityInQueryIndex] = new ColliderBody
                {
                    collider  = new Collider(),
                    transform = new RigidTransform(quaternion.LookRotationSafe(ltw.Forward, ltw.Up), ltw.Position),
                    entity    = entity
                };
            }).ScheduleParallel();

            Dependency = Physics.BuildCollisionLayer(warpedBodies, warpedAabbs).ScheduleParallel(out CollisionLayer warpedLayer, Allocator.TempJob, Dependency);

            ApplyWarpZoneDataProcessor processor = new ApplyWarpZoneDataProcessor
            {
                boundsCdfe         = this.GetPhysicsComponentDataFromEntity<RenderBounds>(),
                positionRadiusCdfe = this.GetPhysicsComponentDataFromEntity<GravityWarpZonePositionRadiusProperty>(),
                paramsCdfe         = this.GetPhysicsComponentDataFromEntity<GravityWarpZoneParamsProperty>(),
                warpZoneCdfe       = GetComponentDataFromEntity<GravityWarpZone>()
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
            public PhysicsComponentDataFromEntity<RenderBounds>                          boundsCdfe;
            public PhysicsComponentDataFromEntity<GravityWarpZonePositionRadiusProperty> positionRadiusCdfe;
            public PhysicsComponentDataFromEntity<GravityWarpZoneParamsProperty>         paramsCdfe;
            [ReadOnly] public ComponentDataFromEntity<GravityWarpZone>                   warpZoneCdfe;

            public void Execute(FindPairsResult result)
            {
                SphereCollider sphere = result.bodyA.collider;
                var            bounds = boundsCdfe[result.entityB];

                boundsCdfe[result.entityB] = new RenderBounds {
                    Value                  = new AABB {
                        Center             = sphere.center,
                        Extents            = sphere.radius + math.abs(bounds.Value.Center) + bounds.Value.Extents
                    }
                };

                var warpZone               = warpZoneCdfe[result.entityA];
                paramsCdfe[result.entityB] = new GravityWarpZoneParamsProperty
                {
                    active             = 1f,
                    swarchschildRadius = warpZone.swarchschildRadius,
                    maxW               = warpZone.maxW,
                };

                positionRadiusCdfe[result.entityB] = new GravityWarpZonePositionRadiusProperty { position = result.bodyA.transform.pos, radius = sphere.radius };
            }
        }
    }
}

