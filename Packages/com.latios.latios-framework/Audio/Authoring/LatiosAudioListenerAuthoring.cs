using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Latios.Audio.Authoring
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Latios/Audio/Audio Listener")]
    public class LatiosAudioListenerAuthoring : MonoBehaviour
    {
        public float volume = 1f;

        [Range(0, 15)]
        public int interauralTimeDelayResolution = 2;

        public AudioIldProfileBuilder listenerResponseProfile;
    }
}

