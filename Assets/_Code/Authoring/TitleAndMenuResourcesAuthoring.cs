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
        public GameObject blipSoundEffect;

        public void DeclareReferencedPrefabs(List<GameObject> referencedPrefabs)
        {
            referencedPrefabs.Add(blipSoundEffect);
        }

        public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
        {
            var blipEntity = conversionSystem.GetPrimaryEntity(blipSoundEffect);
            dstManager.AddComponent<Latios.DontDestroyOnSceneChangeTag>(blipEntity);
            dstManager.AddComponentData(entity, new TitleAndMenuResources { blipSoundEffect = blipEntity});
        }
    }
}

