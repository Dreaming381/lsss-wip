using System.Collections.Generic;
using Latios;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;

namespace Lsss.Authoring
{
    [UpdateInGroup(typeof(GameObjectAfterConversionGroup))]
    public class GravityWarpConversionSystem : GameObjectConversionSystem
    {
        protected override void OnUpdate()
        {
            var system = DstEntityManager.World.GetOrCreateSystem<GravityWarpDestinationWorldConversionSystem>();
            system.Update();
            DstEntityManager.World.DestroySystem(system);
        }

        class GravityWarpDestinationWorldConversionSystem : SystemBase
        {
            protected override void OnUpdate()
            {
                var renderMeshes = new List<RenderMesh>();
                EntityManager.GetAllUniqueSharedComponentData(renderMeshes);
                EntityQuery query = EntityManager.Fluent().WithAll<RenderMesh>(true).Build();

                foreach (var renderMesh in renderMeshes)
                {
                    if (!renderMesh.material.HasProperty("_WarpParams"))
                        continue;

                    query.SetSharedComponentFilter(renderMesh);

                    EntityManager.AddComponent<GravityWarpZonePositionRadiusProperty>(query);
                    EntityManager.AddComponent<GravityWarpZoneParamsProperty>(        query);
                    EntityManager.AddComponent<BackupRenderBounds>(                   query);
                    EntityManager.AddComponent<GravityWarpZoneTag>(                   query);

                    Entities.WithSharedComponentFilter(renderMesh).ForEach((ref BackupRenderBounds backup, in RenderBounds bounds) =>
                    {
                        backup.bounds = bounds;
                    }).Run();
                }
            }
        }
    }
}

