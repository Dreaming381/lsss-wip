using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Latios.Calligraphics
{
    internal struct XMLTag
    {
        public TagType tagType;
        public bool    isClosing;
        public int     startID;  //start position raw text
        public int     endID;  //start position raw text
        public int Length => endID + 1 - startID;
        public TagValue value;
        public XMLTag(bool dummy)
        {
            tagType    = TagType.Unknown;
            isClosing  = false;
            startID    = -1;
            endID      = -1;
            value      = new TagValue();
            value.type = TagValueType.None;
            value.unit = TagUnitType.Pixels;
        }
    }

    internal enum TagType : byte
    {
        Hyperlink,
        Align,
        AllCaps,
        Alpha,
        Bold,
        Br,
        Color,
        CSpace,
        Font,
        FontWeight,
        FontWidth,
        Fraction,
        Gradient,
        Italic,
        Indent,
        LineHeight,
        LineIndent,
        Link,
        Lowercase,
        Mark,
        Mspace,
        NoBr,
        NoParse,
        Rotate,
        Strikethrough,
        Size,
        SmallCaps,
        Space,
        Sprite,
        Style,
        Subscript,
        Superscript,
        Underline,
        Uppercase,
        VOffset,
        Unknown  // Not a real tag, used to indicate an error

        //gradient, margin, pos, will not be supported
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    internal struct TagValue
    {
        [FieldOffset(0)]
        internal TagValueType type;

        [FieldOffset(1)]
        internal TagUnitType unit;

        [FieldOffset(2)]
        private float m_numericalValue;

        [FieldOffset(2)]
        private Color32 m_colorValue;

        //instead of storing String values in here (e.g. name of requested font),
        //we store the position in CalliBytesRaw and fetch it when needed
        [FieldOffset(2)]
        internal int valueStart;
        [FieldOffset(6)]
        internal int valueLength;
        [FieldOffset(10)]
        internal int valueHash;
        [FieldOffset(15)]
        internal StringValue stringValue;

        internal float NumericalValue
        {
            get
            {
                if (type != TagValueType.NumericalValue)
                    throw new InvalidOperationException("Not a numerical value");
                return m_numericalValue;
            }
            set
            {
                m_numericalValue = value;
            }
        }

        internal Color ColorValue
        {
            get
            {
                if (type != TagValueType.ColorValue)
                    throw new InvalidOperationException("Not a color value");
                return m_colorValue;
            }
            set
            {
                m_colorValue = value;
            }
        }
    }

    internal enum TagValueType : byte
    {
        None = 0,
        NumericalValue = 1,
        StringValue = 2,
        ColorValue = 4
    }

    internal enum TagUnitType : byte
    {
        Pixels,
        FontUnits,
        Percentage
    }

    public enum StringValue : byte
    {
        Unknown,  // Not a real tag, used to indicate unknown string, which needs to be fetched from calliBytesRaw
        Default,
        red,
        lightblue,
        blue,
        grey,
        black,
        green,
        white,
        orange,
        purple,
        yellow,
        left,
        right,
        center,
        justified,
        flush
    }
}

