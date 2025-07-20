using System;
using System.Collections.Generic;
using Latios.Kinemation;
using Latios.Kinemation.Authoring;
using Latios.Transforms;
using Latios.Transforms.Authoring.Abstract;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine;

namespace Lsss.Authoring
{
    public class RendererCombinerAuthoring : MonoBehaviour, IOverrideHierarchyRenderer
    {
        public List<MeshRenderer> meshRenderers;

        public bool ShouldOverride(IBaker baker, Renderer renderer)
        {
            if (meshRenderers == null)
                return false;

            if (renderer is MeshRenderer meshRenderer)
            {
                foreach (var mr in meshRenderers)
                {
                    if (mr == meshRenderer)
                        return true;
                }
            }
            return false;
        }
    }

    public class RendererCombinerAuthoringBaker : Baker<RendererCombinerAuthoring>
    {
        List<Material> m_materialsCache = new List<Material>();

        public override void Bake(RendererCombinerAuthoring authoring)
        {
            if (authoring.meshRenderers == null || authoring.meshRenderers.Count == 0)
                return;

            int          opaqueCount        = 0;
            int          transparentCount   = 0;
            MeshRenderer primaryRenderer    = null;
            bool         useLod             = false;
            float        conservativeLodMin = 0f;
            float        conservativeLodMax = 0f;
            foreach (var mr in authoring.meshRenderers)
            {
                if (mr == null)
                    continue;

                var mf  = GetComponent<MeshFilter>(mr);
                var cap = GetComponent<CapsuleMeshAuthoring>(mr);
                if (mf == null || (mf.sharedMesh == null && cap == null))
                    continue;

                m_materialsCache.Clear();
                mr.GetSharedMaterials(m_materialsCache);
                if (m_materialsCache.Count == 0)
                    continue;

                if (primaryRenderer == null)
                    primaryRenderer = mr;

                var opaqueInRenderer      = 0;
                var transparentInRenderer = 0;
                foreach (var mat in m_materialsCache)
                {
                    if (RenderingBakingTools.RequiresDepthSorting(mat))
                        transparentInRenderer++;
                    else
                        opaqueInRenderer++;
                }

                if (cap != null && cap.m_useLOD)
                {
                    opaqueInRenderer      *= 2;
                    transparentInRenderer *= 2;
                    useLod                 = true;
                    conservativeLodMin     = math.max(conservativeLodMin, cap.m_lodTransitionMinPercentage);
                    conservativeLodMax     = math.max(conservativeLodMax, cap.m_lodTransitionMaxPercentage);
                }
                opaqueCount      += opaqueInRenderer;
                transparentCount += transparentInRenderer;
            }

            Span<MeshMaterialSubmeshSettings> mms              = stackalloc MeshMaterialSubmeshSettings[opaqueCount + transparentCount];
            var                               opaqueIndex      = 0;
            var                               transparentIndex = opaqueCount;

            var thisTransform = GetComponent<Transform>();

            Bounds bounds            = default;
            bool   boundsInitialized = false;
            foreach (var mr in authoring.meshRenderers)
            {
                if (mr == null)
                    continue;

                var mf  = GetComponent<MeshFilter>(mr);
                var cap = GetComponent<CapsuleMeshAuthoring>(mr);
                if (mf == null || (mf.sharedMesh == null && cap == null))
                    continue;

                m_materialsCache.Clear();
                mr.GetSharedMaterials(m_materialsCache);
                if (m_materialsCache.Count == 0)
                    continue;

                if (cap == null)
                {
                    var mesh = TransformMesh(thisTransform, GetComponent<Transform>(mr), mf.sharedMesh);
                    if (boundsInitialized)
                        bounds.Encapsulate(mesh.bounds);
                    else
                    {
                        bounds            = mesh.bounds;
                        boundsInitialized = true;
                    }

                    ushort i = 0;
                    foreach (var mat in m_materialsCache)
                    {
                        var toAdd = new MeshMaterialSubmeshSettings
                        {
                            mesh     = mesh,
                            material = mat,
                            submesh  = i,
                            lodMask  = 0xff
                        };
                        i++;
                        if (RenderingBakingTools.RequiresDepthSorting(mat))
                        {
                            mms[transparentIndex] = toAdd;
                            transparentIndex++;
                        }
                        else
                        {
                            mms[opaqueIndex] = toAdd;
                            opaqueIndex++;
                        }
                    }
                }
                else
                {
                    var mesh    = TransformMesh(thisTransform, GetComponent<Transform>(mr), cap.ProcessMeshForBake());
                    var lodMesh = mesh;
                    if (cap.m_useLOD)
                    {
                        lodMesh = CapsuleMeshBaker.CreateLODMesh(cap.m_height, cap.m_radius, cap.m_axis);
                        lodMesh = TransformMesh(thisTransform, mr.transform, lodMesh);
                    }
                    if (boundsInitialized)
                        bounds.Encapsulate(mesh.bounds);
                    else
                    {
                        bounds            = mesh.bounds;
                        boundsInitialized = true;
                    }

                    byte lodMask0 = (byte)(cap.m_useLOD ? 0x01 : 0xff);
                    byte lodMask1 = 0xfe;
                    foreach (var mat in m_materialsCache)
                    {
                        var first = new MeshMaterialSubmeshSettings
                        {
                            mesh     = mesh,
                            material = mat,
                            submesh  = 0,
                            lodMask  = lodMask0
                        };
                        if (RenderingBakingTools.RequiresDepthSorting(mat))
                        {
                            mms[transparentIndex] = first;
                            transparentIndex++;
                        }
                        else
                        {
                            mms[opaqueIndex] = first;
                            opaqueIndex++;
                        }
                        if (!cap.m_useLOD)
                            continue;

                        var second = new MeshMaterialSubmeshSettings
                        {
                            mesh     = lodMesh,
                            material = mat,
                            submesh  = 0,
                            lodMask  = lodMask1
                        };
                        if (RenderingBakingTools.RequiresDepthSorting(mat))
                        {
                            mms[transparentIndex] = second;
                            transparentIndex++;
                        }
                        else
                        {
                            mms[opaqueIndex] = second;
                            opaqueIndex++;
                        }
                    }
                }
            }

            var entity           = CreateAdditionalEntity(TransformUsageFlags.Renderable);
            var rendererSettings = new MeshRendererBakeSettings
            {
                targetEntity                = entity,
                renderMeshDescription       = new RenderMeshDescription(primaryRenderer),
                isDeforming                 = false,
                suppressDeformationWarnings = false,
                useLightmapsIfPossible      = true,
                lightmapIndex               = primaryRenderer.lightmapIndex,
                lightmapScaleOffset         = primaryRenderer.lightmapScaleOffset,
                isStatic                    = IsStatic(),
                localBounds                 = bounds,
            };

            if (useLod)
            {
                AddComponent<UseMmiRangeLodTag>(entity);
                AddComponent<LodCrossfade>(     entity);
                AddComponent(                   entity, new MmiRange2LodSelect
                {
                    fullLod0ScreenHeightFraction = (half)(conservativeLodMax / 100f),
                    fullLod1ScreenHeightFraction = (half)(conservativeLodMin / 100f),
                });
                //half fraction1 = (half)(authoring.m_lodTransitionMinPercentage / 100f);
                //UnityEngine.Debug.Log($"Authored: {authoring.m_lodTransitionMinPercentage}, converted: {(float)fraction1}");
                if (conservativeLodMin > 2f)
                    UnityEngine.Debug.Log($"Bad LOD: {authoring.gameObject.name}, {authoring.transform.root.gameObject.name}");
            }

            if (opaqueCount == 0 || transparentCount == 0)
            {
                Span<MeshRendererBakeSettings> renderers = stackalloc MeshRendererBakeSettings[1];
                renderers[0]                             = rendererSettings;
                Span<int> count                          = stackalloc int[1];
                count[0]                                 = mms.Length;
                this.BakeMeshAndMaterial(renderers, mms, count);
            }
            else
            {
                var additionalEntity = CreateAdditionalEntity(TransformUsageFlags.Renderable, false, $"{GetName()}-TransparentRenderEntity");
                if (useLod)
                {
                    AddComponent<UseMmiRangeLodTag>(additionalEntity);
                    AddComponent<LodCrossfade>(     additionalEntity);
                    AddComponent(                   additionalEntity, new MmiRange2LodSelect
                    {
                        fullLod0ScreenHeightFraction = (half)(conservativeLodMax / 100f),
                        fullLod1ScreenHeightFraction = (half)(conservativeLodMin / 100f),
                    });
                    //half fraction1 = (half)(authoring.m_lodTransitionMinPercentage / 100f);
                    //UnityEngine.Debug.Log($"Authored: {authoring.m_lodTransitionMinPercentage}, converted: {(float)fraction1}");
                    if (conservativeLodMin > 2f)
                        UnityEngine.Debug.Log($"Bad LOD: {authoring.gameObject.name}, {authoring.transform.root.gameObject.name}");
                }

                Span<MeshRendererBakeSettings> renderers = stackalloc MeshRendererBakeSettings[2];
                renderers[0]                             = rendererSettings;
                renderers[1]                             = rendererSettings;
                renderers[1].targetEntity                = additionalEntity;
                Span<int> counts                         = stackalloc int[2];
                counts[0]                                = opaqueCount;
                counts[1]                                = transparentCount;
                this.BakeMeshAndMaterial(renderers, mms, counts);
            }
        }

        Mesh TransformMesh(Transform combinerTransform, Transform childRendererTransform, Mesh mesh)
        {
            var transform = AbstractBakingUtilities.ExtractTransformRelativeTo(childRendererTransform, combinerTransform);
            var positions = mesh.vertices;
            var normals   = mesh.normals;
            var tangents  = mesh.tangents;

            for (int i = 0; i < positions.Length; i++)
            {
                positions[i]   = qvvs.TransformPoint(in transform, positions[i]);
                normals[i]     = qvvs.TransformDirectionWithStretch(in transform, normals[i]);
                float4 tangent = tangents[i];
                tangent.xyz    = qvvs.TransformDirectionWithStretch(in transform, tangent.xyz);
                tangents[i]    = tangent;
            }

            var result      = new Mesh();
            result.vertices = positions;
            result.normals  = normals;
            result.tangents = tangents;

            if (mesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.Color))
                result.colors32 = mesh.colors32;
            if (mesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.TexCoord0))
                result.uv = mesh.uv;
            if (mesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.TexCoord1))
                result.uv2 = mesh.uv2;
            if (mesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.TexCoord2))
                result.uv3 = mesh.uv3;
            if (mesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.TexCoord3))
                result.uv4      = mesh.uv4;
            result.triangles    = mesh.triangles;
            result.subMeshCount = mesh.subMeshCount;
            for (int i = 0; i < mesh.subMeshCount; i++)
                result.SetSubMesh(i, mesh.GetSubMesh(i));
            result.name = $"{GetName()} - {mesh.name}";
            result.RecalculateBounds();
            return result;
        }
    }
}

