using System;
using System.IO;
using System.Reflection;
using Latios.Calligraphics.HarfBuzz;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

using Font = Latios.Calligraphics.HarfBuzz.Font;

namespace Latios.Calligraphics.Systems
{
    [DisableAutoCreation]
    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
    [RequireMatchingQueriesForUpdate]
    [UpdateInGroup(typeof(Latios.Systems.LatiosWorldSyncGroup), OrderLast = true)]
    partial class NativeFontLoaderSystem : SystemBase
    {
        EntityQuery changedFontReferenceQ;
        MethodInfo  methodInfo;
        FieldInfo[] fontReference;
        object      m_fontRef;

        protected override void OnCreate()
        {
            var perThreadFontCaches = new NativeArray<UnsafeList<Font> >(JobsUtility.ThreadIndexCount, Allocator.Persistent);
            for (int i = 0; i < perThreadFontCaches.Length; i++)
            {
                perThreadFontCaches[i] = new UnsafeList<Font>(64, Allocator.Persistent);
            }
            EntityManager.CreateSingleton(new FontTable
            {
                faces                                = new NativeList<Face>(Allocator.Persistent),
                perThreadFontCaches                  = perThreadFontCaches,
                fontAssetRefs                        = new NativeList<FontAssetRef>(Allocator.Persistent),
                fontAssetRefToFaceIndexMap           = new NativeHashMap<FontAssetRef, int>(64, Allocator.Persistent),
                fontAssetRefToNamedVariationIndexMap = new NativeHashMap<FontAssetRef, int>(64, Allocator.Persistent),
            });

            changedFontReferenceQ = SystemAPI.QueryBuilder()
                                    .WithAll<FontReference>()
                                    .Build();
            changedFontReferenceQ.SetChangedVersionFilter(ComponentType.ReadWrite<FontReference>());

            RequireForUpdate(changedFontReferenceQ);

            GetSystemFontsMethod();
        }

        //[BurstCompile]
        protected override void OnUpdate()
        {
            if (changedFontReferenceQ.IsEmpty)
                return;

            var changedFontReferenceBuffer = changedFontReferenceQ.GetSingletonBuffer<FontReference>();
            var fontTable                  = SystemAPI.GetSingletonRW<FontTable>().ValueRW;
            CompleteDependency();

            //copy to nativeArray because LoadFont would invalidate DynamicBuffer due to structural changes
            var fontReferences = CollectionHelper.CreateNativeArray<FontReference>(changedFontReferenceBuffer.AsNativeArray(), WorldUpdateAllocator);

            for (int i = 0, ii = fontReferences.Length; i < ii; i++)
            {
                var fontReference = fontReferences[i];
                if (!fontTable.fontAssetRefToFaceIndexMap.ContainsKey(fontReference.fontAssetRef))
                    LoadFont(fontReference, ref CheckedStateRef, ref fontTable);
            }
        }

        protected override void OnDestroy()
        {
            SystemAPI.GetSingletonRW<FontTable>().ValueRW.TryDispose(Dependency).Complete();
        }

        void GetSystemFontsMethod()
        {
            Assembly textCoreFontEngineModule = default;
            foreach (Assembly loadedAssembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (loadedAssembly.GetName().Name == "UnityEngine.TextCoreFontEngineModule")
                {
                    textCoreFontEngineModule = loadedAssembly;
                    FontEngine.GetSystemFontNames();
                    UnityEngine.Font.GetPathsToOSFonts();
                    //Debug.Log($"Found UnityEngine.TextCoreFontEngineModule in loaded assemblies: {loadedAssembly.FullName}");
                    break;
                }
            }
            var fontReferenceType = textCoreFontEngineModule.GetType("UnityEngine.TextCore.LowLevel.FontReference");
            fontReference         = fontReferenceType.GetFields();
            var m_fontRef         = Activator.CreateInstance(fontReferenceType);

            BindingFlags bindingFlags = BindingFlags.NonPublic | BindingFlags.Static;
            methodInfo                = typeof(FontEngine).GetMethod("TryGetSystemFontReference", bindingFlags);
            //MakeDelegate<fontReference>(methodInfo);
        }
        public static Func<string, string, object, bool> MakeDelegate<U>(MethodInfo methodInfo)
        {
            var f = (Func<string, string, U, bool>)Delegate.CreateDelegate(typeof(Func<string, string, U, bool>), methodInfo);
            return (a, b, c) => f(a, b, (U)c);
        }
        void LoadFont(FontReference fontReference, ref SystemState state, ref FontTable fontTable)
        {
            Blob   blob;
            string fontAssetPath;

            if (fontReference.isSystemFont)
            {
                //loading rules: https://www.high-logic.com/fontcreator/manual15/fonttype.html

                var      typeographicFamilyDataMissing = (fontReference.typographicFamily.IsEmpty || fontReference.typographicSubfamily.IsEmpty);
                var      family                        = typeographicFamilyDataMissing ? fontReference.fontFamily : fontReference.typographicFamily;
                var      subFamily                     = typeographicFamilyDataMissing ? fontReference.fontSubFamily : fontReference.typographicSubfamily;
                object[] args                          = new object[] { family.ToString(), subFamily.ToString(), m_fontRef };
                var      systemFontFound               = (bool)methodInfo.Invoke(null, args);
                var      result                        = args[2];

                //if (!TryGetSystemFontReference(family.ToString(), subFamily.ToString(), out UnityFontReference unityFontReference))
                if (!systemFontFound)
                {
                    //Debug.Log($"Could not find system font {fontReference.fontFamily} {fontReference.fontSubFamily}");
                    return;
                }
                //Debug.Log($"Found {fieldInfos[0].GetValue(result)} {fieldInfos[1].GetValue(result)} {fieldInfos[2].GetValue(result)} {fieldInfos[3].GetValue(result)}");
                fontAssetPath = (string)this.fontReference[3].GetValue(result);
            }
            else
            {
                if (fontReference.streamingAssetLocationValidated)
                    fontAssetPath = Path.Combine(Application.streamingAssetsPath, fontReference.filePath.ToString());
                else
                    fontAssetPath = fontReference.filePath.ToString();

                if (!File.Exists(fontAssetPath))
                {
                    //Debug.Log($"Could not find font in {fontAssetPath}");
                    return;
                }
            }

            blob = new Blob(fontAssetPath);
            blob.MakeImmutable();  //is this neccessary considering we dispose the blob in next instruction?

            // in case font file is a collection font, chances are that none of the faces have been loaded yet
            // while file is open, load them all to avoid opening file again
            var tempFontReferences = new NativeList<FontReference>(blob.FaceCount, Allocator.Temp);
            var language           = Language.English;
            TextHelper.GetFaceInfo(blob, language, fontReference, tempFontReferences);

            for (int i = 0, ii = tempFontReferences.Length; i < ii; i++)
            {
                var tempFontReference = tempFontReferences[i];
                var tempFontAssetRef  = tempFontReference.fontAssetRef;
                if (!fontTable.fontAssetRefToFaceIndexMap.ContainsKey(tempFontAssetRef))
                {
                    var id = fontTable.fontAssetRefToFaceIndexMap.Count;
                    fontTable.fontAssetRefs.Add(tempFontAssetRef);
                    fontTable.fontAssetRefToFaceIndexMap.Add(tempFontAssetRef, id);
                    var face = new Face(blob, tempFontReference.faceIndexInFile);
                    face.MakeImmutable();
                    fontTable.faces.Add(face);

                    for (int k = 0, kk = fontTable.perThreadFontCaches.Length; k < kk; k++)
                    {
                        var list = fontTable.perThreadFontCaches[k];
                        list.Add(default);
                        fontTable.perThreadFontCaches[k] = list;
                    }

                    //setup lookup of named variable instance
                    if (face.HasVarData)
                    {
                        var axisCount = (int)face.AxisCount;

                        //fetch a list of all variation axis
                        Span<AxisInfo> axisInfos = stackalloc AxisInfo[axisCount];
                        face.GetAxisInfos(0, 0, ref axisInfos, out _);
                        AxisInfo axisInfo;
                        float    coord;

                        //fetch a list of named variants
                        //Debug.Log($"found {axisCount} variation axis for font {fontReference.fontFamily} {fontReference.fontSubFamily}, {face.NamedInstanceCount} named instances");
                        Span<float> coords = stackalloc float[axisCount];
                        for (int k = 0, kk = (int)face.NamedInstanceCount; k < kk; k++)
                        {
                            face.GetNamedInstanceDesignCoords(k, ref coords, out uint coordLength);
                            var variableFontAssetRef = tempFontAssetRef;
                            for (int f = 0, ff = (int)coordLength; f < ff; f++)
                            {
                                //axisInfos and coords should be aligned in length and order
                                axisInfo = axisInfos[f];
                                coord    = coords[f];
                                switch (axisInfo.axisTag)
                                {
                                    case AxisTag.WIDTH:
                                        variableFontAssetRef.width = coord; break;
                                    case AxisTag.WEIGHT:
                                        variableFontAssetRef.weight = coord; break;
                                    case AxisTag.ITALIC:
                                        variableFontAssetRef.isItalic = (int)coord == 1; break;
                                    case AxisTag.SLANT:
                                        variableFontAssetRef.slant = coord; break;
                                }
                                //Debug.Log($"Add FontAssetRef {tempFontAssetRef} for variation axis: {axisInfo.axisTag} {face.GetName(axisInfo.nameID, language)}, value = {coord}");
                            }
                            fontTable.fontAssetRefToNamedVariationIndexMap.Add(variableFontAssetRef, k);
                        }
                    }
                }
            }
            //blob can be disposed here, face and font are disposed at world shutdown via FontTable.TryDispose
            blob.Dispose();
        }
    }
}

