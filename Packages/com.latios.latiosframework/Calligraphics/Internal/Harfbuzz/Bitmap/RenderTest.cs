using Font = Latios.Calligraphics.HarfBuzz.Font;
using Latios.Calligraphics;
using Latios.Calligraphics.HarfBuzz;
using Latios.Calligraphics.HarfBuzz.Bitmap;
using Unity.Collections;
using Unity.Profiling;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.Text;

internal class RenderTest : MonoBehaviour
{
    static readonly ProfilerMarker  marker = new ProfilerMarker("Rasterize");
    public Object                   sourceFont;
    [SerializeField] private string fontAssetPath;
    public string                   letter;
    public uint                     glyphID;
    public int                      offsetX           = 0;
    public int                      offsetY           = 0;
    public int                      SPREAD            = 8;
    public int                      padding           = 8;
    public int                      atlasWidth        = 1024;
    public int                      atlasHeight       = 1024;
    public int                      samplingPointSize = 256;
    public bool                     renderGlyphID;
    public FontAsset                fontAsset;

    float                maxDeviation;
    SDFOrientation       orientation;
    public DrawDelegates drawFunctions;
    DrawData             drawData;
    PaintDelegates       paintFunctions;
    PaintData            paintData;
    Blob                 blob;
    Face                 face;
    Font                 font;

    void Start()
    {
#if UNITY_EDITOR
        fontAssetPath = AssetDatabase.GetAssetPath(sourceFont);
#endif
        if (fontAssetPath == null)
            return;

        drawFunctions  = new DrawDelegates(true);
        paintFunctions = new PaintDelegates(true);
        if (!LoadFont(fontAssetPath, samplingPointSize))
            return;

        DrawTest(letter, glyphID);
        //PaintTest(letter, glyphID); //🌁😉🥰💀✌️🌴🐢🐐🍄⚽🍻👑📸😬👀🚨🏡🕊️🏆😻🌟🧿🍀🎨🍜

        //var texture = fontAsset.atlasTexture;
        //var texturebuffer = texture.GetPixelData<byte>(0);
        //SDFCommon.WriteArrayToFile("Unity SDF8.txt", texturebuffer, texture.width, texture.height/2);

        //var lang = new Language("en");
        //var script = Script.LATIN;
        //Language.OtTagsFromScriptAndLanguage(script, lang, out NativeList<uint> script_tags, out NativeList<uint> language_tags);

        //foreach (var tag in script_tags)
        //    Debug.Log(Harfbuzz.HB_TAG(tag));

        //foreach (var tag in language_tags)
        //    Debug.Log(Harfbuzz.HB_TAG(tag));
    }

    void Update()
    {
    }

    void DrawTest(string character, uint glyphID)
    {
        //shape
        Buffer buffer = default;
        if (!renderGlyphID)
        {
            var language = Language.English;
            buffer       = new Buffer(Direction.LTR, Script.LATIN, language);
            //buffer.AddText("😉");
            buffer.AddText(character);
            font.Shape(buffer);
            var glyphInfos = buffer.GetGlyphInfosSpan();
            glyphID        = glyphInfos[0].codepoint;
        }

        //get glyph
        drawData = new DrawData(256, 16, maxDeviation, Allocator.Persistent);
        font.DrawGlyph(glyphID, drawFunctions, ref drawData);
        font.GetGlyphExtents(glyphID, out GlyphExtents glyphExtents);

        var atlasRect = glyphExtents.GetPaddedAtlasRect(offsetX, offsetY, padding);

        //allocate texture
        //atlasWidth = atlasWidth < (atlasRect.width + atlasRect.x) ? (atlasRect.width + atlasRect.x) : atlasWidth;
        //atlasHeight = atlasHeight < (atlasRect.height + atlasRect.y) ? (atlasRect.height + atlasRect.y) : atlasHeight;
        //int nextPowerOfTwo = math.ceilpow2(math.max(atlasHeight, atlasHeight));
        //atlasWidth = atlasHeight = nextPowerOfTwo;
        //var texture2D = new Texture2D(nextPowerOfTwo, nextPowerOfTwo, TextureFormat.Alpha8, false);
        var texture2D   = new Texture2D(atlasWidth, atlasHeight, TextureFormat.Alpha8, false);
        var textureData = texture2D.GetRawTextureData<byte>();
        for (int i = 0; i < textureData.Length; i++)
            textureData[i] = 0;

        //render
        //SDFCommon.WriteGlyphOutlineToFile("Outline.txt", ref drawData, true);
        //SDFCommon.WriteGlyphOutlineToFile($"Outline of glyph {character}.txt", drawData);

        //simplify. Both clipper and polybol outputs the outer contour CCW, and the inner CW, which is postscript definition
        orientation = SDFOrientation.POSTSCRIPT;  //clipper always outputs the outer contour CCW, and the inner CW, which is postscript definition
        PolygonOperation.RemoveSelfIntersections(ref drawData);
        //SDFCommon.WriteGlyphOutlineToFile($"Outline of glyph {character}.txt", drawData);

        marker.Begin();
        //BezierMath.SplitCuvesToLines(ref drawData, maxDeviation, out DrawData flatenedDrawData);
        //SDF.SDFGenerateSubDivision(orientation, ref drawData, ref textureData, ref atlasRect, padding, atlasWidth, atlasHeight,padding);
        SDF_line.SDFGenerateSubDivisionLineEdges(orientation, ref drawData, ref textureData, ref atlasRect, padding, atlasWidth, atlasHeight, SPREAD);
        //SDFCommon.WriteArrayToFile("TMD SDF32.txt", textureData, texture2D.width, texture2D.height/2);
        marker.End();

        var meshRenderer                  = GetComponent<MeshRenderer>();
        meshRenderer.material.mainTexture = texture2D;
        texture2D.Apply();
        if (!renderGlyphID)
            buffer.Dispose();
    }
    void PaintTest(string character, uint glyphID)
    {
        var texture2D   = new Texture2D(atlasWidth, atlasHeight, TextureFormat.ARGB32, false);
        var textureData = texture2D.GetRawTextureData<ColorARGB>();
        Blending.SetBlack(textureData);

        Buffer buffer = default;
        BBox   clipRect;
        if (!renderGlyphID)
        {
            var language = Language.English;
            buffer       = new Buffer(Direction.LTR, Script.LATIN, language);
            buffer.AddText(character);
            font.Shape(buffer);
            var glyphInfos     = buffer.GetGlyphInfosSpan();
            var glyphPositions = buffer.GetGlyphPositionsSpan();
            glyphID            = glyphInfos[0].codepoint;
            //Debug.Log($"glyphID {glyphID} {glyphPositions[0]}");
        }

        paintData = new PaintData(drawFunctions, 256, 4, maxDeviation, Allocator.Temp);
        font.GetGlyphExtents(glyphID, out GlyphExtents glyphExtents);
        paintData.clipRect = glyphExtents.ClipRect;
        paintData.clipRect.Expand(1);  //prevents rendering artifacts that occur for outlines that strech from minX to maxX of clipRect, reason unknown
        paintData.paintSurface = new NativeArray<ColorARGB>(paintData.clipRect.intWidth * paintData.clipRect.intHeight, Allocator.Temp);
        //Debug.Log($"clipBox: {paintData.clipRect}");

        marker.Begin();
        font.PaintGlyph(glyphID, ref paintData, paintFunctions, 0, new ColorARGB(255, 0, 0, 0));
        marker.End();

        if (paintData.imageData.Length > 0)  //render PNG and SVG
        {
            if (paintData.imageFormat == PaintImageFormat.PNG)
            {
                var png = new Texture2D(2, 2, TextureFormat.ARGB32, false);
                png.LoadImage(paintData.imageData.ToArray());
                var sourceTexture = png.GetRawTextureData<ColorARGB>();
                PaintUtils.BlitRawTexture(sourceTexture, paintData.imageWidth, paintData.imageHeight, textureData, atlasWidth, atlasHeight, 0, 0);
            }
            if (paintData.imageFormat == PaintImageFormat.SVG)
            {
                //could use com.unity.vectorgraphics (designed to parse, tesselate and render svg) if it would not be a class
            }
        }
        else if (paintData.paintSurface.Length > 0)  // content from COLR, or raw BGRA data from sbix, CBDT
        {
            clipRect = paintData.clipRect;
            PaintUtils.BlitRawTexture(paintData.paintSurface, clipRect.intWidth, clipRect.intHeight, textureData, atlasWidth, atlasHeight, 0, 0);
        }

        var meshRenderer                  = GetComponent<MeshRenderer>();
        meshRenderer.material.mainTexture = texture2D;
        texture2D.Apply(false);
        if (!renderGlyphID)
            buffer.Dispose();
    }

    private void OnDestroy()
    {
        drawFunctions.Dispose();
        drawData.Dispose();
        paintFunctions.Dispose();
        font.Dispose();
        face.Dispose();
        blob.Dispose();
    }
    bool LoadFont(string fontAssetPath, int samplingPointSize)
    {
        if (!TextHelper.IsValidFont(fontAssetPath))
        {
            Debug.LogWarning("Ensure you only have files ending with 'ttf' or 'otf' (case insensitiv) in font list");
            return false;
        }

        blob = new Blob(fontAssetPath);
        face = new Face(blob, 0);
        font = new Font(face);
        if (face.HasVarData)
        {
            font.VariationNamedInstance = 17;  //13 OK, 14 buggy for "6"
            //DisplayVariationAxis();
        }

        //var scale = font.GetScale();
        font.SetScale(samplingPointSize, samplingPointSize);
        //Debug.Log($"scale: {scale}");

        maxDeviation = BezierMath.GetMaxDeviation(font.GetScale().x);
        //Debug.Log($"Has COLR outlines? {face.HasCOLR()}");
        //Debug.Log($"Has Color Bitmap? {face.HasColorBitmap()}");

        orientation = face.HasTrueTypeOutlines() ? SDFOrientation.TRUETYPE : SDFOrientation.POSTSCRIPT;
        return true;
    }
    void DisplayVariationAxis()
    {
        var axisCount = (int)face.AxisCount;

        //fetch a list of all variation axis
        System.Span<AxisInfo> axisInfos = stackalloc AxisInfo[axisCount];
        face.GetAxisInfos(0, 0, ref axisInfos, out _);
        AxisInfo axisInfo;
        float    coord;

        //fetch a list of named variants
        //Debug.Log($"found {axisCount} variation axis for font {fontReference.fontFamily} {fontReference.fontSubFamily}, {face.NamedInstanceCount} named instances");
        System.Span<float> coords = stackalloc float[axisCount];
        for (int k = 0, kk = (int)face.NamedInstanceCount; k < kk; k++)
        {
            Debug.Log($"Named Instance: {k}");
            face.GetNamedInstanceDesignCoords(k, ref coords, out uint coordLength);
            for (int f = 0, ff = (int)coordLength; f < ff; f++)
            {
                //axisInfos and coords should be aligned in length and order
                axisInfo = axisInfos[f];
                coord    = coords[f];
                Debug.Log($"Variation axis: {axisInfo.axisTag} {face.GetName(axisInfo.nameID, Language.English)}, value = {coord}");
            }
        }
    }
}

