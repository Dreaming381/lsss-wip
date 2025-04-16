using System;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;

namespace Latios.Calligraphics
{
    internal static class GlyphGeneration
    {
        internal static unsafe void CreateRenderGlyphs(ref ShapeStream.Reader shapeStream,
                                                       ref DynamicBuffer<RenderGlyph>  renderGlyphs,
                                                       ref GlyphMappingWriter mappingWriter,
                                                       in DynamicBuffer<CalliByte>     calliBytesBuffer,
                                                       in TextBaseConfiguration textBaseConfiguration,
                                                       ReadOnlySpan<TextColorGradient> textColorGradients,
                                                       in GlyphTable glyphTable)
        {
            //Debug.Log("CreateRenderGlyphs");
            renderGlyphs.Clear();
            var shapeStreamEntity = shapeStream.ReadEntity();
            if (shapeStreamEntity.shapeCount == 0)
                return;
            renderGlyphs.EnsureCapacity(shapeStreamEntity.glyphCount);

            Span<XMLTag> xmlTagBuffer = stackalloc XMLTag[shapeStreamEntity.xmlTagCount];
            for (int i = 0; i < shapeStreamEntity.xmlTagCount; i++)
                xmlTagBuffer[i] = shapeStream.ReadXmlTag();

            var calliString = new CalliString(calliBytesBuffer);
            var characters  = calliString.GetEnumerator();

            var layoutConfig = new LayoutConfig(in textBaseConfiguration);

            XMLTag currentTag                   = default;
            int    tagsCounter                  = 0;
            int    nextSegmentEndID             = xmlTagBuffer.Length > 0 ? xmlTagBuffer[tagsCounter].startID : calliString.Length;
            int    cleanedSegmentLength         = nextSegmentEndID - currentTag.endID;
            int    richTextOffset               = 0;
            int    nextTagPositionInCleanedText = cleanedSegmentLength;
            //Debug.Log($"{currentTag.tagType} {cleanedSegmentLength} {nextTagPositionInCleanedText}");

            int                    characterCount                                  = 0;
            int                    lastWordStartCharacterGlyphIndex                = 0;
            FixedList512Bytes<int> characterGlyphIndicesWithPreceedingSpacesInLine = default;
            int                    accumulatedSpaces                               = 0;
            int                    startOfLineGlyphIndex                           = 0;
            int                    lastCommittedStartOfLineGlyphIndex              = -1;
            int                    lineCount                                       = 0;
            bool                   isLineStart                                     = true;
            float                  currentLineHeight                               = 0f;
            float                  ascentLineDelta                                 = 0;
            float                  decentLineDelta                                 = 0;
            float                  accumulatedVerticalOffset                       = 0f;
            float                  maxLineAscender                                 = float.MinValue;
            float                  maxLineDescender                                = float.MaxValue;

            // Calculate the scale of the font based on selected font size and sampling point size.
            // baseScale is calculated using the font asset assigned to the text object.
            float baseScale           = textBaseConfiguration.fontSize / textBaseConfiguration.samplingSize * (textBaseConfiguration.isOrthographic ? 1 : 0.1f);
            float currentElementScale = baseScale;
            float currentEmScale      = textBaseConfiguration.fontSize * 0.01f * (textBaseConfiguration.isOrthographic ? 1 : 0.1f);

            float topAnchor    = float.NaN;  //GetTopAnchorForConfig(ref currentFont, textBaseConfiguration.verticalAlignment, baseScale);
            float bottomAnchor = float.NaN;  //GetBottomAnchorForConfig(ref currentFont, textBaseConfiguration.verticalAlignment, baseScale);

            Unicode.Rune currentRune, previousRune = Unicode.BadRune;  //input text unicode

            int totalGlyphIndex = 0;
            for (int shapeIndex = 0; shapeIndex < shapeStreamEntity.shapeCount; shapeIndex++)
            {
                var     shapeStreamShape             = shapeStream.ReadShape();
                ref var fontConstants                = ref *shapeStreamShape.fontConstants;
                ref var fontDirectionConstants       = ref *shapeStreamShape.fontDirectionConstants;
                ref var fontDirectionScriptConstants = ref *shapeStreamShape.fontDirectionScriptConstants;
                if (math.isnan(topAnchor))
                {
                    topAnchor = GetTopAnchorForConfig(in fontConstants,
                                                      in fontDirectionConstants,
                                                      in fontDirectionScriptConstants,
                                                      textBaseConfiguration.verticalAlignment,
                                                      baseScale);
                    bottomAnchor = GetBottomAnchorForConfig(in fontConstants,
                                                            in fontDirectionConstants,
                                                            in fontDirectionScriptConstants,
                                                            textBaseConfiguration.verticalAlignment,
                                                            baseScale);
                }

                for (int glyphIndexInShape = 0; glyphIndexInShape < shapeStreamShape.glyphCount; glyphIndexInShape++, totalGlyphIndex++)
                {
                    var glyphOTF = shapeStream.ReadGlyph();

                    var cluster = (int)glyphOTF.cluster;  //cluster is char index in cleaned text = aligned with glyphOTF buffer
                    while (cluster >= nextTagPositionInCleanedText)
                    {
                        if (tagsCounter < xmlTagBuffer.Length)
                        {
                            currentTag      = xmlTagBuffer[tagsCounter++];
                            richTextOffset += currentTag.Length;
                            layoutConfig.Update(ref currentTag, textBaseConfiguration, textColorGradients);
                            nextSegmentEndID             = tagsCounter < xmlTagBuffer.Length ? xmlTagBuffer[tagsCounter].startID - 1 : calliString.Length;
                            cleanedSegmentLength         = nextSegmentEndID - currentTag.endID;
                            nextTagPositionInCleanedText = cluster + cleanedSegmentLength;

                            //Debug.Log($"{currentTag.tagType} {cleanedSegmentLength} {nextTagPositionInCleanedText}");
                        }
                    }
                    // need to add richTextOffset to fetch correct char from richtext buffer.
                    // note: upper/lowercase is not applied in richtextBuffer (is only applied to cleaned text just before shaping)...should not cause any issues here
                    characters.GotoByteIndex(richTextOffset + cluster);
                    currentRune = characters.Current;

                    if (lineCount == 0)
                        topAnchor = GetTopAnchorForConfig(in fontConstants,
                                                          in fontDirectionConstants,
                                                          in fontDirectionScriptConstants,
                                                          textBaseConfiguration.verticalAlignment,
                                                          baseScale,
                                                          topAnchor);
                    bottomAnchor = GetBottomAnchorForConfig(in fontConstants,
                                                            in fontDirectionConstants,
                                                            in fontDirectionScriptConstants,
                                                            textBaseConfiguration.verticalAlignment,
                                                            baseScale,
                                                            bottomAnchor);

                    #region Look up Character Data
                    // Todo: This is not the correct key access.
                    if (!glyphTable.glyphHashToIdMap.TryGetValue(glyphOTF.codepoint, out var id))
                    {
                        UnityEngine.Debug.LogError($"Glyph {currentRune.value} has not yet been added to texture atlas");
                        continue;
                    }
                    var glyphEntry = glyphTable.GetEntry(id);

                    // review how to handle glyphOTF.codepoint = 0 (not defined glyph) which is retured for example for tab stop (9)
                    // see here why: https://github.com/harfbuzz/harfbuzz/commit/81ef4f407d9c7bd98cf62cef951dc538b13442eb#commitcomment-9469767
                    // should not be rendered, but xAdvance should be processed

                    // Cache glyph metrics
                    var x_bearing   = glyphEntry.xBearing;
                    var y_bearing   = glyphEntry.yBearing;
                    var glyphHeight = glyphEntry.height;
                    var glyphWidth  = glyphEntry.width;

                    float adjustedScale      = layoutConfig.m_currentFontSize / textBaseConfiguration.samplingSize * (textBaseConfiguration.isOrthographic ? 1 : 0.1f);
                    float elementAscentLine  = fontDirectionConstants.ascender;
                    float elementDescentLine = fontDirectionConstants.descender;

                    //synthesize superscript and subscript redundant to opentype feature set during shaping.
                    //only purpose is to simulate missing subscript glyphs, but unclear how to determine this
                    float fontScaleMultiplier     = 1;
                    float m_subAndSupscriptOffset = 0;
                    //if ((layoutConfiguration.m_fontStyles & FontStyles.Subscript) == FontStyles.Subscript && !currentRune.IsDigit())
                    //{
                    //    //Debug.Log($"{currentFont.subScriptEmXSize} {currentFont.subScriptEmYOffset} {adjustedScale}");
                    //    fontScaleMultiplier = currentFont.subScriptEmXSize * adjustedScale;
                    //    m_SubAndSupscriptOffset = -currentFont.subScriptEmYOffset * adjustedScale;
                    //}
                    //else if ((layoutConfiguration.m_fontStyles & FontStyles.Superscript) == FontStyles.Superscript && !currentRune.IsDigit())
                    //{
                    //    fontScaleMultiplier = currentFont.superScriptEmXSize * adjustedScale;
                    //    m_SubAndSupscriptOffset = currentFont.superScriptEmYOffset * adjustedScale;
                    //}

                    currentElementScale  = adjustedScale * fontScaleMultiplier;
                    float baselineOffset = fontDirectionScriptConstants.baseLine * adjustedScale * fontScaleMultiplier;
                    #endregion

                    // Optimization to avoid calling this more than once per character.
                    bool isWhiteSpace = currentRune.value <= 0xFFFF && currentRune.IsWhiteSpace();

                    // Handle Mono Spacing
                    #region Handle Mono Spacing
                    float monoAdvance = 0;
                    if (layoutConfig.m_monoSpacing != 0)
                    {
                        monoAdvance =
                            (layoutConfig.m_monoSpacing / 2 - (glyphWidth / 2 + x_bearing) * currentElementScale);  // * (1 - charWidthAdjDelta);
                        layoutConfig.m_xAdvance += monoAdvance;
                    }
                    #endregion

                    // Set Padding based on selected font style
                    #region Handle Style Padding
                    float boldSpacingAdjustment = 0;
                    float style_padding         = 0;
                    //if bold is requested and current font is not bold (=it has not been found), then simulate bold
                    bool simulateBold = layoutConfig.fontWeight >= FontWeight.Bold && fontConstants.weight < (int)FontWeight.Bold * 100f;
                    if (simulateBold)
                    {
                        //Debug.Log($"Simulate Bold {currentFontAssetRef.weight} {(int)FontWeight.Bold}");
                        style_padding         = 0;
                        boldSpacingAdjustment = fontConstants.boldStyleSpacing;
                    }
                    #endregion Handle Style Padding

                    // Determine the position of the vertices of the Character or Sprite.
                    #region Calculate Vertices Position
                    var renderGlyph = new RenderGlyph();

                    // top left is used to position bottom left and top right
                    float2 topLeft;
                    topLeft.x = layoutConfig.m_xAdvance + (x_bearing * layoutConfig.m_fxScale - shapeStreamShape.materialPadding - style_padding + glyphOTF.xOffset) *
                                currentElementScale;
                    topLeft.y = baselineOffset + (y_bearing + shapeStreamShape.materialPadding + glyphOTF.yOffset) * currentElementScale + layoutConfig.m_baselineOffset +
                                m_subAndSupscriptOffset;

                    float2 bottomLeft;
                    bottomLeft.x = topLeft.x;
                    bottomLeft.y = topLeft.y - ((glyphHeight + shapeStreamShape.materialPadding * 2) * currentElementScale);

                    float2 topRight;
                    topRight.x = bottomLeft.x + (glyphWidth * layoutConfig.m_fxScale + shapeStreamShape.materialPadding * 2 + style_padding * 2) * currentElementScale;
                    topRight.y = topLeft.y;

                    float2 bottomRight;
                    bottomRight.x = topRight.x;
                    bottomRight.y = bottomLeft.y;

                    // Bottom right unused
                    #endregion

                    #region Setup UVA
                    //var glyphRect = glyphBlob.glyphRect;
                    //float2 blUVA, tlUVA, trUVA, brUVA;
                    //blUVA.x = (glyphRect.x - currentFont.materialPadding - style_padding) / currentFont.atlasWidth;
                    //blUVA.y = (glyphRect.y - currentFont.materialPadding - style_padding) / currentFont.atlasHeight;
                    //
                    //tlUVA.x = blUVA.x;
                    //tlUVA.y = (glyphRect.y + currentFont.materialPadding + style_padding + glyphRect.height) / currentFont.atlasHeight;
                    //
                    //trUVA.x = (glyphRect.x + currentFont.materialPadding + style_padding + glyphRect.width) / currentFont.atlasWidth;
                    //trUVA.y = tlUVA.y;
                    //
                    //brUVA.x = trUVA.x;
                    //brUVA.y = blUVA.y;
                    //
                    //renderGlyph.blUVA = blUVA;
                    //renderGlyph.trUVA = trUVA;

                    // We use the full padded glyph texture rect in the atlas, which means in normalized coordinates, we just use (0, 0) and (1, 1).
                    // Todo: If this is correct, can we maybe pack other data into the RenderGlyph and then do the replacement assuming these coordinates?
                    renderGlyph.blUVA = 0f;
                    renderGlyph.blUVB = 1f;
                    #endregion

                    #region Setup UVB
                    //Setup UV2 based on Character Mapping Options Selected
                    //m_horizontalMapping case TextureMappingOptions.Character
                    float2 blUVC, tlUVC, trUVC, brUVC;
                    blUVC.x = 0;
                    tlUVC.x = 0;
                    trUVC.x = 1;
                    brUVC.x = 1;

                    //m_verticalMapping case case TextureMappingOptions.Character
                    blUVC.y = 0;
                    tlUVC.y = 1;
                    trUVC.y = 1;
                    brUVC.y = 0;

                    renderGlyph.blUVB = blUVC;
                    renderGlyph.tlUVB = tlUVC;
                    renderGlyph.trUVB = trUVC;
                    renderGlyph.brUVB = brUVC;
                    #endregion

                    #region Setup Color

                    if (layoutConfig.useGradient)  //&& !isColorGlyph)
                    {
                        var gradient        = layoutConfig.m_gradient;
                        renderGlyph.blColor = gradient.bottomLeft;
                        renderGlyph.tlColor = gradient.topLeft;
                        renderGlyph.trColor = gradient.topRight;
                        renderGlyph.brColor = gradient.bottomRight;
                        //if (m_ColorGradientPresetIsTinted)
                        //{
                        //    textInfo.textElementInfo[m_CharacterCount].vertexBottomLeft.color *= m_ColorGradientPreset.bottomLeft;
                        //    textInfo.textElementInfo[m_CharacterCount].vertexTopLeft.color *= m_ColorGradientPreset.topLeft;
                        //    textInfo.textElementInfo[m_CharacterCount].vertexTopRight.color *= m_ColorGradientPreset.topRight;
                        //    textInfo.textElementInfo[m_CharacterCount].vertexBottomRight.color *= m_ColorGradientPreset.bottomRight;
                        //}
                        //else
                        //{
                        //    textInfo.textElementInfo[m_CharacterCount].vertexBottomLeft.color = TextGeneratorUtilities.MinAlpha(m_ColorGradientPreset.bottomLeft, vertexColor);
                        //    textInfo.textElementInfo[m_CharacterCount].vertexTopLeft.color = TextGeneratorUtilities.MinAlpha(m_ColorGradientPreset.topLeft, vertexColor);
                        //    textInfo.textElementInfo[m_CharacterCount].vertexTopRight.color = TextGeneratorUtilities.MinAlpha(m_ColorGradientPreset.topRight, vertexColor);
                        //    textInfo.textElementInfo[m_CharacterCount].vertexBottomRight.color = TextGeneratorUtilities.MinAlpha(m_ColorGradientPreset.bottomRight, vertexColor);
                        //}
                    }
                    else
                    {
                        renderGlyph.blColor = layoutConfig.m_htmlColor;
                        renderGlyph.tlColor = layoutConfig.m_htmlColor;
                        renderGlyph.trColor = layoutConfig.m_htmlColor;
                        renderGlyph.brColor = layoutConfig.m_htmlColor;
                    }
                    #endregion

                    #region Pack Scale into renderGlyph.scale
                    var scale = layoutConfig.m_currentFontSize;
                    if (simulateBold)
                        scale *= -1;

                    renderGlyph.scale = scale;
                    #endregion

                    // Check if we need to Shear the rectangles for Italic styles
                    #region Handle Italic & Shearing
                    float bottomShear = 0f;
                    //if italic is requested and current font is not italic (=it has not been found), then simulate italic
                    bool simulateItalic = (layoutConfig.m_fontStyles & FontStyles.Italic) == FontStyles.Italic && !fontConstants.isItalic;
                    if (simulateItalic)
                    {
                        //Debug.Log($"Simulate Italic {currentFontAssetRef.isItalic}");
                        // Shift Top vertices forward by half (Shear Value * height of character) and Bottom vertices back by same amount.
                        float shear_value = fontConstants.italicsStyleSlant * 0.01f;
                        float midPoint    = ((fontConstants.capHeight - (fontDirectionScriptConstants.baseLine + layoutConfig.m_baselineOffset + m_subAndSupscriptOffset)) / 2) *
                                            fontScaleMultiplier;
                        float topShear = shear_value * ((y_bearing + shapeStreamShape.materialPadding + style_padding - midPoint) * currentElementScale);
                        bottomShear    = shear_value *
                                         ((y_bearing - glyphHeight - shapeStreamShape.materialPadding - style_padding - midPoint) *
                                          currentElementScale);

                        topLeft.x     += topShear;
                        bottomLeft.x  += bottomShear;
                        topRight.x    += topShear;
                        bottomRight.x += bottomShear;
                    }
                    #endregion Handle Italics & Shearing

                    // Handle Character FX Rotation
                    #region Handle Character FX Rotation
                    if (layoutConfig.m_fxRotationAngleCCW_degree != 0f)
                    {
                        var center = (bottomLeft + topRight) * 0.5f;
                        math.sincos(math.radians(layoutConfig.m_fxRotationAngleCCW_degree), out var s, out var c);
                        float2 rotation = new float2(c, s);
                        topLeft         = LatiosMath.ComplexMul(topLeft - center, rotation) + center;
                        bottomLeft      = LatiosMath.ComplexMul(bottomLeft - center, rotation) + center;
                        topRight        = LatiosMath.ComplexMul(topRight - center, rotation) + center;
                        bottomRight     = LatiosMath.ComplexMul(bottomRight - center, rotation) + center;
                    }
                    #endregion

                    #region Store vertex information for the character or sprite.
                    if (isLineStart)
                    {
                        mappingWriter.AddLineStart(renderGlyphs.Length);
                        mappingWriter.AddWordStart(renderGlyphs.Length);
                    }
                    renderGlyph.blPosition = bottomLeft;
                    renderGlyph.tlPosition = topLeft;
                    renderGlyph.brPosition = bottomRight;
                    renderGlyph.trPosition = topRight;
                    if (Hint.Likely(currentRune.value != 10))  //do not render LF
                    {
                        renderGlyphs.Add(renderGlyph);
                        mappingWriter.AddCharNoTags(characterCount - 1, true);
                        mappingWriter.AddCharWithTags(totalGlyphIndex, true);
                        mappingWriter.AddBytes(characters.NextByteIndex, currentRune.LengthInUtf8Bytes(), true);
                    }
                    #endregion

                    // Compute text metrics
                    #region Compute Ascender & Descender values
                    // Element Ascender in line space
                    float elementAscender = elementAscentLine * currentElementScale + layoutConfig.m_baselineOffset + m_subAndSupscriptOffset;

                    // Element Descender in line space
                    float elementDescender = elementDescentLine * currentElementScale + layoutConfig.m_baselineOffset + m_subAndSupscriptOffset;

                    float adjustedAscender  = elementAscender;
                    float adjustedDescender = elementDescender;

                    // Max line ascender and descender in line space
                    if (isLineStart || isWhiteSpace == false)
                    {
                        // Special handling for Superscript and Subscript where we use the unadjusted line ascender and descender
                        if (m_subAndSupscriptOffset != 0)  //To-Do: review (also voffset affecting m_baselineOffset), effect not clear.
                        {
                            adjustedAscender  = math.max((elementAscender - m_subAndSupscriptOffset) / fontScaleMultiplier, adjustedAscender);
                            adjustedDescender = math.min((elementDescender - m_subAndSupscriptOffset) / fontScaleMultiplier, adjustedDescender);
                        }
                        maxLineAscender  = math.max(adjustedAscender, maxLineAscender);
                        maxLineDescender = math.min(adjustedDescender, maxLineDescender);
                    }
                    #endregion

                    #region XAdvance, Tabulation & Stops
                    if (currentRune.value == 9)
                    {
                        float tabSize           = fontConstants.tabWidth * fontConstants.tabMultiple * currentElementScale;
                        float tabs              = math.ceil(layoutConfig.m_xAdvance / tabSize) * tabSize;
                        layoutConfig.m_xAdvance = tabs > layoutConfig.m_xAdvance ? tabs : layoutConfig.m_xAdvance + tabSize;
                    }
                    else if (layoutConfig.m_monoSpacing != 0)
                    {
                        float monoAdjustment     = layoutConfig.m_monoSpacing - monoAdvance;
                        layoutConfig.m_xAdvance += (monoAdjustment + ((fontConstants.regularStyleSpacing) * currentEmScale) + layoutConfig.m_cSpacing);
                        if (isWhiteSpace || currentRune.value == 0x200B)
                            layoutConfig.m_xAdvance += textBaseConfiguration.wordSpacing * currentEmScale;
                    }
                    else
                    {
                        layoutConfig.m_xAdvance += (glyphOTF.xAdvance * layoutConfig.m_fxScale) * currentElementScale +
                                                   (fontConstants.regularStyleSpacing + boldSpacingAdjustment) * currentEmScale + layoutConfig.m_cSpacing;

                        if (isWhiteSpace || currentRune.value == 0x200B)
                            layoutConfig.m_xAdvance += textBaseConfiguration.wordSpacing * currentEmScale;
                    }
                    #endregion XAdvance, Tabulation & Stops

                    #region Check for Line Feed and Last Character
                    if (isLineStart)
                        isLineStart   = false;
                    currentLineHeight = (fontDirectionConstants.ascender - fontDirectionConstants.descender) * baseScale;
                    ascentLineDelta   = maxLineAscender - fontDirectionConstants.ascender * baseScale;
                    decentLineDelta   = fontDirectionConstants.descender * baseScale - maxLineDescender;
                    //if (currentRune.value == 10 || currentRune.value == 11 || currentRune.value == 0x03 || currentRune.value == 0x2028 ||
                    //    currentRune.value == 0x2029 || textConfiguration.m_characterCount == calliString.Length - 1)
                    if (currentRune.value == 10)
                    {
                        var glyphsLine   = renderGlyphs.AsNativeArray().GetSubArray(startOfLineGlyphIndex, renderGlyphs.Length - startOfLineGlyphIndex);
                        var overrideMode = layoutConfig.m_lineJustification;
                        if ((overrideMode) == HorizontalAlignmentOptions.Justified)
                        {
                            // Don't perform justified spacing for the last line in the paragraph.
                            overrideMode = HorizontalAlignmentOptions.Left;
                        }
                        ApplyHorizontalAlignmentToGlyphs(ref glyphsLine,
                                                         ref characterGlyphIndicesWithPreceedingSpacesInLine,
                                                         textBaseConfiguration.maxLineWidth,
                                                         overrideMode);
                        startOfLineGlyphIndex = renderGlyphs.Length;
                        if (lineCount > 0)
                        {
                            accumulatedVerticalOffset += currentLineHeight + ascentLineDelta;
                            if (lastCommittedStartOfLineGlyphIndex != startOfLineGlyphIndex)
                            {
                                ApplyVerticalOffsetToGlyphs(ref glyphsLine, accumulatedVerticalOffset);
                                lastCommittedStartOfLineGlyphIndex = startOfLineGlyphIndex;
                            }
                        }
                        accumulatedVerticalOffset += decentLineDelta;
                        //apply user configurable line and paragraph spacing
                        accumulatedVerticalOffset +=
                            (textBaseConfiguration.lineSpacing +
                             (currentRune.value == 10 || currentRune.value == 0x2029 ? textBaseConfiguration.paragraphSpacing : 0)) * currentEmScale;

                        //reset line status
                        maxLineAscender  = float.MinValue;
                        maxLineDescender = float.MaxValue;

                        lineCount++;
                        isLineStart  = true;
                        bottomAnchor = GetBottomAnchorForConfig(in fontConstants,
                                                                in fontDirectionConstants,
                                                                in fontDirectionScriptConstants,
                                                                textBaseConfiguration.verticalAlignment,
                                                                baseScale);

                        layoutConfig.m_xAdvance = 0 + layoutConfig.m_tagIndent;
                        previousRune            = currentRune;
                        continue;
                    }
                    #endregion

                    #region Word Wrapping
                    // Handle word wrap
                    if (textBaseConfiguration.maxLineWidth < float.MaxValue &&
                        textBaseConfiguration.maxLineWidth > 0 &&
                        layoutConfig.m_xAdvance > textBaseConfiguration.maxLineWidth)
                    {
                        bool dropSpace = false;

                        if (currentRune.value == 32 && previousRune.value != 32)
                        {
                            // What pushed us past the line width was a space character.
                            // The previous character was not a space, and we don't
                            // want to render this character at the start of the next line.
                            // We drop this space character instead and allow the next
                            // character to line-wrap, space or not.
                            dropSpace = true;
                            accumulatedSpaces--;
                        }

                        var yOffsetChange = 0f;  //font.lineHeight * currentElementScale;
                        var xOffsetChange = renderGlyphs[lastWordStartCharacterGlyphIndex].blPosition.x - bottomShear - layoutConfig.m_tagIndent;
                        if (xOffsetChange > 0 && !dropSpace)  // Always allow one visible character
                        {
                            // Finish line based on alignment
                            var glyphsLine = renderGlyphs.AsNativeArray().GetSubArray(startOfLineGlyphIndex,
                                                                                      lastWordStartCharacterGlyphIndex - startOfLineGlyphIndex);
                            ApplyHorizontalAlignmentToGlyphs(ref glyphsLine,
                                                             ref characterGlyphIndicesWithPreceedingSpacesInLine,
                                                             textBaseConfiguration.maxLineWidth,
                                                             layoutConfig.m_lineJustification);

                            if (lineCount > 0)
                            {
                                accumulatedVerticalOffset += currentLineHeight + ascentLineDelta;
                                ApplyVerticalOffsetToGlyphs(ref glyphsLine, accumulatedVerticalOffset);
                                lastCommittedStartOfLineGlyphIndex = startOfLineGlyphIndex;
                            }
                            accumulatedVerticalOffset += decentLineDelta;  // Todo: Delta should be computed per glyph
                                                                           //apply user configurable line and paragraph spacing
                            accumulatedVerticalOffset += textBaseConfiguration.lineSpacing * currentEmScale;

                            //reset line status
                            maxLineAscender  = float.MinValue;
                            maxLineDescender = float.MaxValue;

                            startOfLineGlyphIndex = lastWordStartCharacterGlyphIndex;
                            isLineStart           = true;
                            lineCount++;

                            layoutConfig.m_xAdvance -= xOffsetChange;

                            // Adjust the vertices of the previous render glyphs in the word
                            var glyphPtr = (RenderGlyph*)renderGlyphs.GetUnsafePtr();
                            for (int i = lastWordStartCharacterGlyphIndex; i < renderGlyphs.Length; i++)
                            {
                                glyphPtr[i].blPosition.y -= yOffsetChange;
                                glyphPtr[i].blPosition.x -= xOffsetChange;
                                glyphPtr[i].trPosition.y -= yOffsetChange;
                                glyphPtr[i].trPosition.x -= xOffsetChange;
                            }
                        }
                    }
                    //Detect start of word
                    if (currentRune.value == 32 ||  //Space
                        currentRune.value == 9 ||  //Tab
                        currentRune.value == 45 ||  //Hyphen Minus
                        currentRune.value == 173 ||  //Soft hyphen
                        currentRune.value == 8203 ||  //Zero width space
                        currentRune.value == 8204 ||  //Zero width non-joiner
                        currentRune.value == 8205)  //Zero width joiner
                    {
                        lastWordStartCharacterGlyphIndex = renderGlyphs.Length;
                        mappingWriter.AddWordStart(renderGlyphs.Length);
                    }

                    if (glyphOTF.codepoint == 1)
                    {
                        accumulatedSpaces++;
                    }
                    #endregion
                    previousRune = currentRune;
                }
            }

            var finalGlyphsLine = renderGlyphs.AsNativeArray().GetSubArray(startOfLineGlyphIndex, renderGlyphs.Length - startOfLineGlyphIndex);
            {
                var overrideMode = layoutConfig.m_lineJustification;
                if (overrideMode == HorizontalAlignmentOptions.Justified)
                {
                    // Don't perform justified spacing for the last line.
                    overrideMode = HorizontalAlignmentOptions.Left;
                }
                ApplyHorizontalAlignmentToGlyphs(ref finalGlyphsLine, ref characterGlyphIndicesWithPreceedingSpacesInLine, textBaseConfiguration.maxLineWidth, overrideMode);
                if (lineCount > 0)
                {
                    accumulatedVerticalOffset += currentLineHeight;
                    ApplyVerticalOffsetToGlyphs(ref finalGlyphsLine, accumulatedVerticalOffset);
                }
            }
            lineCount++;
            ApplyVerticalAlignmentToGlyphs(ref renderGlyphs, topAnchor, bottomAnchor, accumulatedVerticalOffset, textBaseConfiguration.verticalAlignment);
        }

        static float GetTopAnchorForConfig(in FontConstants fontConstants,
                                           in FontDirectionConstants fontDirectionConstants,
                                           in FontDirectionScriptConstants fontDirectionScriptConstants,
                                           VerticalAlignmentOptions verticalMode,
                                           float baseScale,
                                           float oldValue = float.PositiveInfinity)
        {
            bool replace = oldValue == float.PositiveInfinity;
            switch (verticalMode)
            {
                case VerticalAlignmentOptions.TopBase: return 0f;
                case VerticalAlignmentOptions.MiddleTopAscentToBottomDescent:
                case VerticalAlignmentOptions.TopAscent: return baseScale *
                           math.max(fontDirectionConstants.ascender - fontDirectionScriptConstants.baseLine, math.select(oldValue, float.NegativeInfinity, replace));
                case VerticalAlignmentOptions.TopDescent: return baseScale * math.min(fontDirectionConstants.descender - fontDirectionScriptConstants.baseLine, oldValue);
                case VerticalAlignmentOptions.TopCap: return baseScale *
                           math.max(fontConstants.capHeight - fontDirectionScriptConstants.baseLine, math.select(oldValue, float.NegativeInfinity, replace));
                case VerticalAlignmentOptions.TopMean: return baseScale *
                           math.max(fontConstants.xHeight - fontDirectionScriptConstants.baseLine, math.select(oldValue, float.NegativeInfinity, replace));
                default: return 0f;
            }
        }

        static float GetBottomAnchorForConfig(in FontConstants fontConstants,
                                              in FontDirectionConstants fontDirectionConstants,
                                              in FontDirectionScriptConstants fontDirectionScriptConstants,
                                              VerticalAlignmentOptions verticalMode,
                                              float baseScale,
                                              float oldValue = float.PositiveInfinity)
        {
            bool replace = oldValue == float.PositiveInfinity;
            switch (verticalMode)
            {
                case VerticalAlignmentOptions.BottomBase: return 0f;
                case VerticalAlignmentOptions.BottomAscent: return baseScale *
                           math.max(fontDirectionConstants.ascender - fontDirectionScriptConstants.baseLine, math.select(oldValue, float.NegativeInfinity, replace));
                case VerticalAlignmentOptions.MiddleTopAscentToBottomDescent:
                case VerticalAlignmentOptions.BottomDescent: return baseScale * math.min(fontDirectionConstants.descender - fontDirectionScriptConstants.baseLine, oldValue);
                case VerticalAlignmentOptions.BottomCap: return baseScale *
                           math.max(fontConstants.capHeight - fontDirectionScriptConstants.baseLine, math.select(oldValue, float.NegativeInfinity, replace));
                case VerticalAlignmentOptions.BottomMean: return baseScale *
                           math.max(fontConstants.xHeight - fontDirectionScriptConstants.baseLine, math.select(oldValue, float.NegativeInfinity, replace));
                default: return 0f;
            }
        }

        static unsafe void ApplyHorizontalAlignmentToGlyphs(ref NativeArray<RenderGlyph> glyphs,
                                                            ref FixedList512Bytes<int>   characterGlyphIndicesWithPreceedingSpacesInLine,
                                                            float width,
                                                            HorizontalAlignmentOptions alignMode)
        {
            if ((alignMode) == HorizontalAlignmentOptions.Left)
            {
                characterGlyphIndicesWithPreceedingSpacesInLine.Clear();
                return;
            }

            var glyphsPtr = (RenderGlyph*)glyphs.GetUnsafePtr();
            if ((alignMode) == HorizontalAlignmentOptions.Center)
            {
                float offset = glyphsPtr[glyphs.Length - 1].trPosition.x / 2f;
                for (int i = 0; i < glyphs.Length; i++)
                {
                    glyphsPtr[i].blPosition.x -= offset;
                    glyphsPtr[i].trPosition.x -= offset;
                }
            }
            else if ((alignMode) == HorizontalAlignmentOptions.Right)
            {
                float offset = glyphsPtr[glyphs.Length - 1].trPosition.x;
                for (int i = 0; i < glyphs.Length; i++)
                {
                    glyphsPtr[i].blPosition.x -= offset;
                    glyphsPtr[i].trPosition.x -= offset;
                }
            }
            else  // Justified
            {
                float nudgePerSpace     = (width - glyphsPtr[glyphs.Length - 1].trPosition.x) / characterGlyphIndicesWithPreceedingSpacesInLine.Length;
                float accumulatedOffset = 0f;
                int   indexInIndices    = 0;
                for (int i = 0; i < glyphs.Length; i++)
                {
                    while (indexInIndices < characterGlyphIndicesWithPreceedingSpacesInLine.Length &&
                           characterGlyphIndicesWithPreceedingSpacesInLine[indexInIndices] == i)
                    {
                        accumulatedOffset += nudgePerSpace;
                        indexInIndices++;
                    }

                    glyphsPtr[i].blPosition.x += accumulatedOffset;
                    glyphsPtr[i].trPosition.x += accumulatedOffset;
                }
            }
            characterGlyphIndicesWithPreceedingSpacesInLine.Clear();
        }

        static unsafe void ApplyVerticalOffsetToGlyphs(ref NativeArray<RenderGlyph> glyphs, float accumulatedVerticalOffset)
        {
            for (int i = 0; i < glyphs.Length; i++)
            {
                var glyph           = glyphs[i];
                glyph.blPosition.y -= accumulatedVerticalOffset;
                glyph.trPosition.y -= accumulatedVerticalOffset;
                glyphs[i]           = glyph;
            }
        }

        static unsafe void ApplyVerticalAlignmentToGlyphs(ref DynamicBuffer<RenderGlyph> glyphs,
                                                          float topAnchor,
                                                          float bottomAnchor,
                                                          float accumulatedVerticalOffset,
                                                          VerticalAlignmentOptions alignMode)
        {
            var glyphsPtr = (RenderGlyph*)glyphs.GetUnsafePtr();
            switch (alignMode)
            {
                case VerticalAlignmentOptions.TopBase:
                    return;
                case VerticalAlignmentOptions.TopAscent:
                case VerticalAlignmentOptions.TopDescent:
                case VerticalAlignmentOptions.TopCap:
                case VerticalAlignmentOptions.TopMean:
                {
                    // Positions were calculated relative to the baseline.
                    // Shift everything down so that y = 0 is on the target line.
                    for (int i = 0; i < glyphs.Length; i++)
                    {
                        glyphsPtr[i].blPosition.y -= topAnchor;
                        glyphsPtr[i].trPosition.y -= topAnchor;
                    }
                    break;
                }
                case VerticalAlignmentOptions.BottomBase:
                case VerticalAlignmentOptions.BottomAscent:
                case VerticalAlignmentOptions.BottomDescent:
                case VerticalAlignmentOptions.BottomCap:
                case VerticalAlignmentOptions.BottomMean:
                {
                    float offset = accumulatedVerticalOffset - bottomAnchor;
                    for (int i = 0; i < glyphs.Length; i++)
                    {
                        glyphsPtr[i].blPosition.y += offset;
                        glyphsPtr[i].trPosition.y += offset;
                    }
                    break;
                }
                case VerticalAlignmentOptions.MiddleTopAscentToBottomDescent:
                {
                    float fullHeight = accumulatedVerticalOffset - bottomAnchor + topAnchor;
                    float offset     = fullHeight / 2f;
                    for (int i = 0; i < glyphs.Length; i++)
                    {
                        glyphsPtr[i].blPosition.y += offset;
                        glyphsPtr[i].trPosition.y += offset;
                    }
                    break;
                }
            }
        }
    }
}

