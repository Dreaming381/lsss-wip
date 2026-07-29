using System.IO;
using Latios.Calligraphics.HarfBuzz;
using Latios.Calligraphics.RichText;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;

namespace Latios.Calligraphics.Systems
{
    public partial struct GenerateGlyphsSystem
    {
        partial struct ShapeJob
        {
            // Resolves word-wrapping, hard line breaks, per-line horizontal alignment, and whole-block
            // vertical alignment for one entity's fully-shaped GlyphOTF sequence.
            //
            // Results are baked straight into outputString's xAdvance/yAdvance fields (reinterpreted as
            // fully-resolved, already-scaled pen-space deltas - see GlyphOTF) plus a per-entity starting
            // pen position. GenerateRenderGlyphsJob then only needs a cumulative sum over these to place
            // every glyph.
            //
            // A line-break (hard LF or word-wrap) is encoded as a delta to move to the next line's start
            // instead of continuing horizontally. Per-line horizontal alignment (Center/Right) and whole-
            // block vertical alignment are resolved once a line's/the text's full extent is known, then
            // patched into the first character's glyph's advance (or the pen start, for the very first line).
            unsafe void ApplyWordWrapAndLayout(ref UnsafeList<GlyphOTF> outputString,
                                               ref UnsafeText cleanedString,
                                               ref UnsafeList<XMLTag>   xmlTags,
                                               in UnsafeList<ShapeSpan> shapeSpans,
                                               in TextBaseConfiguration textBaseConfiguration,
                                               ref LayoutConfig layoutConfig,
                                               ref GlyphOTFStreamHeader header)
            {
                if (outputString.Length == 0)
                {
                    header.penStart = float2.zero;
                    return;
                }

                layoutConfig.Reset(textBaseConfiguration);

                XMLTag currentTag                   = default;
                int    tagsCounter                  = 0;
                int    nextSegmentEndID             = xmlTags.Length > 0 ? xmlTags[tagsCounter].startID : cleanedString.Length;
                int    cleanedSegmentLength         = nextSegmentEndID - currentTag.endID;
                int    nextTagPositionInCleanedText = cleanedSegmentLength;

                float currentEmScale = textBaseConfiguration.fontSize * 0.01f * (textBaseConfiguration.isOrthographic ? 1 : 0.1f);

                int shapeSpanIndex     = 0;
                var currentShapeSpan   = shapeSpans[0];
                int nextShapeSpanStart = shapeSpanIndex + 1 < shapeSpans.Length ? shapeSpans[shapeSpanIndex + 1].clusterStart : int.MaxValue;

                var glyphOTF0                    = outputString[0];
                var currentFaceIndex             = glyphOTF0.glyphKey.faceIndex;
                var currentFont                  = fontTable.GetOrCreateFont(currentFaceIndex, threadIndex);
                var currentFontSamplingPointSize = glyphOTF0.glyphKey.GetSamplingSize();
                currentFont.SetScale(currentFontSamplingPointSize, currentFontSamplingPointSize);
                currentFont.UpdateMetaData(currentShapeSpan.direction, currentShapeSpan.script, currentShapeSpan.language);

                float baseScale = textBaseConfiguration.fontSize / currentFontSamplingPointSize * (textBaseConfiguration.isOrthographic ? 1 : 0.1f);

                float topAnchor    = GetTopAnchorForConfig(ref currentFont, textBaseConfiguration.verticalAlignment, baseScale);
                float bottomAnchor = GetBottomAnchorForConfig(ref currentFont, textBaseConfiguration.verticalAlignment, baseScale);

                int lastWordStartGlyphIndex = 0;
                // Index of the current line's first glyph. Note: A word-wrap is only valid if lastWordStartGlyphIndex is greater than this.
                // Otherwise, the word is too long and should break the bounds.
                int   lineStartGlyphIndex       = 0;
                int   lineStartBreakGlyphIndex  = -1;  // Index whose xAdvance encodes "jump to this line's start"; -1 == the very first line (use penStart instead)
                bool  isFirstLine               = true;
                bool  isLineStart               = true;
                float accumulatedVerticalOffset = 0f;
                float maxLineAscender           = float.MinValue;
                float maxLineDescender          = float.MaxValue;
                float penX                      = 0f;
                float currentLineHeight         = 0f;
                float ascentLineDelta           = 0f;  // Needed for final line

                // Effectively penX, but without the alignment offsets
                float accumulatedLineWidth            = 0f;
                float accumulatedLineWidthAtWordStart = 0f;

                // Used to track the next line's position below this one
                float previousLineDecentLineDelta = 0f;

                // Glyph indices of plain spaces (value 32) seen so far on the current line. Justified alignment widens these
                // to fill the line.
                FixedList512Bytes<int> lineSpaceGlyphIndices = default;

                Unicode.Rune previousRune = Unicode.BadRune;

                for (int glyphIndex = 0; glyphIndex < outputString.Length; glyphIndex++)
                {
                    var glyphOTF = outputString[glyphIndex];
                    int cluster  = (int)glyphOTF.cluster;

                    bool shapeSpanChanged = false;
                    while (cluster >= nextShapeSpanStart)
                    {
                        shapeSpanIndex++;
                        currentShapeSpan   = shapeSpans[shapeSpanIndex];
                        nextShapeSpanStart = shapeSpanIndex + 1 < shapeSpans.Length ? shapeSpans[shapeSpanIndex + 1].clusterStart : int.MaxValue;
                        shapeSpanChanged   = true;
                    }

                    if (currentFaceIndex != glyphOTF.glyphKey.faceIndex || currentFontSamplingPointSize != glyphOTF.glyphKey.GetSamplingSize())
                    {
                        currentFaceIndex             = glyphOTF.glyphKey.faceIndex;
                        currentFont                  = fontTable.GetOrCreateFont(currentFaceIndex, threadIndex);
                        currentFontSamplingPointSize = glyphOTF.glyphKey.GetSamplingSize();
                        currentFont.SetScale(currentFontSamplingPointSize, currentFontSamplingPointSize);
                        currentFont.UpdateMetaData(currentShapeSpan.direction, currentShapeSpan.script, currentShapeSpan.language);
                    }
                    else if (shapeSpanChanged)
                    {
                        // Same font face, but direction/script/language changed.
                        currentFont.UpdateMetaData(currentShapeSpan.direction, currentShapeSpan.script, currentShapeSpan.language);
                    }
                    glyphOTF.baseline        = currentFont.baseLine;
                    outputString[glyphIndex] = glyphOTF;

                    while (tagsCounter < xmlTags.Length && cluster >= nextTagPositionInCleanedText)
                    {
                        currentTag = xmlTags[tagsCounter++];
                        layoutConfig.Update(ref currentTag, in textBaseConfiguration);
                        nextSegmentEndID             = tagsCounter < xmlTags.Length ? xmlTags[tagsCounter].startID - 1 : cleanedString.Length;
                        cleanedSegmentLength         = nextSegmentEndID - currentTag.endID;
                        nextTagPositionInCleanedText = cluster + cleanedSegmentLength;
                    }

                    int decodeIndex = cluster;
                    Unicode.Utf8ToUcs(out Unicode.Rune currentRune, cleanedString.GetUnsafePtr(), ref decodeIndex, cleanedString.Length);

                    if (isFirstLine)
                        topAnchor = GetTopAnchorForConfig(ref currentFont, textBaseConfiguration.verticalAlignment, baseScale, topAnchor);
                    bottomAnchor  = GetBottomAnchorForConfig(ref currentFont, textBaseConfiguration.verticalAlignment, baseScale, bottomAnchor);

                    bool  isWhiteSpace        = currentRune.value <= 0xFFFF && currentRune.IsWhiteSpace();
                    float currentElementScale = layoutConfig.m_currentFontSize / currentFontSamplingPointSize * (textBaseConfiguration.isOrthographic ? 1 : 0.1f);

                    float elementAscender  = currentFont.fontExtents.ascender * currentElementScale + layoutConfig.m_baselineOffset;
                    float elementDescender = currentFont.fontExtents.descender * currentElementScale + layoutConfig.m_baselineOffset;
                    if (isLineStart || !isWhiteSpace)
                    {
                        maxLineAscender  = math.max(elementAscender, maxLineAscender);
                        maxLineDescender = math.min(elementDescender, maxLineDescender);
                    }

                    currentLineHeight     = (currentFont.fontExtents.ascender - currentFont.fontExtents.descender) * baseScale;
                    ascentLineDelta       = maxLineAscender - currentFont.fontExtents.ascender * baseScale;
                    float decentLineDelta = currentFont.fontExtents.descender * baseScale - maxLineDescender;
                    isLineStart           = false;

                    if (currentRune.value == 10)
                    {
                        // Hard line feed. This glyph is never rendered downstream so its own advance is fully
                        // repurposed to encode the jump to the next line.
                        var hardLineFeedJustification = layoutConfig.m_lineJustification == HorizontalAlignmentOptions.Justified ?
                                                        HorizontalAlignmentOptions.Left : layoutConfig.m_lineJustification;
                        penX += ApplyHorizontalAlignmentShift(ref outputString, ref lineSpaceGlyphIndices, lineStartBreakGlyphIndex,
                                                              int.MaxValue, accumulatedLineWidth, textBaseConfiguration.maxLineWidth, hardLineFeedJustification, ref header);

                        float verticalDrop = previousLineDecentLineDelta + (textBaseConfiguration.lineSpacing + textBaseConfiguration.paragraphSpacing) * currentEmScale;
                        if (!isFirstLine)
                            verticalDrop += currentLineHeight + ascentLineDelta;
                        ApplyVerticalDrop(ref outputString, lineStartBreakGlyphIndex, verticalDrop, ref header);
                        previousLineDecentLineDelta = decentLineDelta;

                        var lfGlyph              = outputString[glyphIndex];
                        lfGlyph.xAdvance         = layoutConfig.m_tagIndent - penX;
                        lfGlyph.yAdvance         = 0f;
                        outputString[glyphIndex] = lfGlyph;

                        accumulatedVerticalOffset += verticalDrop;
                        penX                       = layoutConfig.m_tagIndent;
                        accumulatedLineWidth       = layoutConfig.m_tagIndent;

                        maxLineAscender  = float.MinValue;
                        maxLineDescender = float.MaxValue;
                        isFirstLine      = false;
                        isLineStart      = true;
                        bottomAnchor     = GetBottomAnchorForConfig(ref currentFont, textBaseConfiguration.verticalAlignment, baseScale);

                        lineStartBreakGlyphIndex        = glyphIndex;
                        lastWordStartGlyphIndex         = glyphIndex + 1;
                        lineStartGlyphIndex             = lastWordStartGlyphIndex;
                        accumulatedLineWidthAtWordStart = accumulatedLineWidth;
                        previousRune                    = currentRune;
                        continue;
                    }

                    // Normal glyph. Compute its advance.
                    float xAdvanceThisGlyph;
                    if (currentRune.value == 9)
                    {
                        float tabSize     = currentFont.TabAdvance() * currentElementScale;
                        float tabs        = math.ceil(penX / tabSize) * tabSize;
                        xAdvanceThisGlyph = (tabs > penX ? tabs : penX + tabSize) - penX;
                    }
                    else if (layoutConfig.m_monoSpacing != 0)
                    {
                        xAdvanceThisGlyph = layoutConfig.m_monoSpacing + layoutConfig.m_cSpacing;
                        if (isWhiteSpace || currentRune.value == 0x200B)
                            xAdvanceThisGlyph += textBaseConfiguration.wordSpacing * currentEmScale;
                    }
                    else
                    {
                        xAdvanceThisGlyph = glyphOTF.xAdvance * currentElementScale + layoutConfig.m_cSpacing;
                        if (isWhiteSpace || currentRune.value == 0x200B)
                            xAdvanceThisGlyph += textBaseConfiguration.wordSpacing * currentEmScale;
                    }

                    penX                     += xAdvanceThisGlyph;
                    accumulatedLineWidth     += xAdvanceThisGlyph;
                    var resolvedGlyph         = outputString[glyphIndex];
                    resolvedGlyph.xAdvance    = xAdvanceThisGlyph;
                    resolvedGlyph.yAdvance    = 0f;
                    outputString[glyphIndex]  = resolvedGlyph;

                    // Word wrapping: roll back to the last recorded word boundary if this glyph pushed us
                    // past the line width.
                    if (textBaseConfiguration.maxLineWidth < float.MaxValue && textBaseConfiguration.maxLineWidth > 0 && accumulatedLineWidth > textBaseConfiguration.maxLineWidth)
                    {
                        // What pushed us past the line width was a space preceded by a non-space: drop this
                        // one character's worth of overflow tolerance rather than starting the next line with
                        // a leading space.
                        bool dropSpace = currentRune.value == 32 && previousRune.value != 32;

                        // This is how much of the line we've used for visible characters, without alignment being applied.
                        float unalignedXOffsetChange = accumulatedLineWidthAtWordStart - layoutConfig.m_tagIndent;
                        // A newline is only valid if our break opportunity was on this line.
                        // Ensure at least one visible character per line by checking unalignedXOffsetChange.
                        if (lastWordStartGlyphIndex > lineStartGlyphIndex && unalignedXOffsetChange > 0 && !dropSpace)
                        {
                            // boundaryGlyphIndex is the breaking character (usually a space) before the word that starts the newline.
                            int   boundaryGlyphIndex = lastWordStartGlyphIndex - 1;
                            float shiftJustApplied   = ApplyHorizontalAlignmentShift(ref outputString, ref lineSpaceGlyphIndices, lineStartBreakGlyphIndex,
                                                                                     boundaryGlyphIndex, accumulatedLineWidthAtWordStart, textBaseConfiguration.maxLineWidth,
                                                                                     layoutConfig.m_lineJustification, ref header);
                            penX                += shiftJustApplied;
                            float xOffsetChange  = unalignedXOffsetChange + shiftJustApplied;

                            // Uses previousLineDecentLineDelta (the PREVIOUS line's own descent), not
                            // decentLineDelta (this line's own, about to be closed) � see
                            // previousLineDecentLineDelta's declaration.
                            float verticalDrop = previousLineDecentLineDelta + textBaseConfiguration.lineSpacing * currentEmScale;
                            if (!isFirstLine)
                                verticalDrop += currentLineHeight + ascentLineDelta;
                            ApplyVerticalDrop(ref outputString, lineStartBreakGlyphIndex, verticalDrop, ref header);
                            previousLineDecentLineDelta = decentLineDelta;

                            var boundaryGlyph                 = outputString[boundaryGlyphIndex];
                            boundaryGlyph.xAdvance           -= xOffsetChange;
                            boundaryGlyph.yAdvance            = 0f;
                            outputString[boundaryGlyphIndex]  = boundaryGlyph;

                            accumulatedVerticalOffset += verticalDrop;
                            penX                      -= xOffsetChange;
                            accumulatedLineWidth      -= unalignedXOffsetChange;

                            maxLineAscender  = float.MinValue;
                            maxLineDescender = float.MaxValue;
                            isFirstLine      = false;
                            isLineStart      = true;

                            lineStartBreakGlyphIndex = boundaryGlyphIndex;
                            lineStartGlyphIndex      = lastWordStartGlyphIndex;
                        }
                    }

                    if (!layoutConfig.m_isNoBreak && IsBreakOpportunity(currentRune) && IsSafeClusterBoundary(ref outputString, glyphIndex))
                    {
                        lastWordStartGlyphIndex         = glyphIndex + 1;
                        accumulatedLineWidthAtWordStart = accumulatedLineWidth;
                    }
                    // Justified-alignment stretch points: plain spaces only (not tabs/hyphens/ZWSP), matching
                    // standard inter-word justification. A space that later becomes a wrap boundary gets
                    // excluded at consumption time (see ApplyHorizontalAlignmentShift's spaceExclusiveUpperBound).
                    if (currentRune.value == 32 && lineSpaceGlyphIndices.Length < lineSpaceGlyphIndices.Capacity)
                        lineSpaceGlyphIndices.Add(glyphIndex);
                    previousRune = currentRune;
                }

                // Finalize the last line. Matches the linefeed logic.
                var finalLineJustification = layoutConfig.m_lineJustification == HorizontalAlignmentOptions.Justified ?
                                             HorizontalAlignmentOptions.Left : layoutConfig.m_lineJustification;
                penX += ApplyHorizontalAlignmentShift(ref outputString, ref lineSpaceGlyphIndices, lineStartBreakGlyphIndex,
                                                      int.MaxValue, accumulatedLineWidth, textBaseConfiguration.maxLineWidth, finalLineJustification, ref header);
                if (!isFirstLine)
                {
                    accumulatedVerticalOffset += currentLineHeight + ascentLineDelta;
                    ApplyVerticalDrop(ref outputString, lineStartBreakGlyphIndex, currentLineHeight, ref header);
                }

                // Whole-block vertical alignment is applied to the penStart
                float verticalAlignmentShift;
                switch (textBaseConfiguration.verticalAlignment)
                {
                    case VerticalAlignmentOptions.TopBase:
                        verticalAlignmentShift = 0f;
                        break;
                    case VerticalAlignmentOptions.TopAscent:
                    case VerticalAlignmentOptions.TopDescent:
                    case VerticalAlignmentOptions.TopCap:
                    case VerticalAlignmentOptions.TopMean:
                        verticalAlignmentShift = -topAnchor;
                        break;
                    case VerticalAlignmentOptions.MiddleTopAscentToBottomDescent:
                        verticalAlignmentShift = (accumulatedVerticalOffset - topAnchor - bottomAnchor) / 2f;
                        break;
                    default:  // Bottom*
                        verticalAlignmentShift = accumulatedVerticalOffset - bottomAnchor;
                        break;
                }

                header.penStart.y += verticalAlignmentShift;
            }

            // Applies the completed line's alignment and justification. Left alignment is a no-op.
            // For center and right alignment, the patch is applied to the first glyph of the line, or the
            // penStart for the first line. For justified, it is distributed across all plain-space glyphs.
            //
            // Justified is different from Center/Right: instead of one shift applied at the line's start,
            // the line's slack (maxLineWidth - lineWidth) is distributed across every plain-space glyph
            // recorded in lineSpaceGlyphIndices, each getting += nudgePerSpace added directly to its own
            // advance (in outputString, not via the boundary). spaceExclusiveUpperBound is the index of the
            // space before the word that is wrapped to the next line. Pass  int.MaxValue for hard-LF/the
            // final line, where there's no such boundary space to exclude.
            // lineSpaceGlyphIndices is always cleared before returning, once this line is fully resolved.
            //
            // Returns the total shift applied (0 if none). Callers MUST add this to their local penX,
            // so that they can compute the correct offset to apply to start a new line.
            static float ApplyHorizontalAlignmentShift(ref UnsafeList<GlyphOTF>   outputString,
                                                       ref FixedList512Bytes<int> lineSpaceGlyphIndices,
                                                       int lineStartBreakGlyphIndex,
                                                       int spaceExclusiveUpperBound,
                                                       float lineWidth,
                                                       float maxLineWidth,
                                                       HorizontalAlignmentOptions justification,
                                                       ref GlyphOTFStreamHeader header)
            {
                if (justification == HorizontalAlignmentOptions.Justified)
                {
                    int spaceCount = 0;
                    for (int i = 0; i < lineSpaceGlyphIndices.Length; i++)
                        if (lineSpaceGlyphIndices[i] < spaceExclusiveUpperBound)
                            spaceCount++;

                    float totalSlack = 0f;
                    if (spaceCount > 0 && maxLineWidth < float.MaxValue && maxLineWidth > 0)
                    {
                        totalSlack          = maxLineWidth - lineWidth;
                        float nudgePerSpace = totalSlack / spaceCount;
                        for (int i = 0; i < lineSpaceGlyphIndices.Length; i++)
                        {
                            int idx = lineSpaceGlyphIndices[i];
                            if (idx >= spaceExclusiveUpperBound)
                                continue;
                            var g              = outputString[idx];
                            g.xAdvance        += nudgePerSpace;
                            outputString[idx]  = g;
                        }
                    }
                    lineSpaceGlyphIndices.Clear();
                    return totalSlack;
                }

                lineSpaceGlyphIndices.Clear();

                float shift;
                switch (justification)
                {
                    case HorizontalAlignmentOptions.Center:
                        shift = -lineWidth / 2f;
                        break;
                    case HorizontalAlignmentOptions.Right:
                        shift = -lineWidth;
                        break;
                    default:
                        return 0f;
                }

                if (lineStartBreakGlyphIndex >= 0)
                {
                    var glyph                               = outputString[lineStartBreakGlyphIndex];
                    glyph.xAdvance                         += shift;
                    outputString[lineStartBreakGlyphIndex]  = glyph;
                }
                else
                {
                    header.penStart.x += shift;
                }
                return shift;
            }

            // Moves the completed line (and everything after it, via the cumulative sum) down by verticalDrop.
            static void ApplyVerticalDrop(ref UnsafeList<GlyphOTF> outputString, int lineStartBreakGlyphIndex, float verticalDrop, ref GlyphOTFStreamHeader header)
            {
                if (lineStartBreakGlyphIndex >= 0)
                {
                    var glyph                               = outputString[lineStartBreakGlyphIndex];
                    glyph.yAdvance                         -= verticalDrop;
                    outputString[lineStartBreakGlyphIndex]  = glyph;
                }
                else
                {
                    header.penStart.y -= verticalDrop;
                }
            }

            // Legacy Word/line-break-opportunity classification, ported unchanged from the original implementation.
            // In the future, we should support a more standardized break-detection algorithm.
            static bool IsBreakOpportunity(Unicode.Rune rune)
            {
                return rune.value == 32 ||  // Space
                       rune.value == 9 ||  // Tab
                       rune.value == 45 ||  // Hyphen-minus
                       rune.value == 173 ||  // Soft hyphen
                       rune.value == 8203 ||  // Zero width space
                       rune.value == 8204 ||  // Zero width non-joiner
                       rune.value == 8205;  // Zero width joiner
            }

            // Because we don't yet support line-break opportunities that would share a cluster in practice,
            // we just don't break up clusters and instead pick earlier break points if a cluster overruns
            // our width. This avoids having to reshape anything.
            static bool IsSafeClusterBoundary(ref UnsafeList<GlyphOTF> outputString, int glyphIndex)
            {
                if (glyphIndex + 1 >= outputString.Length)
                    return true;
                return outputString[glyphIndex].cluster != outputString[glyphIndex + 1].cluster;
            }

            static float GetTopAnchorForConfig(ref Font font, VerticalAlignmentOptions verticalMode, float baseScale, float oldValue = float.PositiveInfinity)
            {
                bool replace = oldValue == float.PositiveInfinity;
                switch (verticalMode)
                {
                    case VerticalAlignmentOptions.TopBase: return 0f;
                    case VerticalAlignmentOptions.MiddleTopAscentToBottomDescent:
                    case VerticalAlignmentOptions.TopAscent:
                        return baseScale *
                               math.max(font.fontExtents.ascender - font.baseLine, math.select(oldValue, float.NegativeInfinity, replace));
                    case VerticalAlignmentOptions.TopDescent: return baseScale * math.min(font.fontExtents.descender - font.baseLine, oldValue);
                    case VerticalAlignmentOptions.TopCap: return baseScale * math.max(font.capHeight - font.baseLine, math.select(oldValue, float.NegativeInfinity, replace));
                    case VerticalAlignmentOptions.TopMean: return baseScale * math.max(font.xHeight - font.baseLine, math.select(oldValue, float.NegativeInfinity, replace));
                    default: return 0f;
                }
            }

            static float GetBottomAnchorForConfig(ref Font font, VerticalAlignmentOptions verticalMode, float baseScale, float oldValue = float.PositiveInfinity)
            {
                bool replace = oldValue == float.PositiveInfinity;
                switch (verticalMode)
                {
                    case VerticalAlignmentOptions.BottomBase: return 0f;
                    case VerticalAlignmentOptions.BottomAscent:
                        return baseScale *
                               math.max(font.fontExtents.ascender - font.baseLine, math.select(oldValue, float.NegativeInfinity, replace));
                    case VerticalAlignmentOptions.MiddleTopAscentToBottomDescent:
                    case VerticalAlignmentOptions.BottomDescent: return baseScale * math.min(font.fontExtents.descender - font.baseLine, oldValue);
                    case VerticalAlignmentOptions.BottomCap: return baseScale *
                               math.max(font.capHeight - font.baseLine, math.select(oldValue, float.NegativeInfinity, replace));
                    case VerticalAlignmentOptions.BottomMean: return baseScale * math.max(font.xHeight - font.baseLine, math.select(oldValue, float.NegativeInfinity, replace));
                    default: return 0f;
                }
            }
        }
    }
}

