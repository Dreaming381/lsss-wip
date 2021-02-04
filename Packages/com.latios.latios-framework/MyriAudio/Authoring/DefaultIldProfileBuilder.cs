using Unity.Mathematics;
using UnityEngine;

namespace Latios.Myri.Authoring
{
    internal class DefaultIldProfileBuilder : AudioIldProfileBuilder
    {
        protected override void ComputeProfile()
        {
            //left unblocked
            AddChannel(new float2(math.PI / 2f, math.PI * 1.25f), new float2(-math.PI, math.PI), 1f, false);
            //left fully blocked
            AddChannel(new float2(-math.PI / 4f, math.PI / 4f),   new float2(-math.PI, math.PI), 0f, false);
            AddFilterToChannel(new FrequencyFilter
            {
                cutoff         = 1500f,
                gainInDecibels = 0f,
                q              = 0.707f,
                type           = FrequencyFilterType.Lowpass
            }, 1, false);
            //right unblocked
            AddChannel(new float2(-math.PI / 4f, math.PI / 2f),      new float2(-math.PI, math.PI), 1f, true);
            //right fully blocked
            AddChannel(new float2(math.PI * 0.75f, math.PI * 1.25f), new float2(-math.PI, math.PI), 0f, true);
            AddFilterToChannel(new FrequencyFilter
            {
                cutoff         = 1500f,
                gainInDecibels = 0f,
                q              = 0.707f,
                type           = FrequencyFilterType.Lowpass
            }, 3, true);
        }
    }
}

