using System.Collections.Generic;
using Latios.Audio.Authoring;
using usfxr;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Lsss.Authoring
{
    [DisallowMultipleComponent]
    //[RequireComponent(typeof(LatiosAudioSourceAuthoring))]
    [AddComponentMenu("LSSS/Behaviors/Sound Effect")]
    public class SoundEffectAuthoring : MonoBehaviour, IDeclareReferencedPrefabs
    {
        public SfxrParams effectSettings;

        public void DeclareReferencedPrefabs(List<GameObject> referencedPrefabs)
        {
            var audioSource  = GetComponent<LatiosAudioSourceAuthoring>();
            audioSource.clip = SfxrPlayer.GetClip(effectSettings);
        }
    }
}

