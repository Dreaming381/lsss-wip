using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Latios.Audio.Authoring
{
    [DisallowMultipleComponent]
    public class LatiosAudioListenerAuthoring : MonoBehaviour, IConvertGameObjectToEntity
    {
        public float volume                 = 1f;
        public int   audioFramesPerUpdate   = 3;
        public int   audioSubframesPerFrame = 1;

        //Eventually I would like to expose the listener profiles, but I need to discuss with real professionals first because I don't know enough about this.

        public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
        {
            IldProfile profile = default;
            //left unblocked
            profile.AddChannel(new float2(math.PI / 2f, math.PI * 1.25f), new float2(-math.PI, math.PI), 1f, false);
            //left fully blocked
            profile.AddChannel(new float2(-math.PI / 4f, math.PI / 4f),   new float2(-math.PI, math.PI), 1f, false);
            profile.AddFilterToChannel(new FrequencyFilter
            {
                cutoff         = 1500f,
                gainInDecibels = 0f,
                q              = 0.707f,
                type           = FrequencyFilterType.Lowpass
            }, 1, false);
            //right unblocked
            profile.AddChannel(new float2(-math.PI / 4f, math.PI / 2f),      new float2(-math.PI, math.PI), 1f, true);
            //right fully blocked
            profile.AddChannel(new float2(math.PI * 0.75f, math.PI * 1.25f), new float2(-math.PI, math.PI), 1f, true);
            profile.AddFilterToChannel(new FrequencyFilter
            {
                cutoff         = 1500f,
                gainInDecibels = 0f,
                q              = 0.707f,
                type           = FrequencyFilterType.Lowpass
            }, 3, true);

            dstManager.AddComponentData(entity, new AudioListener
            {
                volume                        = volume,
                audioFramesPerUpdate          = audioFramesPerUpdate,
                audioSubframesPerFrame        = audioSubframesPerFrame,
                interAuralTimeDelayResolution = 2,
                ildProfile                    = profile
            });
        }
    }
}

