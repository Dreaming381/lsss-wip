using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Latios.Audio
{
    public struct AudioSourceLooped : IComponentData
    {
        //Internal until we can detect changes. Will likely require a separate component.
        internal BlobAssetReference<AudioClipBlob> clip;
        internal int                               loopOffsetIndex;
        public float                               volume;
        public float                               innerRange;
        public float                               outerRange;
        public float                               rangeFadeMargin;
    }

    public struct AudioSourceOneShot : IComponentData
    {
        internal BlobAssetReference<AudioClipBlob> clip;
        internal int                               spawnedAudioFrame;
        internal int                               spawnedBufferId;
        public float                               volume;
        public float                               innerRange;
        public float                               outerRange;
        public float                               rangeFadeMargin;
    }

    public struct AudioSourceEmitterCone : IComponentData
    {
        public float cosInnerAngle;
        public float cosOuterAngle;
        public float outerAngleAttenuation;
    }

    public struct AudioSourceDestroyOneShotWhenFinished : IComponentData { }

    internal struct AudioClipBlob
    {
        public BlobArray<float> samplesLeftOrMono;
        public BlobArray<float> samplesRight;
        public BlobArray<int>   loopedOffsets;
        public int              sampleRate;

        public bool isStereo => samplesRight.Length == samplesLeftOrMono.Length;
    }

    //This does not need to be systemstate because the audio system doesn't care if the entity is destroyed.
    internal struct AudioSourceInitialized : IComponentData { }
}

