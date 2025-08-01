using Latios.Transforms;
using Unity.Burst;
using Unity.Mathematics;

namespace Latios.Psyshock
{
    internal static class PointRayDispatch
    {
        public static bool DistanceBetween(float3 point, in Collider collider, in TransformQvvs transform, float maxDistance, out PointDistanceResult result)
        {
            var rigidTransform = new RigidTransform(transform.rotation, transform.position);
            switch (collider.type)
            {
                case ColliderType.Sphere:
                    var sphere = collider.m_sphere;
                    Physics.ScaleStretchCollider(ref sphere, transform.scale, transform.stretch);
                    return PointRaySphere.DistanceBetween(point, in sphere, in rigidTransform, maxDistance, out result);
                case ColliderType.Capsule:
                    var capsule = collider.m_capsule;
                    Physics.ScaleStretchCollider(ref capsule, transform.scale, transform.stretch);
                    return PointRayCapsule.DistanceBetween(point, in capsule, in rigidTransform, maxDistance, out result);
                case ColliderType.Box:
                    var box = collider.m_box;
                    Physics.ScaleStretchCollider(ref box, transform.scale, transform.stretch);
                    return PointRayBox.DistanceBetween(point, in box, in rigidTransform, maxDistance, out result);
                case ColliderType.Triangle:
                    var triangle = collider.m_triangle;
                    Physics.ScaleStretchCollider(ref triangle, transform.scale, transform.stretch);
                    return PointRayTriangle.DistanceBetween(point, in triangle, in rigidTransform, maxDistance, out result);
                case ColliderType.Convex:
                    var convex = collider.m_convex;
                    Physics.ScaleStretchCollider(ref convex, transform.scale, transform.stretch);
                    return PointRayConvex.DistanceBetween(point, in convex, in rigidTransform, maxDistance, out result);
                case ColliderType.TriMesh:
                    var triMesh = collider.m_triMesh();
                    Physics.ScaleStretchCollider(ref triMesh, transform.scale, transform.stretch);
                    return PointRayTriMesh.DistanceBetween(point, in triMesh, in rigidTransform, maxDistance, out result);
                case ColliderType.Compound:
                    var compound = collider.m_compound();
                    Physics.ScaleStretchCollider(ref compound, transform.scale, transform.stretch);
                    return PointRayCompound.DistanceBetween(point, in compound, in rigidTransform, maxDistance, out result);
                case ColliderType.Terrain:
                    var terrain = collider.m_terrain();
                    Physics.ScaleStretchCollider(ref terrain, transform.scale, transform.stretch);
                    return PointRayTerrain.DistanceBetween(point, in terrain, in rigidTransform, maxDistance, out result);
                default:
                    result = default;
                    return false;
            }
        }

        public static bool Raycast(in Ray ray, in Collider collider, in TransformQvvs transform, out RaycastResult result)
        {
            var rigidTransform = new RigidTransform(transform.rotation, transform.position);
            switch (collider.type)
            {
                case ColliderType.Sphere:
                    var sphere = collider.m_sphere;
                    Physics.ScaleStretchCollider(ref sphere, transform.scale, transform.stretch);
                    return PointRaySphere.Raycast(in ray, in sphere, in rigidTransform, out result);
                case ColliderType.Capsule:
                    var capsule = collider.m_capsule;
                    Physics.ScaleStretchCollider(ref capsule, transform.scale, transform.stretch);
                    return PointRayCapsule.Raycast(in ray, in capsule, in rigidTransform, out result);
                case ColliderType.Box:
                    var box = collider.m_box;
                    Physics.ScaleStretchCollider(ref box, transform.scale, transform.stretch);
                    return PointRayBox.Raycast(in ray, in box, in rigidTransform, out result);
                case ColliderType.Triangle:
                    var triangle = collider.m_triangle;
                    Physics.ScaleStretchCollider(ref triangle, transform.scale, transform.stretch);
                    return PointRayTriangle.Raycast(in ray, in triangle, in rigidTransform, out result);
                case ColliderType.Convex:
                    var convex = collider.m_convex;
                    Physics.ScaleStretchCollider(ref convex, transform.scale, transform.stretch);
                    return PointRayConvex.Raycast(in ray, in convex, in rigidTransform, out result);
                case ColliderType.TriMesh:
                    var triMesh = collider.m_triMesh();
                    Physics.ScaleStretchCollider(ref triMesh, transform.scale, transform.stretch);
                    return PointRayTriMesh.Raycast(in ray, in triMesh, in rigidTransform, out result);
                case ColliderType.Compound:
                    var compound = collider.m_compound();
                    Physics.ScaleStretchCollider(ref compound, transform.scale, transform.stretch);
                    return PointRayCompound.Raycast(in ray, in compound, in rigidTransform, out result);
                case ColliderType.Terrain:
                    var terrain = collider.m_terrain();
                    Physics.ScaleStretchCollider(ref terrain, transform.scale, transform.stretch);
                    return PointRayTerrain.Raycast(in ray, in terrain, in rigidTransform, out result);
                default:
                    result = default;
                    return false;
            }
        }

        public static bool Raycast(in Ray ray, in Collider collider, in TransformQvvs transform, int subCollider, out RaycastResult result)
        {
            var rigidTransform = new RigidTransform(transform.rotation, transform.position);
            switch (collider.type)
            {
                case ColliderType.Sphere:
                    var sphere = collider.m_sphere;
                    Physics.ScaleStretchCollider(ref sphere, transform.scale, transform.stretch);
                    return PointRaySphere.Raycast(in ray, in sphere, in rigidTransform, out result);
                case ColliderType.Capsule:
                    var capsule = collider.m_capsule;
                    Physics.ScaleStretchCollider(ref capsule, transform.scale, transform.stretch);
                    return PointRayCapsule.Raycast(in ray, in capsule, in rigidTransform, out result);
                case ColliderType.Box:
                    var box = collider.m_box;
                    Physics.ScaleStretchCollider(ref box, transform.scale, transform.stretch);
                    return PointRayBox.Raycast(in ray, in box, in rigidTransform, out result);
                case ColliderType.Triangle:
                    var triangle = collider.m_triangle;
                    Physics.ScaleStretchCollider(ref triangle, transform.scale, transform.stretch);
                    return PointRayTriangle.Raycast(in ray, in triangle, in rigidTransform, out result);
                case ColliderType.Convex:
                    var convex = collider.m_convex;
                    Physics.ScaleStretchCollider(ref convex, transform.scale, transform.stretch);
                    return PointRayConvex.Raycast(in ray, in convex, in rigidTransform, out result);
                case ColliderType.TriMesh:
                    var triMesh         = collider.m_triMesh();
                    var triMeshTriangle = triMesh.triMeshColliderBlob.Value.triangles[subCollider];
                    Physics.ScaleStretchCollider(ref triMeshTriangle, transform.scale, transform.stretch * triMesh.scale);
                    var triMeshHit          = PointRayTriangle.Raycast(in ray, in triMeshTriangle, in rigidTransform, out result);
                    result.subColliderIndex = subCollider;
                    return triMeshHit;
                case ColliderType.Compound:
                    var compound = collider.m_compound();
                    Physics.ScaleStretchCollider(ref compound, transform.scale, transform.stretch);
                    compound.GetScaledStretchedSubCollider(subCollider, out var compoundCollider, out var subTransform);
                    var compoundTransform = math.mul(rigidTransform, subTransform);
                    switch (compoundCollider.type)
                    {
                        case ColliderType.Sphere:
                            return PointRaySphere.Raycast(in ray, in collider.m_sphere, compoundTransform, out result);
                        case ColliderType.Capsule:
                            return PointRayCapsule.Raycast(in ray, in collider.m_capsule, compoundTransform, out result);
                        case ColliderType.Box:
                            return PointRayBox.Raycast(in ray, in collider.m_box, compoundTransform, out result);
                        default:
                            // Should never happen
                            result = default;
                            return false;
                    }
                case ColliderType.Terrain:
                    var terrain                = collider.m_terrain();
                    var terrainTriangleIndices = terrain.terrainColliderBlob.Value.GetTriangle(subCollider);
                    var terrainTriangle        = PointRayTerrain.CreateLocalTriangle(ref terrain.terrainColliderBlob.Value,
                                                                                     terrainTriangleIndices,
                                                                                     terrain.baseHeightOffset,
                                                                                     terrain.scale * transform.scale * transform.stretch);
                    return PointRayTriangle.Raycast(in ray, in terrainTriangle, in rigidTransform, out result);
                default:
                    result = default;
                    return false;
            }
        }
    }
}

