using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Latios.Calligraphics
{
    public struct FaceConstants
    {
        //public float unitsPerEM;
        //public uint designSize;
        //public uint subfamilyID;
        //public uint subfamilyNameID;
        //public uint minRecommendedSize;
        //public uint maxRecommendedSize;
    }

    public struct FontConstants
    {
        public bool  isItalic;
        public float italicsStyleSlant;
        public float weight;
        public float boldStyleSpacing;
        public int   capHeight;
        public int   xHeight;
        //public int subscriptEmXSize;
        //public int subscriptEmYSize;
        //public int subscriptEmXOffset;
        //public int subscriptEmYOffset;
        //public int superscriptEmXSize;
        //public int superscriptEmYSize;
        //public int superscriptEmXOffset;
        //public int superscriptEmYOffset;
        //public int2 scale;
        public float tabWidth;
        public float tabMultiple;
        public float regularStyleSpacing;
    }

    public struct FontDirectionConstants
    {
        public float ascender;
        public float descender;
        //public float lineGap;
    }

    public struct FontDirectionScriptConstants
    {
        public float baseLine;
    }
}

