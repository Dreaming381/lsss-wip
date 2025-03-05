using System.Collections.Generic;
using UnityEngine;

namespace Latios.Calligraphics.Authoring
{
    [CreateAssetMenu(fileName = "FontCollectionAsset", menuName = "Latios/Calligraphics/Font Collection Asset")]
    public class FontCollectionAsset : ScriptableObject
    {
        [Tooltip("Supported types: .ttf  .ttc")]
        public List<Object> streamingAssetFonts;
        public List<Sprite> sprites;

        // If anyone figures out how to make the Text Renderer Authoring inspector fancy to do a font selector,
        // feel free to add a field here to list out OS fonts so that they can be added to the selector.
    }
}

