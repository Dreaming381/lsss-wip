using System;
using System.Runtime.CompilerServices;

namespace UnityEngine.TextCore.Exposed
{
    public struct UnityFontReference
    {
        public string typographicFamily;

        public string typographicSubfamily;

        public int faceIndex;

        public string filePath;
    }

    public static class SystemFonts
    {
        public static ReadOnlySpan<UnityFontReference> GetAll()
        {
            // Warning: This only works if UnityFontReference and LowLevel.FontReference are identical.
            // Todo: Would it maybe be better to just allocate a new managed array and copy all elements with mono?
            var internalFonts = LowLevel.FontEngine.GetSystemFontReferences();
            return Unsafe.As<UnityFontReference[]>(internalFonts).AsSpan();
        }
    }
}

