using System;
using System.Collections;
using System.Collections.Generic;
using Latios.Kinemation;
using Latios.Kinemation.Authoring;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine;

namespace Lsss.Authoring
{
    [RequireComponent(typeof(MeshFilter))]
    [AddComponentMenu("LSSS/Mesh Builders/Capsule")]
    public class CapsuleMeshAuthoring : MonoBehaviour, IOverrideMeshRenderer
    {
        public enum DirectionAxis
        {
            X,
            Y,
            Z
        }

        private Mesh m_mesh;

        public Mesh GeneratedMesh => m_mesh;

        public float         m_height = 2f;
        public float         m_radius = 0.5f;
        public DirectionAxis m_axis   = DirectionAxis.Y;

        public bool m_useLOD = false;
        [Range(0.0f, 100f)]
        public float m_lodTransitionMinPercentage = 0.5f;
        [Range(0.0f, 100f)]
        public float m_lodTransitionMaxPercentage = 1f;

        private static Mesh s_srcCapsuleMesh;

        private float         m_savedHeight = 2f;
        private float         m_savedRadius = 0.5f;
        private DirectionAxis m_savedAxis   = DirectionAxis.Y;

        private void OnValidate()
        {
            ProcessMesh();
        }

        private void Awake()
        {
            ProcessMesh();
        }

        public static bool StaticMeshNull => s_srcCapsuleMesh == null;
        public static void SetStaticMesh(Mesh mesh)
        {
            if (s_srcCapsuleMesh != null)
                return;

            s_srcCapsuleMesh = mesh;
        }

        public void ProcessMesh(bool runtime = false)
        {
            if (s_srcCapsuleMesh == null)
            {
                if (runtime)
                {
                    var go           = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    s_srcCapsuleMesh = go.GetComponent<MeshFilter>().sharedMesh;
                    DestroyImmediate(go);
                }
#if UNITY_EDITOR
                else
                    UnityEditor.EditorApplication.delayCall += () =>
                    {
                        if (this == null)
                            return;
                        var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                        s_srcCapsuleMesh = go.GetComponent<MeshFilter>().sharedMesh;
                        DestroyImmediate(go);
                        ProcessMesh();
                    };
#endif
                if (!runtime)
                    return;
            }

            bool dirty = false;

            char axname          = m_axis == DirectionAxis.X ? 'x' : m_axis == DirectionAxis.Y ? 'y' : 'z';
            var  capsuleMeshName = $"capsule_{axname}_{m_radius}_{m_height}";

            var mfmesh = GetComponent<MeshFilter>().sharedMesh;
            if (mfmesh != null && m_mesh == null && capsuleMeshName == mfmesh.name)
            {
                m_mesh = mfmesh;
                return;
            }
            if (m_mesh == null || capsuleMeshName != m_mesh.name || mfmesh == null || m_mesh.name != mfmesh.name || runtime)
            {
                m_mesh = new Mesh();
                if (runtime)
                    GetComponent<MeshFilter>().sharedMesh = m_mesh;
#if UNITY_EDITOR
                UnityEditor.EditorApplication.delayCall += () =>
                {
                    if (this == null)
                        return;
                    GetComponent<MeshFilter>().sharedMesh = m_mesh;
                };
#endif

                dirty = true;
            }
            dirty |= m_height != m_savedHeight ||
                     m_radius != m_savedRadius ||
                     m_axis != m_savedAxis;

            if (dirty)
            {
                Quaternion rot = Quaternion.identity;
                if (m_axis == DirectionAxis.Z)
                {
                    rot = Quaternion.Euler(90f, 0f, 0f);
                }
                else if (m_axis == DirectionAxis.X)
                {
                    rot = Quaternion.Euler(0f, 0f, 90f);
                }

                var positions = s_srcCapsuleMesh.vertices;

                for (int i = 0; i < positions.Length; i++)
                {
                    var p = positions[i];
                    if (p.y > 0f)
                    {
                        p.y -= 0.5f;
                        p   *= m_radius * 2f;
                        p.y += m_height / 2f - m_radius;
                    }
                    else
                    {
                        p.y += 0.5f;
                        p   *= m_radius * 2f;
                        p.y -= m_height / 2f - m_radius;
                    }
                    p            = rot * p;
                    positions[i] = p;
                }

                m_mesh.vertices = positions;

                if (m_axis != DirectionAxis.Y)
                {
                    var normals  = s_srcCapsuleMesh.normals;
                    var tangents = s_srcCapsuleMesh.tangents;
                    for (int i = 0; i < normals.Length; i++)
                    {
                        normals[i]  = rot * normals[i];
                        tangents[i] = rot * tangents[i];
                    }
                    m_mesh.normals  = normals;
                    m_mesh.tangents = tangents;
                }
                else
                {
                    m_mesh.normals  = s_srcCapsuleMesh.normals;
                    m_mesh.tangents = s_srcCapsuleMesh.tangents;
                }
                m_mesh.uv        = s_srcCapsuleMesh.uv;
                m_mesh.uv2       = s_srcCapsuleMesh.uv2;
                m_mesh.triangles = s_srcCapsuleMesh.triangles;

                var filter = GetComponent<MeshFilter>();

                var capCol = GetComponent<CapsuleCollider>();
                if (capCol != null)
                {
                    capCol.direction = (int)m_axis;
                    capCol.center    = Vector3.zero;
                    capCol.height    = m_height;
                    capCol.radius    = m_radius;
                }

                m_savedAxis   = m_axis;
                m_savedHeight = m_height;
                m_savedRadius = m_radius;
                m_mesh.name   = capsuleMeshName;
            }
        }

        public Mesh ProcessMeshForBake()
        {
            if (s_srcCapsuleMesh == null)
            {
                var go           = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                s_srcCapsuleMesh = go.GetComponent<MeshFilter>().sharedMesh;
                DestroyImmediate(go);
            }

            char axname          = m_axis == DirectionAxis.X ? 'x' : m_axis == DirectionAxis.Y ? 'y' : 'z';
            var  capsuleMeshName = $"capsule_{axname}_{m_radius}_{m_height}";
            var  mesh            = new Mesh();

            Quaternion rot = Quaternion.identity;
            if (m_axis == DirectionAxis.Z)
            {
                rot = Quaternion.Euler(90f, 0f, 0f);
            }
            else if (m_axis == DirectionAxis.X)
            {
                rot = Quaternion.Euler(0f, 0f, 90f);
            }

            var positions = s_srcCapsuleMesh.vertices;

            for (int i = 0; i < positions.Length; i++)
            {
                var p = positions[i];
                if (p.y > 0f)
                {
                    p.y -= 0.5f;
                    p   *= m_radius * 2f;
                    p.y += m_height / 2f - m_radius;
                }
                else
                {
                    p.y += 0.5f;
                    p   *= m_radius * 2f;
                    p.y -= m_height / 2f - m_radius;
                }
                p            = rot * p;
                positions[i] = p;
            }

            mesh.vertices = positions;

            if (m_axis != DirectionAxis.Y)
            {
                var normals  = s_srcCapsuleMesh.normals;
                var tangents = s_srcCapsuleMesh.tangents;
                for (int i = 0; i < normals.Length; i++)
                {
                    normals[i]  = rot * normals[i];
                    tangents[i] = rot * tangents[i];
                }
                mesh.normals  = normals;
                mesh.tangents = tangents;
            }
            else
            {
                mesh.normals  = s_srcCapsuleMesh.normals;
                mesh.tangents = s_srcCapsuleMesh.tangents;
            }
            mesh.uv        = s_srcCapsuleMesh.uv;
            mesh.uv2       = s_srcCapsuleMesh.uv2;
            mesh.triangles = s_srcCapsuleMesh.triangles;

            var capCol = GetComponent<CapsuleCollider>();
            if (capCol != null)
            {
                capCol.direction = (int)m_axis;
                capCol.center    = Vector3.zero;
                capCol.height    = m_height;
                capCol.radius    = m_radius;
            }

            m_savedAxis   = m_axis;
            m_savedHeight = m_height;
            m_savedRadius = m_radius;
            mesh.name     = capsuleMeshName;

            return mesh;
        }
    }

    public class CapsuleMeshBaker : Baker<CapsuleMeshAuthoring>
    {
        List<Material>                  m_materialsCache         = new List<Material>();
        List<RendererCombinerAuthoring> m_rendererCombiningCache = new List<RendererCombinerAuthoring>();

        public override void Bake(CapsuleMeshAuthoring authoring)
        {
            var meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer == null)
                return;

            m_rendererCombiningCache.Clear();
            GetComponentsInParent(m_rendererCombiningCache);
            foreach (var rc in  m_rendererCombiningCache)
            {
                if (rc.ShouldOverride(this, meshRenderer))
                    return;
            }

            m_materialsCache.Clear();
            meshRenderer.GetSharedMaterials(m_materialsCache);

            var mesh = authoring.ProcessMeshForBake();

            var                               materialCount = m_materialsCache.Count;
            Span<MeshMaterialSubmeshSettings> mms           = stackalloc MeshMaterialSubmeshSettings[materialCount * math.select(1, 2, authoring.m_useLOD)];
            RenderingBakingTools.ExtractMeshMaterialSubmeshes(mms.Slice(0, materialCount), mesh, m_materialsCache);
            var opaqueMaterialCount = RenderingBakingTools.GroupByDepthSorting(mms);

            var entity = GetEntity(TransformUsageFlags.Renderable);
            RenderingBakingTools.GetLOD(this, meshRenderer, out var lodSettings);
            if (authoring.m_useLOD)
            {
                var lodMesh = CreateLODMesh(authoring.m_height, authoring.m_radius, authoring.m_axis);
                for (int i = 0; i < materialCount; i++)
                {
                    // Todo: This is technically bugged for transparent materials.
                    mms[materialCount + i] = new MeshMaterialSubmeshSettings
                    {
                        material = mms[i].material,
                        mesh     = lodMesh,
                        submesh  = 0,
                        lodMask  = 0xfe
                    };
                    mms[i].lodMask = 0x1;
                }

                AddComponent<UseMmiRangeLodTag>(entity);
                AddComponent<LodCrossfade>(     entity);
                AddComponent(                   entity, new MmiRange2LodSelect
                {
                    height                       = authoring.m_height,
                    fullLod0ScreenHeightFraction = (half)(authoring.m_lodTransitionMaxPercentage / 100f),
                    fullLod1ScreenHeightFraction = (half)(authoring.m_lodTransitionMinPercentage / 100f),
                });
                //half fraction1 = (half)(authoring.m_lodTransitionMinPercentage / 100f);
                //UnityEngine.Debug.Log($"Authored: {authoring.m_lodTransitionMinPercentage}, converted: {(float)fraction1}");
                if (authoring.m_lodTransitionMinPercentage > 2f)
                    UnityEngine.Debug.Log($"Bad LOD: {authoring.gameObject.name}, {authoring.transform.root.gameObject.name}");
            }
            else
            {
                RenderingBakingTools.BakeLodMaskForEntity(this, entity, lodSettings);
            }

            var rendererSettings = new MeshRendererBakeSettings
            {
                targetEntity                = entity,
                renderMeshDescription       = new RenderMeshDescription(meshRenderer),
                isDeforming                 = false,
                suppressDeformationWarnings = false,
                useLightmapsIfPossible      = true,
                lightmapIndex               = meshRenderer.lightmapIndex,
                lightmapScaleOffset         = meshRenderer.lightmapScaleOffset,
                isStatic                    = IsStatic(),
                localBounds                 = mesh != null ? mesh.bounds : default,
            };

            if (opaqueMaterialCount == mms.Length || opaqueMaterialCount == 0)
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
                if (authoring.m_useLOD)
                {
                    AddComponent<UseMmiRangeLodTag>(additionalEntity);
                }
                else
                {
                    RenderingBakingTools.BakeLodMaskForEntity(this, additionalEntity, lodSettings);
                }

                Span<MeshRendererBakeSettings> renderers = stackalloc MeshRendererBakeSettings[2];
                renderers[0]                             = rendererSettings;
                renderers[1]                             = rendererSettings;
                renderers[1].targetEntity                = additionalEntity;
                Span<int> counts                         = stackalloc int[2];
                counts[0]                                = opaqueMaterialCount;
                counts[1]                                = mms.Length - opaqueMaterialCount;
                this.BakeMeshAndMaterial(renderers, mms, counts);
            }
        }

        public static Mesh CreateLODMesh(float height, float radius, CapsuleMeshAuthoring.DirectionAxis axis)
        {
            var mesh      = new Mesh();
            var positions = new NativeArray<float3>(10, Allocator.Temp);

            var halfHeight    = height / 2f;
            var radialExtents = radius / math.sqrt(2f);

            positions[0] = new float3(-halfHeight, 0f, 0f);
            positions[1] = new float3(-halfHeight + radius, -radialExtents, -radialExtents);
            positions[2] = new float3(-halfHeight + radius, radialExtents, -radialExtents);
            positions[3] = new float3(-halfHeight + radius, radialExtents, radialExtents);
            positions[4] = new float3(-halfHeight + radius, -radialExtents, radialExtents);
            positions[5] = new float3(halfHeight - radius, -radialExtents, -radialExtents);
            positions[6] = new float3(halfHeight - radius, radialExtents, -radialExtents);
            positions[7] = new float3(halfHeight - radius, radialExtents, radialExtents);
            positions[8] = new float3(halfHeight - radius, -radialExtents, radialExtents);
            positions[9] = new float3(halfHeight, 0f, 0f);

            if (axis == CapsuleMeshAuthoring.DirectionAxis.Y)
            {
                for (int i = 0; i < positions.Length; i++)
                {
                    var p        = positions[i];
                    p            = p.yxz;
                    positions[i] = p;
                }
            }
            else if (axis == CapsuleMeshAuthoring.DirectionAxis.Z)
            {
                for (int i = 0; i < positions.Length; i++)
                {
                    var p        = positions[i];
                    p            = p.yzx;
                    positions[i] = p;
                }
            }

            var triangles = new NativeArray<int3>(16, Allocator.Temp);
            triangles[0]  = new int3(1, 5, 6);
            triangles[1]  = new int3(1, 6, 2);
            triangles[2]  = new int3(2, 6, 7);
            triangles[3]  = new int3(2, 7, 3);
            triangles[4]  = new int3(3, 7, 8);
            triangles[5]  = new int3(3, 8, 4);
            triangles[6]  = new int3(4, 8, 5);
            triangles[7]  = new int3(4, 5, 1);
            triangles[8]  = new int3(0, 1, 2);
            triangles[9]  = new int3(0, 2, 3);
            triangles[10] = new int3(0, 3, 4);
            triangles[11] = new int3(0, 4, 1);
            triangles[12] = new int3(5, 6, 9);
            triangles[13] = new int3(6, 7, 9);
            triangles[14] = new int3(7, 8, 9);
            triangles[15] = new int3(8, 5, 9);

            mesh.SetVertices(positions);
            mesh.SetTriangles(triangles.Reinterpret<int>(12).ToArray(), 0);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            return mesh;
        }
    }
}

