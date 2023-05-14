using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Lsss.Authoring
{
    [DisallowMultipleComponent]
    [AddComponentMenu("LSSS/UI/Title And Menu Resources")]
    public class TitleAndMenuResourcesAuthoring : MonoBehaviour
    {
        public GameObject selectSoundEffect;
        public GameObject navigateSoundEffect;
    }

    public class TitleAndMenuResourcesBaker : Baker<TitleAndMenuResourcesAuthoring>
    {
        public override void Bake(TitleAndMenuResourcesAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            DependsOn(authoring.selectSoundEffect);
            DependsOn(authoring.navigateSoundEffect);

            var selectEntity   = GetEntity(  authoring.selectSoundEffect, TransformUsageFlags.Dynamic);
            var navigateEntity = GetEntity(authoring.navigateSoundEffect, TransformUsageFlags.Dynamic);
            AddComponent(entity, new TitleAndMenuResources
            {
                selectSoundEffect   = selectEntity,
                navigateSoundEffect = navigateEntity
            });
        }
    }
}

