using Latios.Myri.DSP;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Latios.Myri.Authoring
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Latios/Myri/Audio Listener (Myri)")]
    public class AudioListenerAuthoring : MonoBehaviour
    {
        [Header("Output")]
        [Tooltip("The raw volume applied to everything the listener hears. This value is not in decibels.")]
        public float volume = 1f;
        [Tooltip("A gain value that is applied to the mixed audio signal before the listener limiter is applied. This value is not in decibels.")]
        public float gain = 1f;
        [Tooltip("How quickly the volume should recover after an audio spike, in decibels per second")]
        [InspectorName("Limiter dB Relax Rate")]
        public float limiterDBRelaxPerSecond = BrickwallLimiter.kDefaultReleaseDBPerSample * 48000f;
        [Tooltip(
             "The amount of time in advance in seconds that the final limiter should examine samples for spikes so that it can begin ramping down the volume. Larger values result in smoother transitions but add latency to the final output.")
        ]
        public float limiterLookaheadTime = 255.9f / 48000f;

        [Header("Spatialization")]
        [Tooltip("A scale factor for all spatial ranges. Increasing this value allows the listener to hear sources farther away.")]
        public float rangeMultiplier = 1f;

        [Tooltip("The resolution of time-based spatialization. Increasing this value incurs a higher cost but may increase the player's sense of direction.")]
        [Range(0, 15)]
        public int interauralTimeDifferenceResolution = 2;

        [Tooltip("A custom volume and frequency spatialization profile. If empty, a default profile will be used.")]
        public ListenerProfileBuilder listenerResponseProfile;
    }

    public class AudioListenerBaker : Baker<AudioListenerAuthoring>
    {
        public override void Bake(AudioListenerAuthoring authoring)
        {
            BlobAssetReference<ListenerProfileBlob> blob;
            if (authoring.listenerResponseProfile == null)
            {
                var defaultBuilder = new DefaultListenerProfileBuilder();
                blob               = this.BuildAndRegisterListenerProfileBlob(defaultBuilder);
            }
            else
                blob = this.BuildAndRegisterListenerProfileBlob(authoring.listenerResponseProfile);

            var entity = GetEntity(TransformUsageFlags.Renderable);
            AddComponent(entity, new AudioListener
            {
                ildProfile              = blob,
                itdResolution           = authoring.interauralTimeDifferenceResolution,
                rangeMultiplier         = (half)authoring.rangeMultiplier,
                volume                  = authoring.volume,
                gain                    = authoring.gain,
                limiterDBRelaxPerSecond = authoring.limiterDBRelaxPerSecond,
                limiterLookaheadTime    = authoring.limiterLookaheadTime
            });
        }
    }
}

