using System.Collections.Generic;
using Latios.Myri.Authoring;
using usfxr;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Lsss.Authoring
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSourceAuthoring))]
    [AddComponentMenu("LSSS/Behaviors/Sound Effect")]
    public class SoundEffectAuthoring : MonoBehaviour, IDeclareReferencedPrefabs
    {
        public SfxrParams effectSettings;

        public void DeclareReferencedPrefabs(List<GameObject> referencedPrefabs)
        {
            var audioSource       = GetComponent<AudioSourceAuthoring>();
            audioSource.clip      = SfxrPlayer.GetClip(effectSettings);
            audioSource.clip.name = gameObject.name;
            //DestroyImmediate(this, );
        }
    }

    [UpdateInGroup(typeof(GameObjectDeclareReferencedObjectsGroup))]
    public class RemoveUsfxrPlayers : GameObjectConversionSystem
    {
        protected override void OnUpdate()
        {
            Entities.ForEach((SfxrPlayer player) =>
            {
                //Object.DestroyImmediate(player);
            });
        }
    }
}

