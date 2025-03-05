using Latios.Authoring;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Latios.Calligraphics.Authoring
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Latios/Calligraphics/Text Renderer (Calligraphics)")]
    public class TextRendererAuthoring : MonoBehaviour
    {
        [TextArea(5, 10)]
        public string text = "New text";

        public string fontName = "Liberation Sans";

        [EnumButtons]
        public FontStyles fontStyles = FontStyles.Normal;
        public float      fontSize   = 12f;

        [ColorUsage(true, true)]
        public Color color = Color.white;

        public HorizontalAlignmentOptions horizontalAlignment = HorizontalAlignmentOptions.Left;
        public VerticalAlignmentOptions   verticalAlignment   = VerticalAlignmentOptions.TopAscent;
        public bool                       wordWrap            = true;
        public float                      maxLineWidth        = 30;
        public bool                       isOrthographic      = false;
        [Tooltip("Additional word spacing in font units where a value of 1 equals 1/100em.")]
        public float wordSpacing = 0;
        [Tooltip("Additional line spacing in font units where a value of 1 equals 1/100em.")]
        public float lineSpacing = 0;
        [Tooltip("Paragraph spacing in font units where a value of 1 equals 1/100em.")]
        public float paragraphSpacing = 0;
        [Tooltip("An asset representing the StreamingAsset fonts as well as Sprite assets which should be accessible at runtime")]
        public FontCollectionAsset fontCollection;
        [Tooltip("An asset representing the fallback strategy when a font family is absent or has missing glyphs")]
        public FontFallbackStrategyAsset fallbackStrategy;
    }

    public class TextRendererAuthoringBaker : Baker<TextRendererAuthoring>
    {
        public override void Bake(TextRendererAuthoring authoring)
        {
            //
        }
    }
}

