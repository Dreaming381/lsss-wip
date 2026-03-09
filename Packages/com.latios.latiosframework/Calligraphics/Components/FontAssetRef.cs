using System;
using Unity.Collections;

namespace TextMeshDOTS
{
    // Todo: This is used by FontRequest. Once we decide what to do with that, we can maybe internalize this.

    /// <summary>
    /// FontAssetRef is THE link between any fonts request and font entities, and consists of a hash representing the 
    /// font family, and variation axis used during typesetting such as weight ("normal", "bold", semibold"), 
    /// width ("condensed", normal"), and italic. Slant is ignored in such font matching (see GetHashcode) 
    /// because slant value cannot be "guessed" and requested by user during typesetting 
    /// </summary>
    [Serializable]
    public struct FontAssetRef : IEquatable<FontAssetRef>
    {
        //Font selection logic: https://www.high-logic.com/font-editor/fontcreator/tutorials/font-family-settings
        public int familyHash;    //default to typeographic family, and fall-back to family if it does not exist
        public float weight;
        public float width;
        public bool isItalic;
        public float slant;

        public FontAssetRef(FixedString128Bytes fontFamily, FixedString128Bytes typographicFamily, float weight, float width, bool isItalic, float slant)
        {
            this.familyHash = typographicFamily.IsEmpty ? TextHelper.GetHashCodeCaseInsensitive(fontFamily) : TextHelper.GetHashCodeCaseInsensitive(typographicFamily);
            this.weight = weight;
            this.width = width;
            this.isItalic = isItalic;
            this.slant = slant;
        }
        public FontAssetRef(int familyNameHashCode, float weight, float width, bool isItalic, float slant = 0)
        {
            this.familyHash = familyNameHashCode;
            this.weight = weight;
            this.width = width;
            this.isItalic = isItalic;
            this.slant = slant;
        }

        public override bool Equals(object obj) => obj is FontAssetRef other && Equals(other);

        public bool Equals(FontAssetRef other)
        {
            return GetHashCode() == other.GetHashCode();
        }

        public static bool operator ==(FontAssetRef e1, FontAssetRef e2)
        {
            return e1.GetHashCode() == e2.GetHashCode();
        }
        public static bool operator !=(FontAssetRef e1, FontAssetRef e2)
        {
            return e1.GetHashCode() != e2.GetHashCode();
        }
        
        public override int GetHashCode()
        {
            int hashCode = 2055808453;
            hashCode = hashCode * -1521134295 + familyHash;
            hashCode = hashCode * -1521134295 + (int)weight;
            hashCode = hashCode * -1521134295 + width.GetHashCode();
            hashCode = hashCode * -1521134295 + isItalic.GetHashCode();
            //fonts are searched at runtime via FontAssetRef match. As slant angle cannot be guessed, do not include this in hash
            //hashCode = hashCode * -1521134295 + slant.GetHashCode();
            return hashCode;
        }
        public override string ToString()
        {
            return $"FamilyHash {familyHash} weigth {weight} width {width} isItalic {isItalic}";
        }
    }
}