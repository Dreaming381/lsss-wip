using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Lsss.Editor
{
    public static class CapsuleMeshBakeout
    {
        [MenuItem("Lsss/Bake Meshes For All Capsule Prefabs")]
        public static void BakeMeshes()
        {
            AssetDatabase.DeleteAsset("Assets/CapsuleMeshBakes");
            AssetDatabase.CreateFolder("Assets", "CapsuleMeshBakes");

            if (Authoring.CapsuleMeshAuthoring.StaticMeshNull)
            {
                var go   = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                var mesh = go.GetComponent<MeshFilter>().sharedMesh;
                Object.DestroyImmediate(go);
                Authoring.CapsuleMeshAuthoring.SetStaticMesh(mesh);
            }

            //var allAuthoring = Resources.FindObjectsOfTypeAll<Authoring.CapsuleMeshAuthoring>();
            //Debug.Log($"Found {allAuthoring.Length} instances of CapsuleMeshAuthoring");
            //foreach (var authoring in allAuthoring)

            var guids            = AssetDatabase.FindAssets("t:Prefab");
            var alreadyProcessed = new HashSet<Authoring.CapsuleMeshAuthoring>();
            var allAuthorings    = new List<Authoring.CapsuleMeshAuthoring>();
            foreach (var guid in guids)
            {
                var prefabPath = AssetDatabase.GUIDToAssetPath(guid);
                var objects    = AssetDatabase.LoadAllAssetsAtPath(prefabPath);
                foreach (var obj in objects)
                {
                    var go = obj as GameObject;
                    if (go == null)
                        continue;

                    allAuthorings.Clear();
                    go.GetComponentsInChildren(allAuthorings);

                    foreach (var authoring in allAuthorings)
                    {
                        if (authoring.gameObject.scene.IsValid())
                            continue;

                        if (alreadyProcessed.Contains(authoring))
                            continue;

                        alreadyProcessed.Add(authoring);

                        var meshFilter = authoring.GetComponent<MeshFilter>();
                        if (meshFilter == null)
                            continue;
                        var existingMesh = meshFilter.sharedMesh;
                        if (existingMesh == null)
                        {
                            authoring.ProcessMesh(true);
                            existingMesh = authoring.GeneratedMesh;
                            Debug.Log($"Generated new mesh: {existingMesh.name}");
                        }

                        string targetPath   = $"Assets/CapsuleMeshBakes/{existingMesh.name}.mesh";
                        var    databaseMesh = AssetDatabase.LoadAssetAtPath<Mesh>(targetPath);
                        if (databaseMesh == null)
                        {
                            AssetDatabase.CreateAsset(existingMesh, targetPath);
                            databaseMesh = existingMesh;
                            Debug.Log($"Saving mesh path: {targetPath}");
                        }

                        meshFilter.sharedMesh = databaseMesh;
                    }
                }
            }

            AssetDatabase.SaveAssets();
        }
    }
}

