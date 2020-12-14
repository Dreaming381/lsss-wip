using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Latios.Audio
{
    internal unsafe struct IldBufferChannel
    {
        public float* buffer;
    }

    internal struct IldBuffer
    {
        public FixedList64<IldBufferChannel> leftBufferChannels;
        public FixedList64<IldBufferChannel> rightBufferChannels;
        public int                           frame;
        public int                           bufferId;
        public int                           framesInBuffer;
        public int                           subframesPerFrame;
        public bool                          warnIfStarved;
    }
}

