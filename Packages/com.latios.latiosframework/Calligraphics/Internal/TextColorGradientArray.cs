using Unity.Collections;
using Unity.Entities;

namespace TextMeshDOTS
{
    internal struct TextColorGradientArray
    {
        public FixedList4096Bytes<TextColorGradient> textColorGradients;
        public readonly int Length => textColorGradients.Length;
        public readonly TextColorGradient this[int index] => textColorGradients[index];

        public void Initialize(Entity textColorGradientEntity,
                               BufferLookup<TextColorGradient> textColorGradientLookup)
        {
            textColorGradients.Clear();
            if (textColorGradientEntity == Entity.Null)
                return;
            var textColorGradientBuffer = textColorGradientLookup[textColorGradientEntity];
            for (int i = 0, ii= textColorGradientBuffer.Length; i < ii; i++)
                textColorGradients.Add(textColorGradientBuffer[i]);
        }        
    }
}

