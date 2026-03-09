#if UNITY_EDITOR
using Unity.Collections;
using Unity.Entities;
using Unity.Rendering;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace TextMeshDOTS.Authoring
{
    [DisallowMultipleComponent]
    [AddComponentMenu("TextMeshDOTS/TMD Runtime Material")]
    public class TMDRuntimeMaterialAuthoring : MonoBehaviour
    {
        [Tooltip("Drop here the material you wou like to use for Text Renderer spawned at runtime")]
        public Material material;
        [Tooltip("Use BCP 47 conform tags to set the language of text spawned at runtime https://en.wikipedia.org/wiki/IETF_language_tag#List_of_common_primary_language_subtags)")]
        public string language = "en";
    }

    class TMDRuntimeMaterialBaker : Baker<TMDRuntimeMaterialAuthoring>
    {
        public override void Bake(TMDRuntimeMaterialAuthoring authoring)
        {
            DependsOn(authoring.material);
            if (authoring.material ==null)
                return;

            string[] guids = AssetDatabase.FindAssets("TextBackendMesh t:mesh", null);
            if (guids.Length == 0 || guids[0] == null)
                return;

            var backEndMesh = AssetDatabase.LoadAssetByGUID(new GUID(guids[0]), typeof(Mesh)) as Mesh;

            var entity = GetEntity(TransformUsageFlags.None);

            var runtimeFontMaterial = new RuntimeFontMaterial
            {
                material = authoring.material,
                backendMesh = backEndMesh,
                materialMeshInfo = new MaterialMeshInfo(BatchMaterialID.Null, BatchMeshID.Null)
            };            
            AddComponent(entity, runtimeFontMaterial);
            var runtimeLanguage = new RuntimeLanguage
            {
                value = BakeLangugeString(authoring.language)
            };
            AddComponent(entity, runtimeLanguage);
        }
        BlobAssetReference<LanguageBlob> BakeLangugeString(FixedString128Bytes language)
        {
            var customHash = new Unity.Entities.Hash128((uint)language.GetHashCode(), 0, 0, 0);
            if (!TryGetBlobAssetReference(customHash, out BlobAssetReference<LanguageBlob> blobReference))
            {
                blobReference = TextRendererUtility.BakeLanguage(language);
                AddBlobAssetWithCustomHash(ref blobReference, customHash); // Register the Blob Asset to the Baker for de-duplication and reverting.
            }
            return blobReference;
        }
    }    
}
#endif