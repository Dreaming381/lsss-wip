using Unity.Collections;

namespace Latios.Calligraphics
{
    internal static class CalligraphicsInternalExtensions
    {
        public static void GetSubString(this in CalliString calliString, ref FixedString128Bytes htmlTag, int startIndex, int length)
        {
            htmlTag.Clear();
            for (int i = startIndex, end = startIndex + length; i < end; i++)
                htmlTag.Append((char)calliString[i]);
        }

        public static bool IsAscii(this Unicode.Rune rune) => rune.value < 0x80;

        // Todo: Add support for other languages in a Burst-compatible way.
        public static Unicode.Rune ToLower(this Unicode.Rune rune)
        {
            if (rune.IsAscii())
                return new Unicode.Rune(rune.value + (((uint)(rune.value - 'A') <= ('Z' - 'A')) ? 0x20 : 0));
            return rune;
        }

        public static Unicode.Rune ToUpper(this Unicode.Rune rune)
        {
            if (rune.IsAscii())
                return new Unicode.Rune(rune.value - (((uint)(rune.value - 'a') <= ('z' - 'a')) ? 0x20 : 0));
            return rune;
        }

        public static bool IsLatin1(this Unicode.Rune rune)
        {
            return rune.value < 0x100;
        }

        public static bool IsWhiteSpace(this Unicode.Rune rune)
        {
            // https://en.wikipedia.org/wiki/Whitespace_character#Unicode
            var value = rune.value;
            if (IsLatin1(rune))
            {
                return value == ' ' ||
                       (value >= 0x9 && value <= 0xD) ||  // CHARACTER TABULATION (U+0009), LINE FEED (U+000A), LINE TABULATION (U+000B), FORM FEED (U+000C), CARRIAGE RETURN (U+000D)
                       value == 0xA0 ||  // NO-BREAK SPACE
                       value == 0x85  // NEXT LINE
                ;
            }

            return value == 0x1680 ||  // OGHAM SPACE MARK
                   (value >= 0x2000 && value <= 0x200A)  // EN QUAD(U+2000)
                                                         // EM QUAD(U+2001)
                                                         // EN SPACE(U+2002)
                                                         // EM SPACE(U+2003)
                                                         // THREE - PER - EM SPACE(U + 2004)
                                                         // FOUR - PER - EM SPACE(U + 2005)
                                                         // SIX - PER - EM SPACE(U + 2006)
                                                         // FIGURE SPACE(U+2007)
                                                         // PUNCTUATION SPACE(U+2008)
                                                         // THIN SPACE(U+2009)
                                                         // HAIR SPACE(U+200A)
                   || value == 0x2028 ||  // LINE SEPARATOR
                   value == 0x2029 ||  // PARAGRAPH SEPARATOR
                   value == 0x202F ||  // NARROW NO-BREAK SPACE
                   value == 0x205F ||  // MEDIUM MATHEMATICAL SPACE
                   value == 0x3000  // IDEOGRAPHIC SPACE
            ;
        }

        public static int GetSamplingSize(this FontTextureSize fontTextureSize)
        {
            // Todo: It would be nice if we could split this based on glyph type, but currently glyph generation
            // is dependent on the base configuration font
            return fontTextureSize switch
                   {
                       FontTextureSize.Normal => 64,  // Todo: 64 SDF8, 128 color
                       FontTextureSize.Big => 256,  // Todo: 256 SDF16, 512 color
                       FontTextureSize.Massive => 4096,  // Todo: 1024 SDF16, 4096 color
                       _ => 64
                   };
        }
    }
}

