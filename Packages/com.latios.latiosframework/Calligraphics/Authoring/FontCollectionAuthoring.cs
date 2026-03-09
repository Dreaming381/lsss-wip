#if UNITY_EDITOR
using Latios.Calligraphics;
using Latios.Calligraphics.Authoring;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace Latios.Calligraphics
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Calligraphics/Font Collection")]
    public class FontCollectionAuthoring : MonoBehaviour
    {
        public FontCollectionAsset fontCollectionAsset;
    }
    class FontCollectionBaker : Baker<FontCollectionAuthoring>
    {
        public override void Bake(FontCollectionAuthoring authoring)
        {
            int fontCount;
            if (authoring.fontCollectionAsset == null || (fontCount = authoring.fontCollectionAsset.fontReferences.Count) == 0)
                return;

            var fontRequests = new NativeArray<FontReference>(fontCount, Allocator.Temp);

            var sourceFontRequests = authoring.fontCollectionAsset.fontReferences;
            for (int i = 0, ii = sourceFontRequests.Count; i < ii; i++)
                fontRequests[i] = sourceFontRequests[i];            

            var entity = GetEntity(TransformUsageFlags.None);
            var fontRequestsBuffer = AddBuffer<FontReference>(entity);
            fontRequestsBuffer.AddRange(fontRequests);
        }        
    }
}
#endif