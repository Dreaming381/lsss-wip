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

    internal unsafe struct IldBuffer
    {
        public IldBufferChannel* bufferChannels;
        public int               channelCount;
        public int               frame;
        public int               bufferId;
        public int               framesInBuffer;
        public int               subframesPerFrame;
        public bool              warnIfStarved;
    }
}

