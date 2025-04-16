using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Latios.Calligraphics
{
    public struct FontCollectionBlobReference : IComponentData
    {
        public BlobAssetReference<FontCollectionBlob> blob;
    }

    public struct FontFallbackStrategyBlobReference : IComponentData
    {
        public BlobAssetReference<FontFallbackStrategyBlob> blob;
    }

    public struct FontCollectionBlob
    {
        public struct StreamingTtc
        {
            public BlobArray<byte> path;
            public BlobArray<byte> family;
        }

        public struct StreamingTtf
        {
            public BlobArray<byte> path;
            public BlobArray<byte> family;
            public BlobArray<byte> style;
        }

        public struct Sprite
        {
            public int2               dimensions;
            public BlobArray<byte>    name;
            public BlobArray<Color32> pixels;
        }

        public BlobArray<StreamingTtc> ttcs;
        public BlobArray<StreamingTtf> ttfs;
        public BlobArray<Sprite>       sprites;
    }

    public struct FontFallbackStrategyBlob
    {
        public struct Fallback
        {
            public BlobArray<byte> familyWithAbsent;
            public BlobArray<byte> familyFallback;
        }

        public BlobArray<Fallback> fallbacks;
    }

    // Todo: Blob asset and blob refs
    public struct TextColorGradient
    {
        public int   nameHash;  // Todo: Make 64-bit?
        public half4 topLeft;
        public half4 topRight;
        public half4 bottomLeft;
        public half4 bottomRight;
    }
}

