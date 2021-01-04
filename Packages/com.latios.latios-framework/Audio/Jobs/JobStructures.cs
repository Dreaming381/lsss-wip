using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Latios.Audio
{
    internal struct ClipFrameLookup : IEquatable<ClipFrameLookup>
    {
        public BlobAssetReference<AudioClipBlob> clip;
        public int                               spawnFrameOrOffsetIndex;

        public unsafe bool Equals(ClipFrameLookup other)
        {
            return ((ulong)clip.GetUnsafePtr()).Equals((ulong)other.clip.GetUnsafePtr()) && spawnFrameOrOffsetIndex == other.spawnFrameOrOffsetIndex;
        }

        public unsafe override int GetHashCode()
        {
            return (int)((ulong)clip.GetUnsafePtr() >> 4) + 23 * spawnFrameOrOffsetIndex;
        }
    }

    internal struct Weights
    {
        public FixedListFloat128 channelWeights;
        public FixedListFloat512 itdWeights;
    }

    internal struct ListenerBufferParameters
    {
        public int        bufferStart;
        public BitField32 channelIsRight;
        public int        samplesPerChannel;
        public short      numChannels;
        public short      subFramesPerFrame;
    }

    internal struct ListenerWithTransform
    {
        public AudioListener  listener;
        public RigidTransform transform;
    }

    internal struct OneshotEmitter
    {
        public AudioSourceOneShot     source;
        public RigidTransform         transform;
        public AudioSourceEmitterCone cone;
        public bool                   useCone;
    }

    internal struct LoopedEmitter
    {
        public AudioSourceLooped      source;
        public RigidTransform         transform;
        public AudioSourceEmitterCone cone;
        public bool                   useCone;
    }
}

