using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Latios.Audio
{
    public struct AudioListener : IComponentData
    {
        public float volume;
        public int   audioFramesPerUpdate;
        public int   audioSubframesPerFrame;

        public int        interAuralTimeDelayResolution;
        public IldProfile ildProfile;
    }

    //Todo: Should this be a blob asset?
    public struct IldProfile : IEquatable<IldProfile>
    {
        internal FixedList512<FrequencyFilter> filtersLeft;
        internal FixedListInt128               channelIndicesLeft;
        internal FixedList512<FrequencyFilter> filtersRight;
        internal FixedListInt128               channelIndicesRight;

        internal FixedList512<float4> anglesPerLeftChannel;
        internal FixedList512<float4> anglesPerRightChannel;
        internal FixedListFloat128    panFilterRatiosPerLeftChannel;
        internal FixedListFloat128    panFilterRatiosPerRightChannel;

        public void AddFilterToChannel(FrequencyFilter filter, int channelIndex, bool isRightChannel)
        {
            if (isRightChannel)
            {
                filtersRight.Add(filter);
                channelIndicesRight.Add(channelIndex);
            }
            else
            {
                filtersLeft.Add(filter);
                channelIndicesLeft.Add(channelIndex);
            }
        }

        public bool Equals(IldProfile other)
        {
            return filtersLeft == other.filtersLeft &&
                   filtersRight == other.filtersRight &&
                   channelIndicesLeft == other.channelIndicesLeft &&
                   channelIndicesRight == other.channelIndicesRight &&
                   anglesPerLeftChannel == other.anglesPerLeftChannel &&
                   anglesPerRightChannel == other.anglesPerRightChannel &&
                   panFilterRatiosPerLeftChannel == other.panFilterRatiosPerLeftChannel &&
                   panFilterRatiosPerRightChannel == other.panFilterRatiosPerRightChannel;
        }

        public void AddChannel(float2 minMaxHorizontalAngleInRadiansCounterClockwiseFromRight, float2 minMaxVerticalAngleInRadians, float panFilterRatio, bool isRightChannel)
        {
            if (isRightChannel)
            {
                anglesPerRightChannel.Add(new float4(minMaxHorizontalAngleInRadiansCounterClockwiseFromRight, minMaxVerticalAngleInRadians));
                panFilterRatiosPerRightChannel.Add(panFilterRatio);
            }
            else
            {
                anglesPerLeftChannel.Add(new float4(minMaxHorizontalAngleInRadiansCounterClockwiseFromRight, minMaxVerticalAngleInRadians));
                panFilterRatiosPerLeftChannel.Add(panFilterRatio);
            }
        }

        public void SetChannelAttenuation(float panFilterRatio, int channelIndex, bool isRightChannel)
        {
            if (isRightChannel)
            {
                panFilterRatiosPerRightChannel[channelIndex] = panFilterRatio;
            }
            else
            {
                panFilterRatiosPerLeftChannel[channelIndex] = panFilterRatio;
            }
        }
    }

    public struct FrequencyFilter
    {
        public float               cutoff;
        public float               q;
        public float               gainInDecibels;
        public FrequencyFilterType type;
    }

    public enum FrequencyFilterType
    {
        Lowpass,
        Highpass,
        Bandpass,
        Bell,
        Notch,
        Lowshelf,
        Highshelf
    }
}

