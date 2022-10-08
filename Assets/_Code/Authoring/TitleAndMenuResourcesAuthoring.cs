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
            var selectEntity   = GetEntity(  authoring.selectSoundEffect);
            var navigateEntity = GetEntity(authoring.navigateSoundEffect);
            AddComponent<Latios.DontDestroyOnSceneChangeTag>(selectEntity);
            AddComponent(                                    new TitleAndMenuResources
            {
                selectSoundEffect   = selectEntity,
                navigateSoundEffect = navigateEntity
            });
        }
    }
}

