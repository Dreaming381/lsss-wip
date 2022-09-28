using System.Collections.Generic;
using System.Text;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Latios.Kinemation.Authoring.Systems
{
    [UpdateInGroup(typeof(GameObjectBeforeConversionGroup))]
    [UpdateAfter(typeof(DiscoverSkeletonsConversionSystem))]
    [ConverterVersion("Latios", 1)]
    [DisableAutoCreation]
    public partial class DiscoverUnboundSkinnedMeshesConversionSystem : GameObjectConversionSystem
    {
        protected override void OnUpdate()
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            Entities.WithNone<SkinnedMeshConversionContext>().ForEach((Entity entity, SkinnedMeshSettingsAuthoring authoring, SkinnedMeshRenderer renderer) =>
            {
                if (authoring.bindingMode == BindingMode.ConversionTime)
                {
                    Debug.LogError($"Skinned Mesh {renderer.gameObject.name} does not have a skeleton. Skipping.");
                }
                else if (authoring.bindingMode == BindingMode.Import)
                {
                    if (authoring.m_importBonePathsReversed == null)
                    {
                        Debug.LogError($"Skinned Mesh {renderer.gameObject.name} is trying to use skeleton BindingMode.Import, but this feature hasn't been implemented yet.");
                        return;
                    }
                    var meshContext = new SkinnedMeshConversionContext
                    {
                        authoring         = authoring,
                        bonePathsReversed = authoring.m_importBonePathsReversed,
                        renderer          = renderer,
                        skeletonContext   = null
                    };
                    ecb.AddComponent(entity, meshContext);
                }
                else if (authoring.bindingMode == BindingMode.Custom)
                {
                    if (authoring.customBonePathsReversed == null)
                    {
                        Debug.LogError($"{renderer.gameObject.name} is trying to use skinned mesh BindingMode.Custom, but no custom skeleton data was provided.");
                        return;
                    }
                    var meshContext = new SkinnedMeshConversionContext
                    {
                        authoring         = authoring,
                        bonePathsReversed = authoring.customBonePathsReversed.ToArray(),
                        renderer          = renderer,
                        skeletonContext   = null
                    };
                    ecb.AddComponent(entity, meshContext);
                }
            }).WithoutBurst().Run();

            ecb.Playback(EntityManager);
        }
    }
}

