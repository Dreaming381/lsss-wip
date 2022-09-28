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
    /*[UpdateInGroup(typeof(GameObjectAfterConversionGroup))]
       public partial class SpeedShaderConversionSystem : GameObjectConversionSystem
       {
        protected override void OnUpdate()
        {
            var system = DstEntityManager.World.GetOrCreateSystemManaged<SpeedShaderDestinationWorldConversionSystem>();
            system.Update();
            DstEntityManager.World.DestroySystem(system.SystemHandle);
        }

        partial class SpeedShaderDestinationWorldConversionSystem : SystemBase
        {
            protected override void OnUpdate()
            {
                var renderMeshes = new List<RenderMesh>();
                EntityManager.GetAllUniqueSharedComponentsManaged(renderMeshes);
                EntityQuery query = EntityManager.Fluent().WithAll<RenderMesh>(true).Build();

                foreach (var renderMesh in renderMeshes)
                {
                    bool needsSpeedProperty           = renderMesh.material.HasProperty("_Speed");
                    bool needsIntegratedSpeedProperty = renderMesh.material.HasProperty("_IntegratedSpeed");
                    if (!needsSpeedProperty && !needsIntegratedSpeedProperty)
                        continue;

                    query.SetSharedComponentFilter(renderMesh);
                    EntityManager.AddComponent<SpeedEntity>(query);

                    NativeList<Entity> foundSpeedEntities = new NativeList<Entity>(Allocator.TempJob);
                    NativeList<Entity> badEntities        = new NativeList<Entity>(Allocator.TempJob);
                    Entities.WithSharedComponentFilter(renderMesh).ForEach((Entity entity, ref SpeedEntity speedEntity) =>
                    {
                        if (HasComponent<Speed>(entity))
                        {
                            speedEntity.entityWithSpeed = entity;
                            foundSpeedEntities.Add(entity);
                            return;
                        }
                        Entity activeEntity = entity;
                        while (HasComponent<Parent>(activeEntity))
                        {
                            activeEntity = GetComponent<Parent>(activeEntity).Value;
                            if (HasComponent<Speed>(activeEntity))
                            {
                                speedEntity.entityWithSpeed = activeEntity;
                                foundSpeedEntities.Add(activeEntity);
                                return;
                            }
                        }
                        badEntities.Add(entity);
                    }).Run();

                    if (needsSpeedProperty)
                        EntityManager.AddComponent<SpeedProperty>(query);
                    if (needsIntegratedSpeedProperty)
                    {
                        EntityManager.AddComponent<IntegratedSpeedProperty>(query);
                        EntityManager.AddComponent<IntegratedSpeed>(        foundSpeedEntities);
                    }
                    EntityManager.RemoveComponent<SpeedProperty>(          badEntities);
                    EntityManager.RemoveComponent<IntegratedSpeedProperty>(badEntities);

                    foundSpeedEntities.Dispose();
                    badEntities.Dispose();
                }

                query.Dispose();
            }
        }
       }*/
}

