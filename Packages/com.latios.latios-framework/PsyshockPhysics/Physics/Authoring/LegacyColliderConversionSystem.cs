using System.Collections.Generic;
using Latios.Unsafe;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace Latios.Psyshock.Authoring.Systems
{
    [ConverterVersion("latios", 3)]
    public class LegacyColliderConversionSystem : GameObjectConversionSystem
    {
        protected override void OnUpdate()
        {
            Entities.WithNone<DontConvertColliderTag>().ForEach((UnityEngine.SphereCollider goSphere) =>
            {
                DeclareDependency(goSphere,
                                  goSphere.transform);
                float3 lossyScale = goSphere.transform.lossyScale;
                if (math.cmax(lossyScale) - math.cmin(lossyScale) > 1.0E-5f)
                {
                    UnityEngine.Debug.LogWarning(
                        $"Failed to convert {goSphere}. Only uniform scaling is supported on SphereCollider. Lossy Scale divergence was: {math.cmax(lossyScale) - math.cmin(lossyScale)}");
                    return;
                }

                Entity   entity    = GetPrimaryEntity(goSphere);
                Collider icdSphere = new SphereCollider
                {
                    center = goSphere.center,
                    radius = goSphere.radius * goSphere.transform.localScale.x
                };
                DstEntityManager.AddComponentData(entity, icdSphere);
            });

            Entities.WithNone<DontConvertColliderTag>().ForEach((UnityEngine.CapsuleCollider goCap) =>
            {
                DeclareDependency(goCap,
                                  goCap.transform);
                float3 lossyScale = goCap.transform.lossyScale;
                if (math.cmax(lossyScale) - math.cmin(lossyScale) > 1.0E-5f)
                {
                    UnityEngine.Debug.LogWarning(
                        $"Failed to convert { goCap }. Only uniform scaling is supported on CapsuleCollider. Lossy Scale divergence was: {math.cmax(lossyScale) - math.cmin(lossyScale)}");
                    return;
                }

                Entity entity = GetPrimaryEntity(goCap);
                float3 dir;
                if (goCap.direction == 0)
                {
                    dir = new float3(1f, 0f, 0f);
                }
                else if (goCap.direction == 1)
                {
                    dir = new float3(0f, 1, 0f);
                }
                else
                {
                    dir = new float3(0f, 0f, 1f);
                }
                Collider icdCap = new CapsuleCollider
                {
                    pointB = (float3)goCap.center + ((goCap.height / 2f - goCap.radius) * goCap.transform.lossyScale.x * dir),
                    pointA = (float3)goCap.center - ((goCap.height / 2f - goCap.radius) * goCap.transform.lossyScale.x * dir),
                    radius = goCap.radius * goCap.transform.lossyScale.x
                };
                DstEntityManager.AddComponentData(entity, icdCap);
            });

            Entities.WithNone<DontConvertColliderTag>().ForEach((UnityEngine.BoxCollider goBox) =>
            {
                DeclareDependency(goBox, goBox.transform);
                float3 lossyScale = goBox.transform.lossyScale;

                Entity   entity = GetPrimaryEntity(goBox);
                Collider icdBox = new BoxCollider
                {
                    center   = goBox.center,
                    halfSize = goBox.size * lossyScale / 2f
                };
                DstEntityManager.AddComponentData(entity, icdBox);
            });

            var convexUnityList = new List<UnityEngine.MeshCollider>();

            Entities.WithNone<DontConvertColliderTag>().ForEach((UnityEngine.MeshCollider goMesh) =>
            {
                DeclareDependency(goMesh, goMesh.transform);
                DeclareAssetDependency(goMesh.gameObject, goMesh.sharedMesh);

                if (goMesh.convex && goMesh.sharedMesh != null)
                    convexUnityList.Add(goMesh);
            });
            ConvertConvexColliders(convexUnityList);
        }

        void ConvertConvexColliders(List<UnityEngine.MeshCollider> unityColliders)
        {
            if (unityColliders.Count == 0)
                return;

            //Temporary workaround
            var tempList = new NativeList<int>(Allocator.TempJob);
            for (int i = 0; i < 5; i++)
                tempList.Add(i);
            unsafe
            {
                var result = xxHash3.Hash128(tempList.GetUnsafePtr(), tempList.Length * 4);
            }
            tempList.Dispose();

            var meshList             = new List<UnityEngine.Mesh>(unityColliders.Count);
            var meshNames            = new NativeList<FixedString128Bytes>(unityColliders.Count, Allocator.TempJob);
            var computationDataArray = new NativeList<ConvexComputationData>(unityColliders.Count, Allocator.TempJob);
            var deduplicatedMeshMap  = new NativeHashMap<int, int>(unityColliders.Count, Allocator.TempJob);

            foreach(var uc in unityColliders)
            {
                if (deduplicatedMeshMap.TryAdd(uc.sharedMesh.GetInstanceID(), computationDataArray.Length))
                {
                    meshList.Add(uc.sharedMesh);
                    meshNames.Add(uc.sharedMesh.name);
                    computationDataArray.Add(new ConvexComputationData { meshIndex = computationDataArray.Length });
                }
            }

            var meshDataArray = UnityEngine.Mesh.AcquireReadOnlyMeshData(meshList);
            new ComputeConvexHashesJob
            {
                meshes    = meshDataArray,
                meshNames = meshNames,
                dataArray = computationDataArray
            }.ScheduleParallel(computationDataArray.Length, 1, default).Complete();

            var computationContext = new BlobAssetComputationContext<ConvexComputationData, ConvexColliderBlob>(BlobAssetStore, 128, Allocator.Temp);
            for (int i = 0; i < computationDataArray.Length; i++)
            {
                computationContext.AssociateBlobAssetWithUnityObject(computationDataArray[i].hash, meshList[i]);
                if (computationContext.NeedToComputeBlobAsset(computationDataArray[i].hash))
                    computationContext.AddBlobAssetToCompute(computationDataArray[i].hash, computationDataArray[i]);
            }

            var needsComputeDataArray = computationContext.GetSettings(Allocator.TempJob);
            new ComputeConvexBlobsJob
            {
                meshes    = meshDataArray,
                meshNames = meshNames,
                dataArray = needsComputeDataArray
                            //}.ScheduleParallel(needsComputeDataArray.Length, 1, default).Complete();
            }.Execute(0);

            foreach (var c in needsComputeDataArray)
            {
                computationContext.AddComputedBlobAsset(c.hash, c.blobReference);
            }

            foreach (var c in unityColliders)
            {
                computationContext.GetBlobAsset(computationDataArray[deduplicatedMeshMap[c.sharedMesh.GetInstanceID()]].hash, out var blob);
                Collider collider = new ConvexCollider(blob);
                DstEntityManager.AddComponentData(GetPrimaryEntity(c), collider);
            }

            meshNames.Dispose();
            meshDataArray.Dispose();
            computationDataArray.Dispose();
            deduplicatedMeshMap.Dispose();
            needsComputeDataArray.Dispose();
            computationContext.Dispose();
        }

        struct ConvexComputationData
        {
            public int                                    meshIndex;
            public Hash128                                hash;
            public BlobAssetReference<ConvexColliderBlob> blobReference;
        }

        [BurstCompile]
        struct ComputeConvexHashesJob : IJobFor
        {
            [ReadOnly] public UnityEngine.Mesh.MeshDataArray   meshes;
            [ReadOnly] public NativeArray<FixedString128Bytes> meshNames;

            public NativeArray<ConvexComputationData> dataArray;

            public unsafe void Execute(int index)
            {
                var mesh       = meshes[index];
                var data       = dataArray[index];
                data.meshIndex = index;

                var hashHigh = xxHash3.Hash64(meshNames[index]);
                var hashLow  = hashHigh;
                for (int i = 0; i < mesh.vertexBufferCount; i++)
                {
                    var buffer = mesh.GetVertexData<byte>(i);
                    hashLow    = xxHash3.Hash64(buffer.GetUnsafeReadOnlyPtr(), buffer.Length, ((ulong)hashLow.y << 32) | hashLow.x);
                }
                data.hash        = new Hash128(hashLow.x, hashLow.y, hashHigh.x, hashHigh.y);
                dataArray[index] = data;
            }
        }

        [BurstCompile]
        struct ComputeConvexBlobsJob : IJobFor
        {
            [ReadOnly] public UnityEngine.Mesh.MeshDataArray   meshes;
            [ReadOnly] public NativeArray<FixedString128Bytes> meshNames;

            [NativeDisableContainerSafetyRestriction] NativeList<UnityEngine.Vector3> vector3Cache;

            public NativeArray<ConvexComputationData> dataArray;

            public unsafe void Execute(int index)
            {
                if (!vector3Cache.IsCreated)
                {
                    vector3Cache = new NativeList<UnityEngine.Vector3>(Allocator.Temp);
                }

                var builder = new BlobBuilder(Allocator.Temp);

                ref var blobRoot = ref builder.ConstructRoot<ConvexColliderBlob>();
                var     data     = dataArray[index];
                var     mesh     = meshes[data.meshIndex];

                blobRoot.meshName     = meshNames[data.meshIndex];
                blobRoot.authoredHash = data.hash;

                vector3Cache.ResizeUninitialized(mesh.vertexCount);
                mesh.GetVertices(vector3Cache);

                // ConvexHullBuilder doesn't seem to properly check duplicated vertices when they are nice numbers.
                // So we deduplicate things ourselves.
                var hashedMeshVertices = new NativeHashSet<float3>(vector3Cache.Length, Allocator.Temp);
                for (int i = 0; i < vector3Cache.Length; i++)
                    hashedMeshVertices.Add(vector3Cache[i]);
                var meshVertices = hashedMeshVertices.ToNativeArray(Allocator.Temp);

                // These are the default Unity uses except with 0 bevel radius.
                // They don't matter too much since Unity is allowed to violate them anyways to meet storage constraints.
                var parameters = new ConvexHullGenerationParameters
                {
                    BevelRadius             = 0f,
                    MinimumAngle            = math.radians(2.5f),
                    SimplificationTolerance = 0.015f
                };
                // These are the default storage constraints Unity uses.
                // Changing them will likely break EPA.
                int maxVertices     = 252;
                int maxFaces        = 252;
                int maxFaceVertices = 32;

                var convexHullBuilder = new ConvexHullBuilder(meshVertices, parameters, maxVertices, maxFaces, maxFaceVertices, out float convexRadius);
                // Todo: We should handle 2D convex colliders more elegantly.
                // Right now our queries don't consider it.
                if (convexHullBuilder.dimension != 3)
                {
                    parameters.MinimumAngle            = 0f;
                    parameters.SimplificationTolerance = 0f;
                    convexHullBuilder                  = new ConvexHullBuilder(meshVertices, parameters, maxVertices, maxFaces, maxFaceVertices, out convexRadius);
                }
                UnityEngine.Assertions.Assert.IsTrue(convexRadius < math.EPSILON);

                // Based on Unity.Physics - ConvexCollider.cs
                var vertices       = new NativeList<float3>(convexHullBuilder.vertices.peakCount, Allocator.Temp);
                var indexVertexMap = new NativeArray<byte>(convexHullBuilder.vertices.peakCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

                foreach (int vIndex in convexHullBuilder.vertices.indices)
                {
                    indexVertexMap[vIndex] = (byte)vertices.Length;
                    vertices.Add(convexHullBuilder.vertices[vIndex].position);
                }

                var facePlanes                        = new NativeList<Plane>(convexHullBuilder.numFaces, Allocator.Temp);
                var edgeIndicesInFaces                = new NativeList<int>(convexHullBuilder.numFaceVertices, Allocator.Temp);
                var edgeIndicesInFacesStartsAndCounts = new NativeList<int2>(convexHullBuilder.numFaces, Allocator.Temp);
                var vertexIndicesInEdges              = new NativeList<int2>(convexHullBuilder.vertices.peakCount, Allocator.Temp);
                var edgeHashMap                       = new NativeHashMap<int2, int>(convexHullBuilder.vertices.peakCount, Allocator.Temp);
                var edgeFlippedInFaces                = new NativeList<bool>(convexHullBuilder.numFaceVertices, Allocator.Temp);

                var tempVerticesInFace = new NativeList<int>(Allocator.Temp);

                for (ConvexHullBuilder.FaceEdge hullFace = convexHullBuilder.GetFirstFace(); hullFace.isValid; hullFace = convexHullBuilder.GetNextFace(hullFace))
                {
                    ConvexHullBuilder.Edge firstEdge = hullFace;
                    Plane                  facePlane = convexHullBuilder.planes[convexHullBuilder.triangles[firstEdge.triangleIndex].faceIndex];
                    facePlanes.Add(facePlane);

                    // Walk the face's outer vertices & edges
                    tempVerticesInFace.Clear();
                    for (ConvexHullBuilder.FaceEdge edge = hullFace; edge.isValid; edge = convexHullBuilder.GetNextFaceEdge(edge))
                    {
                        tempVerticesInFace.Add(indexVertexMap[convexHullBuilder.StartVertex(edge)]);
                    }
                    UnityEngine.Assertions.Assert.IsTrue(tempVerticesInFace.Length >= 3);

                    // The rest of this is custom.
                    int edgeIndicesInFaceStart = edgeIndicesInFaces.Length;
                    int previousVertexIndex    = tempVerticesInFace[tempVerticesInFace.Length - 1];
                    for (int i = 0; i < tempVerticesInFace.Length; i++)
                    {
                        int2 edge           = new int2(previousVertexIndex, tempVerticesInFace[i]);
                        previousVertexIndex = tempVerticesInFace[i];
                        if (edgeHashMap.TryGetValue(edge, out var edgeIndex))
                        {
                            edgeIndicesInFaces.Add(edgeIndex);
                            edgeFlippedInFaces.Add(false);
                        }
                        else if (edgeHashMap.TryGetValue(edge.yx, out edgeIndex))
                        {
                            edgeIndicesInFaces.Add(edgeIndex);
                            edgeFlippedInFaces.Add(true);
                        }
                        else
                        {
                            edgeIndex = vertexIndicesInEdges.Length;
                            vertexIndicesInEdges.Add(edge);
                            edgeHashMap.Add(edge, edgeIndex);
                            edgeIndicesInFaces.Add(edgeIndex);
                            edgeFlippedInFaces.Add(false);
                        }
                    }
                    edgeIndicesInFacesStartsAndCounts.Add(new int2(edgeIndicesInFaceStart, tempVerticesInFace.Length));
                }

                var viie = builder.ConstructFromNativeArray(ref blobRoot.vertexIndicesInEdges,
                                                            (int2*)vertexIndicesInEdges.GetUnsafeReadOnlyPtr(),
                                                            vertexIndicesInEdges.Length);
                var eiif    = builder.ConstructFromNativeArray(ref blobRoot.edgeIndicesInFaces, (int*)edgeIndicesInFaces.GetUnsafeReadOnlyPtr(), edgeIndicesInFaces.Length);
                var eiifsac = builder.ConstructFromNativeArray(ref blobRoot.edgeIndicesInFacesStartsAndCounts,
                                                               (int2*)edgeIndicesInFacesStartsAndCounts.GetUnsafeReadOnlyPtr(),
                                                               edgeIndicesInFacesStartsAndCounts.Length);

                var edgeNormals = builder.Allocate(ref blobRoot.edgeNormals, vertexIndicesInEdges.Length);
                for (int i = 0; i < edgeNormals.Length; i++)
                    edgeNormals[i] = float3.zero;

                var facePlaneX    = builder.Allocate(ref blobRoot.facePlaneX, edgeIndicesInFacesStartsAndCounts.Length);
                var facePlaneY    = builder.Allocate(ref blobRoot.facePlaneY, edgeIndicesInFacesStartsAndCounts.Length);
                var facePlaneZ    = builder.Allocate(ref blobRoot.facePlaneZ, edgeIndicesInFacesStartsAndCounts.Length);
                var facePlaneDist = builder.Allocate(ref blobRoot.facePlaneDist, edgeIndicesInFacesStartsAndCounts.Length);

                var faceEdgeOutwardPlanes = builder.Allocate(ref blobRoot.faceEdgeOutwardPlanes, edgeIndicesInFaces.Length);

                for (int faceIndex = 0; faceIndex < edgeIndicesInFacesStartsAndCounts.Length; faceIndex++)
                {
                    var plane                = facePlanes[faceIndex];
                    facePlaneX[faceIndex]    = plane.normal.x;
                    facePlaneY[faceIndex]    = plane.normal.y;
                    facePlaneZ[faceIndex]    = plane.normal.z;
                    facePlaneDist[faceIndex] = plane.distanceFromOrigin;

                    var edgeIndicesStartAndCount = edgeIndicesInFacesStartsAndCounts[faceIndex];

                    bool ccw = true;
                    {
                        int2 abIndices = vertexIndicesInEdges[edgeIndicesInFaces[edgeIndicesStartAndCount.x]];
                        int2 cIndices  = vertexIndicesInEdges[edgeIndicesInFaces[edgeIndicesStartAndCount.x + 1]];
                        if (edgeFlippedInFaces[edgeIndicesStartAndCount.x])
                            abIndices = abIndices.yx;
                        if (edgeFlippedInFaces[edgeIndicesStartAndCount.x + 1])
                            cIndices = cIndices.yx;
                        float3 a     = vertices[abIndices.x];
                        float3 b     = vertices[abIndices.y];
                        float3 c     = vertices[cIndices.y];
                        ccw          = math.dot(math.cross(plane.normal, b - a), c - a) < 0f;
                    }

                    for (int faceEdgeIndex = edgeIndicesStartAndCount.x; faceEdgeIndex < edgeIndicesStartAndCount.x + edgeIndicesStartAndCount.y; faceEdgeIndex++)
                    {
                        int edgeIndex           = edgeIndicesInFaces[faceEdgeIndex];
                        edgeNormals[edgeIndex] += plane.normal;

                        int2 abIndices = vertexIndicesInEdges[edgeIndex];
                        if (edgeFlippedInFaces[faceEdgeIndex])
                            abIndices                        = abIndices.yx;
                        float3 a                             = vertices[abIndices.x];
                        float3 b                             = vertices[abIndices.y];
                        var    outwardNormal                 = math.cross(plane.normal, b - a);
                        outwardNormal                        = math.select(-outwardNormal, outwardNormal, ccw);
                        faceEdgeOutwardPlanes[faceEdgeIndex] = new Plane(outwardNormal, -math.dot(outwardNormal, a));
                    }
                }

                var vertexNormals = builder.Allocate(ref blobRoot.vertexNormals, vertices.Length);
                for (int i = 0; i < vertexNormals.Length; i++)
                    vertexNormals[i] = float3.zero;

                for (int edgeIndex = 0; edgeIndex < vertexIndicesInEdges.Length; edgeIndex++)
                {
                    edgeNormals[edgeIndex] = math.normalize(edgeNormals[edgeIndex]);

                    var abIndices               = vertexIndicesInEdges[edgeIndex];
                    vertexNormals[abIndices.x] += edgeNormals[edgeIndex];
                    vertexNormals[abIndices.y] += edgeNormals[edgeIndex];

                    float3 a = vertices[abIndices.x];
                    float3 b = vertices[abIndices.y];
                }

                var verticesX = builder.Allocate(ref blobRoot.verticesX, vertices.Length);
                var verticesY = builder.Allocate(ref blobRoot.verticesY, vertices.Length);
                var verticesZ = builder.Allocate(ref blobRoot.verticesZ, vertices.Length);

                Aabb aabb = new Aabb(vertices[0], vertices[0]);

                for (int vertexIndex = 0; vertexIndex < vertices.Length; vertexIndex++)
                {
                    var vertex = vertices[vertexIndex];

                    verticesX[vertexIndex] = vertex.x;
                    verticesY[vertexIndex] = vertex.y;
                    verticesZ[vertexIndex] = vertex.z;

                    aabb = Physics.CombineAabb(vertex, aabb);

                    vertexNormals[vertexIndex] = math.normalize(vertexNormals[vertexIndex]);
                }

                blobRoot.localAabb = aabb;

                data.blobReference = builder.CreateBlobAssetReference<ConvexColliderBlob>(Allocator.Persistent);
                dataArray[index]   = data;
            }
        }
    }
}

