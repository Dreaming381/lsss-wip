using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Lsss.Authoring
{
    [DisallowMultipleComponent]
    [AddComponentMenu("LSSS/UI/Title And Menu Resources")]
    public class TitleAndMenuResourcesAuthoring : MonoBehaviour, IDeclareReferencedPrefabs, IConvertGameObjectToEntity
    {
        public GameObject selectSoundEffect;
        public GameObject navigateSoundEffect;

        public void DeclareReferencedPrefabs(List<GameObject> referencedPrefabs)
        {
            referencedPrefabs.Add(selectSoundEffect);
            referencedPrefabs.Add(navigateSoundEffect);
        }

        public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
        {
            var selectEntity   = conversionSystem.GetPrimaryEntity(selectSoundEffect);
            var navigateEntity = conversionSystem.GetPrimaryEntity(navigateSoundEffect);
            dstManager.AddComponent<Latios.DontDestroyOnSceneChangeTag>(selectEntity);
            dstManager.AddComponentData(entity, new TitleAndMenuResources
            {
                selectSoundEffect   = selectEntity,
                navigateSoundEffect = navigateEntity
            });
        }
    }
}

