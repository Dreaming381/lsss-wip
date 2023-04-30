using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

using Latios.Kinemation.Authoring;

namespace Lsss.Authoring
{
    [RequireComponent(typeof(MeshFilter))]
    [AddComponentMenu("LSSS/Mesh Builders/Capsule")]
    public class CapsuleMeshAuthoring : OverrideMeshRendererBase
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
        List<Material> m_materialsCache = new List<Material>();

        public override void Bake(CapsuleMeshAuthoring authoring)
        {
            var meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer == null)
                return;

            m_materialsCache.Clear();
            meshRenderer.GetSharedMaterials(m_materialsCache);

            var mesh = authoring.ProcessMeshForBake();

            this.BakeMeshAndMaterial(meshRenderer, mesh, m_materialsCache);
        }
    }
}

