using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Latios.Audio.Authoring
{
    [DisallowMultipleComponent]
    public class LatiosAudioListenerAuthoring : MonoBehaviour
    {
        public float volume                 = 1f;
        public int   audioFramesPerUpdate   = 3;
        public int   audioSubframesPerFrame = 1;

        [Range(0, 15)]
        public int interauralTimeDelayResolution = 2;

        public AudioIldProfileBuilder listenerResponseProfile;
    }
}

