using System;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace TextMeshDOTS
{
    [Serializable]
    [InternalBufferCapacity(0)]
    public struct FontReference : IEquatable<FontReference>, IBufferElementData
    {
        public FixedString512Bytes filePath;
        public bool streamingAssetLocationValidated;
        public bool isSystemFont;
        public int faceIndexInFile; 

        //face Information
        public FixedString128Bytes fontFamily;
        public FixedString128Bytes fontSubFamily;
        public FixedString128Bytes typographicFamily;
        public FixedString128Bytes typographicSubfamily;
        public float defaultWeight;
        public float defaultWidth;
        public bool isItalic;
        public float slant;
        public readonly FontAssetRef fontAssetRef => new FontAssetRef(fontFamily, typographicFamily, defaultWeight, defaultWidth, isItalic, slant);

        public override bool Equals(object obj)
        {
            if (obj is FontReference item)
            {
                return Equals(item);
            }
            return false;
        }
        bool IEquatable<FontReference>.Equals(FontReference other)
        {
            return fontAssetRef == other.fontAssetRef;
        }
        public override int GetHashCode()
        {
             return fontAssetRef.GetHashCode();
        }

        public static bool operator ==(FontReference target, FontReference other) { return target.Equals(other); }
        public static bool operator !=(FontReference target, FontReference other) { return !target.Equals(other); }
        public override string ToString()
        {
            if (typographicFamily != "")
                return $"{fontFamily} - {fontSubFamily} (typographic: {typographicFamily} - {typographicSubfamily})";
            else
                return $"{fontFamily} - {fontSubFamily}";
        }
    }
}
