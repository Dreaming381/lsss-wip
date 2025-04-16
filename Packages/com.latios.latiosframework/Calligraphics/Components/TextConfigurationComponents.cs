using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Latios.Calligraphics
{
    /// <summary>
    /// The base settings of the text before any rich text tags or animations are applied.
    /// Usage: ReadWrite
    /// </summary>
    public struct TextBaseConfiguration : IComponentData
    {
        /// <summary>The hash code of the font's family name, which can be computed using TypeHash.FNV1A64<T>()
        /// Do NOT use the managed string version to compute the hash!
        /// </summary>
        public ulong defaultFontFamilyHash;

        /// <summary>
        /// The line width of the font, in world units, only if word wrapping is enabled
        /// </summary>
        public float maxLineWidth;

        internal uint packed;  // 4 bits left unused

        /// <summary>
        /// The color of the rendered text
        /// </summary>
        public half4 color;

        /// <summary>
        /// The size of the font, in point sizes
        /// </summary>
        public half fontSize;

        /// <summary>
        /// Additional word spacing in font units where a value of 1 equals 0.01 em
        /// </summary>
        public half wordSpacing;
        /// <summary>
        /// Additional line spacing in font units where a value of 1 equals 0.01 em
        /// </summary>
        public half lineSpacing;
        /// <summary>
        /// Additional paragraph spacing in font units where a value of 1 equals 0.01 em
        /// </summary>
        public half paragraphSpacing;

        /// <summary>
        /// The horizontal alignment mode of the text
        /// </summary>
        public HorizontalAlignmentOptions lineJustification
        {
            get => (HorizontalAlignmentOptions)Bits.GetBits(packed, 0, 3);
            set => Bits.SetBits(ref packed, 0, 3, (uint)value);
        }

        /// <summary>
        /// The vertical alignment mode of the text
        /// </summary>
        public VerticalAlignmentOptions verticalAlignment
        {
            get => (VerticalAlignmentOptions)Bits.GetBits(packed, 4, 4);
            set => Bits.SetBits(ref packed, 4, 4, (uint)value);
        }

        /// <summary>
        /// The thickness or intensity of the font
        /// </summary>
        public FontWeight fontWeight
        {
            get => (FontWeight)Bits.GetBits(packed, 8, 4);
            set => Bits.SetBits(ref packed, 8, 4, (uint)value);
        }
        /// <summary>
        /// The compression or widening of the font across a line
        /// </summary>
        public FontWidth fontWidth
        {
            get => (FontWidth)Bits.GetBits(packed, 12, 4);
            set => Bits.SetBits(ref packed, 12, 4, (uint)value);
        }

        public float fontWidthValue => fontWidth switch
        {
            FontWidth.UltraCondensed => 50f,
            FontWidth.ExtraCondensed => 62.5f,
            FontWidth.Narrow => 75f,
            FontWidth.SemiCondensed => 87.5f,
            FontWidth.Normal => 100f,
            FontWidth.SemiExpanded => 112.5f,
            FontWidth.Expanded => 125f,
            FontWidth.ExtraExpanded => 150f,
            FontWidth.UltraExpanded => 200f,
            _ => float.NaN,
        };

        /// <summary>
        /// The various font styling flags
        /// </summary>
        public FontStyles fontStyles
        {
            get => (FontStyles)Bits.GetBits(packed, 16, 11);
            set => Bits.SetBits(ref packed, 16, 11, (uint)value);
        }

        /// <summary>
        /// The size of the characters in the texture
        /// </summary>
        public FontTextureSize fontTextureSize
        {
            get => (FontTextureSize)Bits.GetBits(packed, 27, 2);
            set => Bits.SetBits(ref packed, 27, 2, (uint)value);
        }

        /// <summary>
        /// Use orthographic-mode computation of character sizes
        /// </summary>
        public bool isOrthographic
        {
            get => Bits.GetBit(packed, 30);
            set => Bits.SetBit(ref packed, 30, value);
        }

        /// <summary>
        /// Enable word wrapping
        /// </summary>
        public bool wordWrap
        {
            get => Bits.GetBit(packed, 31);
            set => Bits.SetBit(ref packed, 31, value);
        }

        // Todo: It would be nice if we could split this based on glyph type, but currently glyph generation
        // is dependent on the base configuration font
        internal int samplingSize => fontTextureSize switch
        {
            FontTextureSize.Normal => 64,  // Todo: 64 SDF8, 128 color
            FontTextureSize.Big => 256,  // Todo: 256 SDF16, 512 color
            FontTextureSize.Massive => 4096,  // Todo: 1024 SDF16, 4096 color
            _ => 64
        };
    }

    /// <summary>
    /// Horizontal text alignment options.
    /// </summary>
    public enum HorizontalAlignmentOptions : byte
    {
        Left,
        Center,
        Right,
        Justified,
        Flush,
        Geometry
    }

    /// <summary>
    /// Vertical text alignment options.
    /// </summary>
    public enum VerticalAlignmentOptions : byte
    {
        TopBase,
        TopAscent,
        TopDescent,
        TopCap,
        TopMean,
        BottomBase,
        BottomAscent,
        BottomDescent,
        BottomCap,
        BottomMean,
        MiddleTopAscentToBottomDescent,
    }

    public enum FontWeight : byte
    {
        //https://learn.microsoft.com/en-us/typography/opentype/spec/os2#usweightclass
        // All values are divided by 100 for data compression
        Thin = 1,
        ExtraLight = 2,
        UltraLight = 2,
        Light = 3,
        Normal = 4,
        Regular = 4,
        Medium = 5,
        SemiBold = 6,
        DemiBold = 6,
        Bold = 7,
        ExtraBold = 8,
        UltraBold = 8,
        Black = 9,
        Heavy = 9,
    }

    public enum FontWidth : byte
    {
        //https://learn.microsoft.com/en-us/typography/opentype/spec/os2#uswidthclass
        // Enum values do not correspond to raw values
        UltraCondensed = 1,  // 50
        ExtraCondensed = 2,  // 62.5
        Narrow = 3,  // 75
        Condensed = 3,  // 75
        SemiCondensed = 4,  // 87.5
        Normal = 5,  // 100
        SemiExpanded = 6,  // 112.5
        Expanded = 7,  // 125
        ExtraExpanded = 8,  // 150
        UltraExpanded = 9  // 200
    }

    [Flags]
    public enum FontStyles
    {
        [InspectorName("clr")] Normal = 0,
        [InspectorName("B")] Bold = 0x1,
        [InspectorName("I")] Italic = 0x2,
        //[InspectorName("_")] Underline = 0x4,
        [InspectorName("low")] LowerCase = 0x8,
        [InspectorName("UPP")] UpperCase = 0x10,
        [InspectorName("Sᴍᴀʟʟ")] SmallCaps = 0x20,
        //[InspectorName("-")] Strikethrough = 0x40,
        [InspectorName("x²")] Superscript = 0x80,
        [InspectorName("x₂")] Subscript = 0x100,
        //[InspectorName("[]")] Highlight = 0x200,
        [InspectorName("½")] Fraction = 0x400,
    }

    public enum FontTextureSize
    {
        Normal = 0,
        Big = 1,
        Massive = 2,
    }
}

