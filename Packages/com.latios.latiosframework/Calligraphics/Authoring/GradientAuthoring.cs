using System;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using UnityEngine.TextCore.Text;

namespace Latios.Calligraphics.Authoring
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Calligraphics/Text Color Gradient")]
    public class TextGradientAuthoring : MonoBehaviour
    {
        [Tooltip("For horizontal gradients, specify at least top-(left & right). For vertical gradients (top & bottom)-left. Otherwise specify all corner")]
        public List<ColorGradient> gradients;
    }

    class TextGradientBaker : Baker<TextGradientAuthoring>
    {
        public override void Bake(TextGradientAuthoring authoring)
        {
            if (authoring.gradients == null)
                return;

            if (authoring.gradients.Count > 24)  //160byte per gradient, stored in FixedList4096Bytes in shapeJob
            {
                Debug.Log("Calligraphics supports currently only 24 gradients");
                return;
            }
            var entity             = GetEntity(TransformUsageFlags.None);
            var textColorGradients = AddBuffer<TextColorGradient>(entity);
            for (int i = 0, ii = authoring.gradients.Count; i < ii; i++)
            {
                var               gradient = authoring.gradients[i];
                TextColorGradient textColorGradient;
                switch (gradient.colorMode)
                {
                    case ColorGradientMode.VerticalGradient:
                        textColorGradient = new TextColorGradient
                        {
                            nameHash    = TextHelper.GetValueHash(gradient.name),
                            topLeft     = gradient.topLeft,
                            topRight    = gradient.topLeft,
                            bottomLeft  = gradient.bottomLeft,
                            bottomRight = gradient.bottomLeft,
                        };
                        break;
                    case ColorGradientMode.HorizontalGradient:
                        textColorGradient = new TextColorGradient
                        {
                            nameHash    = TextHelper.GetValueHash(gradient.name),
                            topLeft     = gradient.topLeft,
                            topRight    = gradient.topRight,
                            bottomLeft  = gradient.topLeft,
                            bottomRight = gradient.topRight,
                        };
                        break;
                    case ColorGradientMode.Single:
                        textColorGradient = new TextColorGradient
                        {
                            nameHash    = TextHelper.GetValueHash(gradient.name),
                            topLeft     = gradient.topLeft,
                            topRight    = gradient.topLeft,
                            bottomLeft  = gradient.topLeft,
                            bottomRight = gradient.topLeft,
                        };
                        break;
                    case ColorGradientMode.FourCornersGradient:
                    default:
                        textColorGradient = new TextColorGradient
                        {
                            nameHash    = TextHelper.GetValueHash(gradient.name),
                            topLeft     = gradient.topLeft,
                            topRight    = gradient.topRight,
                            bottomLeft  = gradient.bottomLeft,
                            bottomRight = gradient.bottomRight,
                        };
                        break;
                }
                textColorGradients.Add(textColorGradient);
            }
        }
    }

    /// <summary>
    /// Definition of color gradients
    /// </summary>
    [Serializable]
    public struct ColorGradient
    {
        [SerializeField]
        public string name;
        [SerializeField]
        public ColorGradientMode colorMode;
        [SerializeField]
        public Color topLeft;
        [SerializeField]
        public Color topRight;
        [SerializeField]
        public Color bottomLeft;
        [SerializeField]
        public Color bottomRight;
    }
}

