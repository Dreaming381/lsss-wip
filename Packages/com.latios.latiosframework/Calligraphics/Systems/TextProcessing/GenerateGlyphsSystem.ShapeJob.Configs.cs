using System;
using Latios.Calligraphics.HarfBuzz;
using Latios.Calligraphics.RichText;
using Unity.Collections;
using UnityEngine;

namespace Latios.Calligraphics.Systems
{
    public partial struct GenerateGlyphsSystem
    {
        partial struct ShapeJob
        {
            struct OpenTypeFeatureConfig : IDisposable
            {
                //https://learn.microsoft.com/en-us/typography/opentype/spec/featurelist

                public NativeList<Feature> values;
                public int                 smallCapsStartID;
                public int                 subscriptStartID;
                public int                 superscriptStartID;
                public int                 fractionStartID;
                public OpenTypeFeatureConfig(int size, Allocator allocator)
                {
                    values             = new NativeList<Feature>(size, allocator);
                    smallCapsStartID   = -1;
                    subscriptStartID   = -1;
                    superscriptStartID = -1;
                    fractionStartID    = -1;
                }
                public void FinalizeOpenTypeFeatures(int position)
                {
                    if (smallCapsStartID != -1)
                        values.Add(new Feature(Harfbuzz.HB_TAG('s', 'm', 'c', 'p'), 1, (uint)smallCapsStartID, (uint)position));
                    if (subscriptStartID != -1)
                        values.Add(new Feature(Harfbuzz.HB_TAG('s', 'u', 'b', 's'), 1, (uint)subscriptStartID, (uint)position));
                    if (superscriptStartID != -1)
                        values.Add(new Feature(Harfbuzz.HB_TAG('s', 'u', 'p', 's'), 1, (uint)superscriptStartID, (uint)position));
                    if (fractionStartID != -1)
                        values.Add(new Feature(Harfbuzz.HB_TAG('f', 'r', 'a', 'c'), 1, (uint)fractionStartID, (uint)position));
                }
                public void Update(ref XMLTag tag, int position)
                {
                    switch (tag.tagType)
                    {
                        case TagType.SmallCaps:
                            if (!tag.isClosing)
                            {
                                if (smallCapsStartID == -1)
                                    smallCapsStartID = position;
                            }
                            else
                            {
                                values.Add(new Feature(Harfbuzz.HB_TAG('s', 'm', 'c', 'p'), 1, (uint)smallCapsStartID, (uint)position));
                                smallCapsStartID = -1;
                            }
                            return;
                        case TagType.Subscript:
                            if (!tag.isClosing)
                            {
                                if (subscriptStartID == -1)
                                    subscriptStartID = position;
                            }
                            else
                            {
                                values.Add(new Feature(Harfbuzz.HB_TAG('s', 'u', 'b', 's'), 1, (uint)subscriptStartID, (uint)position));
                                subscriptStartID = -1;
                            }
                            return;
                        case TagType.Superscript:
                            if (!tag.isClosing)
                            {
                                if (superscriptStartID == -1)
                                    superscriptStartID = position;
                            }
                            else
                            {
                                values.Add(new Feature(Harfbuzz.HB_TAG('s', 'u', 'p', 's'), 1, (uint)superscriptStartID, (uint)position));
                                superscriptStartID = -1;
                            }
                            return;
                        case TagType.Fraction:
                            if (!tag.isClosing)
                            {
                                if (fractionStartID == -1)
                                    fractionStartID = position;
                            }
                            else
                            {
                                values.Add(new Feature(Harfbuzz.HB_TAG('f', 'r', 'a', 'c'), 1, (uint)fractionStartID, (uint)position));
                                fractionStartID = -1;
                            }
                            return;
                    }
                }
                public void SetGlobalFeatures(in TextBaseConfiguration textBaseConfiguration, uint textLength)
                {
                    if ((textBaseConfiguration.fontStyles & FontStyles.SmallCaps) == FontStyles.SmallCaps)
                        values.Add(new Feature(Harfbuzz.HB_TAG('s', 'm', 'c', 'p'), 1, 0, textLength));
                    if ((textBaseConfiguration.fontStyles & FontStyles.Subscript) == FontStyles.Subscript)
                        values.Add(new Feature(Harfbuzz.HB_TAG('s', 'u', 'b', 's'), 1, 0, textLength));
                    if ((textBaseConfiguration.fontStyles & FontStyles.Superscript) == FontStyles.Superscript)
                        values.Add(new Feature(Harfbuzz.HB_TAG('s', 'u', 'p', 's'), 1, 0, textLength));
                    if ((textBaseConfiguration.fontStyles & FontStyles.Fraction) == FontStyles.Fraction)
                        values.Add(new Feature(Harfbuzz.HB_TAG('f', 'r', 'a', 'c'), 1, 0, textLength));
                }
                public void Clear()
                {
                    values.Clear();
                    smallCapsStartID   = -1;
                    subscriptStartID   = -1;
                    superscriptStartID = -1;
                    fractionStartID    = -1;
                }

                public void Dispose()
                {
                    if (values.IsCreated)
                        values.Dispose();
                }
            }

            struct FontConfig
            {
                public int m_faceIndex;
                public int m_namedVariationIndex;

                public int                     m_fontFamilyHash;
                public FixedStack512Bytes<int> m_fontFamilyHashStack;

                public float                     m_fontWeight;
                public FixedStack512Bytes<float> m_fontWeightStack;

                public float                     m_fontWidth;
                public FixedStack512Bytes<float> m_fontWidthStack;

                public bool m_isItalic;

                public FontTextureSize m_fontTextureSize;

                FontLookupKey FontLookupKey
                {
                    get { return new FontLookupKey(m_fontFamilyHash, m_fontWeight, m_fontWidth, m_isItalic); }
                }

                public void Reset(in TextBaseConfiguration textBaseConfiguration, ref FontTable fontTable)
                {
                    m_isItalic = (textBaseConfiguration.fontStyles & FontStyles.Italic) == FontStyles.Italic;

                    m_fontFamilyHash = textBaseConfiguration.defaultFontFamilyHash;
                    m_fontFamilyHashStack.Clear();
                    m_fontFamilyHashStack.Add(m_fontFamilyHash);

                    m_fontWeight = textBaseConfiguration.fontWeight.Value();
                    m_fontWeightStack.Clear();
                    m_fontWeightStack.Add(m_fontWeight);

                    m_fontWidth = textBaseConfiguration.fontWidth.Value();
                    m_fontWidthStack.Clear();
                    m_fontWidthStack.Add(m_fontWidth);

                    m_fontTextureSize = textBaseConfiguration.fontTextureSize;

                    var defaultFaceIndex = fontTable.GetFaceIndex(FontLookupKey);
                    if(defaultFaceIndex == -1)
                    {
                        //fontTable does not contain a font of this family,
                        //so switch default font family to the family at faceIndex 0,
                        //leave everything else the same (weight, width etc), and search matching faceIndex
                        //Debug.Log($"Could not find FontLookupKey: {FontLookupKey}");
                        defaultFaceIndex        = 0;
                        var defaultFontLookupKey = fontTable.fontLookupKeys[defaultFaceIndex];
                        m_fontFamilyHash        = defaultFontLookupKey.familyHash;
                        defaultFaceIndex        = fontTable.GetFaceIndex(FontLookupKey);

                        //var face = fontTable.faces[defaultFaceIndex];
                        //var language = Language.English();
                        //Debug.Log($"set default FontLookupKey to {FontLookupKey} ({face.GetName(NameID.FONT_FAMILY, language)} {face.GetName(NameID.FONT_SUBFAMILY, language)})");
                    }
                    m_faceIndex = defaultFaceIndex == -1 ? m_faceIndex : defaultFaceIndex;

                    if (fontTable.faces[m_faceIndex].HasVarData)
                        fontTable.GetNamedVariationLookup(FontLookupKey, out m_namedVariationIndex);
                }

                public void Update(ref XMLTag tag, ref FontTable fontTable, ref CalliString calliStringRaw)
                {
                    int newFaceIndex;
                    switch (tag.tagType)
                    {
                        case TagType.Italic:
                            if (!tag.isClosing)
                                m_isItalic = true;
                            else
                                m_isItalic = false;

                            newFaceIndex = fontTable.GetFaceIndex(FontLookupKey);
                            m_faceIndex  = newFaceIndex == -1 ? m_faceIndex : newFaceIndex;
                            if (fontTable.faces[m_faceIndex].HasVarData)
                                fontTable.GetNamedVariationLookup(FontLookupKey, out m_namedVariationIndex);
                            return;
                        case TagType.Bold:
                            if (!tag.isClosing)
                            {
                                m_fontWeight = FontWeight.Bold.Value();
                                m_fontWeightStack.Add(m_fontWeight);
                            }
                            else
                                m_fontWeight = m_fontWeightStack.RemoveExceptRoot();

                            newFaceIndex = fontTable.GetFaceIndex(FontLookupKey);
                            m_faceIndex  = newFaceIndex == -1 ? m_faceIndex : newFaceIndex;
                            if (fontTable.faces[m_faceIndex].HasVarData)
                                fontTable.GetNamedVariationLookup(FontLookupKey, out m_namedVariationIndex);
                            return;
                        case TagType.FontWeight:
                            if (!tag.isClosing)
                            {
                                m_fontWeight = tag.value.NumericalValue;
                                m_fontWeightStack.Add(m_fontWeight);
                            }
                            else
                                m_fontWeight = m_fontWeightStack.RemoveExceptRoot();

                            newFaceIndex = fontTable.GetFaceIndex(FontLookupKey);
                            m_faceIndex  = newFaceIndex == -1 ? m_faceIndex : newFaceIndex;
                            if (fontTable.faces[m_faceIndex].HasVarData)
                                fontTable.GetNamedVariationLookup(FontLookupKey, out m_namedVariationIndex);
                            return;
                        case TagType.FontWidth:
                            if (!tag.isClosing)
                            {
                                m_fontWidth = tag.value.NumericalValue;
                                m_fontWidthStack.Add(m_fontWidth);
                            }
                            else
                                m_fontWidth = m_fontWidthStack.RemoveExceptRoot();

                            newFaceIndex = fontTable.GetFaceIndex(FontLookupKey);
                            m_faceIndex  = newFaceIndex == -1 ? m_faceIndex : newFaceIndex;
                            if (fontTable.faces[m_faceIndex].HasVarData)
                                fontTable.GetNamedVariationLookup(FontLookupKey, out m_namedVariationIndex);
                            return;
                        case TagType.Font:
                            if (!tag.isClosing)
                            {
                                if (tag.value.stringValue == StringValue.Default)
                                    m_fontFamilyHash = m_fontFamilyHashStack[0];
                                else
                                {
                                    //fetch name of font from calliStringRaw Buffer
                                    //To-Do: better to store valueHash in tag struct
                                    FixedString128Bytes stringValue = default;  //should not happen too often, so should be OK to allocate here
                                    calliStringRaw.GetSubString(ref stringValue, tag.value.valueStart, tag.value.valueLength);
                                    m_fontFamilyHash = TextHelper.GetHashCodeCaseInsensitive(stringValue);
                                    m_fontFamilyHashStack.Add(m_fontFamilyHash);
                                }

                                newFaceIndex = fontTable.GetFaceIndex(FontLookupKey);
                                m_faceIndex  = newFaceIndex == -1 ? m_faceIndex : newFaceIndex;
                                if (fontTable.faces[m_faceIndex].HasVarData)
                                    fontTable.GetNamedVariationLookup(FontLookupKey, out m_namedVariationIndex);
                                return;
                            }
                            else
                            {
                                m_fontFamilyHash = m_fontFamilyHashStack.RemoveExceptRoot();

                                newFaceIndex = fontTable.GetFaceIndex(FontLookupKey);
                                m_faceIndex  = newFaceIndex == -1 ? m_faceIndex : newFaceIndex;
                                if (fontTable.faces[m_faceIndex].HasVarData)
                                    fontTable.GetNamedVariationLookup(FontLookupKey, out m_namedVariationIndex);
                            }
                            return;
                    }
                }
            }

            // Use LayoutConfig to change case prior to hb-shape (works only for latin text), and to track
            // the subset of rich-text-driven layout state needed to make word-wrap / line-break / vertical-
            // layout decisions during shaping: font size, char/mono spacing, indent, line-justification, and
            // baseline offset. Purely visual state (colors, gradients, FX rotation/scale, underline/
            // strikethrough) stays in GenerateRenderGlyphsJob's own (larger) LayoutConfig.
            // Should this use cases really be in scope of Latios.Calligraphics?
            struct LayoutConfig
            {
                public FontStyles m_fontStyles;

                public float                     m_currentFontSize;
                public FixedStack512Bytes<float> m_sizeStack;

                public float m_cSpacing;
                public float m_monoSpacing;
                public float m_xAdvance;

                public float                     m_tagLineIndent;
                public float                     m_tagIndent;
                public FixedStack512Bytes<float> m_indentStack;

                public HorizontalAlignmentOptions                     m_lineJustification;
                public FixedStack512Bytes<HorizontalAlignmentOptions> m_lineJustificationStack;

                public float                     m_baselineOffset;
                public FixedStack512Bytes<float> m_baselineOffsetStack;

                public LayoutConfig(in TextBaseConfiguration textBaseConfiguration)
                {
                    m_fontStyles = textBaseConfiguration.fontStyles;

                    m_currentFontSize = textBaseConfiguration.fontSize;
                    m_sizeStack       = default;
                    m_sizeStack.Add(m_currentFontSize);

                    m_cSpacing    = 0;
                    m_monoSpacing = 0;
                    m_xAdvance    = 0;

                    m_tagLineIndent = 0;
                    m_tagIndent     = 0;
                    m_indentStack   = default;
                    m_indentStack.Add(m_tagIndent);

                    m_lineJustification      = textBaseConfiguration.lineJustification;
                    m_lineJustificationStack = default;
                    m_lineJustificationStack.Add(m_lineJustification);

                    m_baselineOffset      = 0;
                    m_baselineOffsetStack = default;
                    m_baselineOffsetStack.Add(m_baselineOffset);
                }
                public void Reset(in TextBaseConfiguration textBaseConfiguration)
                {
                    m_fontStyles = textBaseConfiguration.fontStyles;

                    m_currentFontSize = textBaseConfiguration.fontSize;
                    m_sizeStack.Clear();
                    m_sizeStack.Add(m_currentFontSize);

                    m_cSpacing    = 0;
                    m_monoSpacing = 0;
                    m_xAdvance    = 0;

                    m_tagLineIndent = 0;
                    m_tagIndent     = 0;
                    m_indentStack.Clear();
                    m_indentStack.Add(m_tagIndent);

                    m_lineJustification = textBaseConfiguration.lineJustification;
                    m_lineJustificationStack.Clear();
                    m_lineJustificationStack.Add(m_lineJustification);

                    m_baselineOffset = 0;
                    m_baselineOffsetStack.Clear();
                    m_baselineOffsetStack.Add(0);
                }
                public void Update(ref XMLTag tag, in TextBaseConfiguration textBaseConfiguration)
                {
                    switch (tag.tagType)
                    {
                        case TagType.AllCaps:
                        case TagType.Uppercase:
                        {
                            if (tag.isClosing)
                                m_fontStyles &= ~FontStyles.UpperCase;
                            else
                                m_fontStyles |= FontStyles.UpperCase;
                        }
                        break;
                        case TagType.Lowercase:
                        {
                            if (tag.isClosing)
                                m_fontStyles &= ~FontStyles.LowerCase;
                            else
                                m_fontStyles |= FontStyles.LowerCase;
                        }
                        break;
                        case TagType.Size:
                            if (!tag.isClosing)
                            {
                                switch (tag.value.unit)
                                {
                                    case TagUnitType.Pixels:
                                        m_currentFontSize = tag.value.NumericalValue;
                                        m_sizeStack.Add(m_currentFontSize);
                                        return;
                                    case TagUnitType.FontUnits:
                                        m_currentFontSize = textBaseConfiguration.fontSize * tag.value.NumericalValue;
                                        m_sizeStack.Add(m_currentFontSize);
                                        return;
                                    case TagUnitType.Percentage:
                                        m_currentFontSize = textBaseConfiguration.fontSize * tag.value.NumericalValue / 100;
                                        m_sizeStack.Add(m_currentFontSize);
                                        return;
                                }
                            }
                            else
                                m_currentFontSize = m_sizeStack.RemoveExceptRoot();
                            return;
                        case TagType.Space:
                            switch (tag.value.unit)
                            {
                                case TagUnitType.Pixels:
                                    m_xAdvance += (textBaseConfiguration.isOrthographic ? 1 : 0.1f) * tag.value.NumericalValue;
                                    return;
                                case TagUnitType.FontUnits:
                                    m_xAdvance += (textBaseConfiguration.isOrthographic ? 1 : 0.1f) * m_currentFontSize * tag.value.NumericalValue;
                                    return;
                                case TagUnitType.Percentage:
                                    // Not applicable
                                    return;
                            }
                            return;
                        case TagType.Align:
                            if (!tag.isClosing)
                            {
                                switch (tag.value.stringValue)
                                {
                                    case StringValue.left:  // <align=left>
                                        m_lineJustification = HorizontalAlignmentOptions.Left;
                                        break;
                                    case StringValue.right:  // <align=right>
                                        m_lineJustification = HorizontalAlignmentOptions.Right;
                                        break;
                                    case StringValue.center:  // <align=center>
                                        m_lineJustification = HorizontalAlignmentOptions.Center;
                                        break;
                                    case StringValue.justified:  // <align=justified>
                                        m_lineJustification = HorizontalAlignmentOptions.Justified;
                                        break;
                                }
                                m_lineJustificationStack.Add(m_lineJustification);
                                return;
                            }
                            else
                                m_lineJustification = m_lineJustificationStack.RemoveExceptRoot();
                            return;
                        case TagType.CSpace:
                            if (!tag.isClosing)
                            {
                                switch (tag.value.unit)
                                {
                                    case TagUnitType.Pixels:
                                        m_cSpacing = (textBaseConfiguration.isOrthographic ? 1 : 0.1f) * tag.value.NumericalValue;
                                        return;
                                    case TagUnitType.FontUnits:
                                        m_cSpacing = (textBaseConfiguration.isOrthographic ? 1 : 0.1f) * m_currentFontSize * tag.value.NumericalValue;
                                        return;
                                    case TagUnitType.Percentage:
                                        return;
                                }
                            }
                            else
                                m_cSpacing = 0;
                            return;
                        case TagType.Mspace:
                            if (!tag.isClosing)
                            {
                                switch (tag.value.unit)
                                {
                                    case TagUnitType.Pixels:
                                        m_monoSpacing = (textBaseConfiguration.isOrthographic ? 1 : 0.1f) * tag.value.NumericalValue;
                                        return;
                                    case TagUnitType.FontUnits:
                                        m_monoSpacing = (textBaseConfiguration.isOrthographic ? 1 : 0.1f) * m_currentFontSize * tag.value.NumericalValue;
                                        return;
                                    case TagUnitType.Percentage:
                                        return;
                                }
                            }
                            else
                                m_monoSpacing = 0;
                            return;
                        case TagType.Indent:
                            if (!tag.isClosing)
                            {
                                switch (tag.value.unit)
                                {
                                    case TagUnitType.Pixels:
                                        m_tagIndent = (textBaseConfiguration.isOrthographic ? 1 : 0.1f) * tag.value.NumericalValue;
                                        break;
                                    case TagUnitType.FontUnits:
                                        m_tagIndent = (textBaseConfiguration.isOrthographic ? 1 : 0.1f) * m_currentFontSize * tag.value.NumericalValue;
                                        break;
                                    case TagUnitType.Percentage:
                                        break;
                                }
                                m_indentStack.Add(m_tagIndent);
                                m_xAdvance = m_tagIndent;
                            }
                            else
                                m_tagIndent = m_indentStack.RemoveExceptRoot();
                            return;
                        case TagType.LineIndent:
                            if (!tag.isClosing)
                            {
                                switch (tag.value.unit)
                                {
                                    case TagUnitType.Pixels:
                                        m_tagLineIndent = (textBaseConfiguration.isOrthographic ? 1 : 0.1f) * tag.value.NumericalValue;
                                        break;
                                    case TagUnitType.FontUnits:
                                        m_tagLineIndent = (textBaseConfiguration.isOrthographic ? 1 : 0.1f) * m_currentFontSize * tag.value.NumericalValue;
                                        break;
                                    case TagUnitType.Percentage:
                                        break;
                                }
                                m_xAdvance += m_tagLineIndent;
                            }
                            else
                                m_tagLineIndent = 0;
                            return;
                        case TagType.VOffset:
                            if (!tag.isClosing)
                            {
                                switch (tag.value.unit)
                                {
                                    case TagUnitType.Pixels:
                                        m_baselineOffset = (textBaseConfiguration.isOrthographic ? 1 : 0.1f) * tag.value.NumericalValue;
                                        break;
                                    case TagUnitType.FontUnits:
                                        m_baselineOffset = (textBaseConfiguration.isOrthographic ? 1 : 0.1f) * m_currentFontSize * tag.value.NumericalValue;
                                        break;
                                    case TagUnitType.Percentage:
                                        break;
                                }
                                m_baselineOffsetStack.Add(m_baselineOffset);
                            }
                            else
                                m_baselineOffset = m_baselineOffsetStack.RemoveExceptRoot();
                            return;
                    }
                }
            }
        }
    }
}

