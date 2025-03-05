using System;
using System.Collections.Generic;
using UnityEngine;

namespace Latios.Calligraphics.Authoring
{
    [CreateAssetMenu(fileName = "FontFallbackStrategyAsset", menuName = "Latios/Calligraphics/FontFallbackStrategyAsset")]
    public class FontFallbackStrategyAsset : ScriptableObject
    {
        [Serializable]
        public struct Fallback
        {
            public string desiredFamily;
            public string fallbackFamily;
        }
        public List<Fallback> fallbacks;
    }
}

