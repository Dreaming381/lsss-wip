using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Latios.Psyshock
{
    internal static class ConvexTerrain
    {
        public static bool DistanceBetween(in TerrainCollider terrain,
                                           in RigidTransform terrainTransform,
                                           in ConvexCollider convex,
                                           in RigidTransform convexTransform,
                                           float maxDistance,
                                           out ColliderDistanceResult result)
        {
            var processor = new DistanceBetweenClosestProcessor { bestIndex = -1, bestDistance = float.MaxValue };
            DistanceBetweenAll(in terrain, in terrainTransform, in convex, in convexTransform, maxDistance, ref processor);
            var hit = processor.bestIndex >= 0;

            if (!hit)
            {
                result = default;
                return false;
            }

            var triangleIndices = terrain.terrainColliderBlob.Value.GetTriangle(processor.bestIndex);
            var triangle        = PointRayTerrain.CreateLocalTriangle(ref terrain.terrainColliderBlob.Value,
                                                                      triangleIndices,
                                                                      terrain.baseHeightOffset,
                                                                      terrain.scale);
            hit                                  = TriangleConvex.DistanceBetween(in convex, in convexTransform, in triangle, in terrainTransform, maxDistance, out result);
            (result.hitpointA, result.hitpointB) = (result.hitpointB, result.hitpointA);
            (result.normalA, result.normalB)     = (result.normalB, result.normalA);
            result.subColliderIndexB             = result.subColliderIndexA;
            result.subColliderIndexA             = processor.bestIndex;
            return hit;
        }

        public static unsafe void DistanceBetweenAll<T>(in TerrainCollider terrain,
                                                        in RigidTransform terrainTransform,
                                                        in ConvexCollider convex,
                                                        in RigidTransform convexTransform,
                                                        float maxDistance,
                                                        ref T processor) where T : unmanaged, IDistanceBetweenAllProcessor
        {
            var aabb         = Physics.AabbFrom(convex, math.mul(math.inverse(terrainTransform), convexTransform));
            var inverseScale = math.rcp(terrain.scale);
            var validAxes    = math.isfinite(inverseScale);

            var crosses = (aabb.min < 0f) & (aabb.max > 0f);
            if (math.any(!crosses & !validAxes))
                return;

            Physics.GetCenterExtents(aabb, out var center, out var extents);
            center     *= inverseScale;
            extents    *= inverseScale;
            var min     = (int3)math.floor(center - extents);
            var max     = (int3)math.ceil(center + extents);
            min.y      -= terrain.baseHeightOffset;
            max.y      -= terrain.baseHeightOffset;
            var minInt  = math.select(short.MinValue, min, validAxes);
            var maxInt  = math.select(short.MaxValue, max, validAxes);

            if (minInt.y > terrain.terrainColliderBlob.Value.maxHeight || maxInt.y < terrain.terrainColliderBlob.Value.minHeight)
                return;

            var terrainProcessor = new DistanceBetweenAllProcessor<T>
            {
                convex           = convex,
                convexTransform  = convexTransform,
                maxDistance      = maxDistance,
                minHeight        = (short)minInt.y,
                maxHeight        = (short)minInt.y,
                heightOffset     = terrain.baseHeightOffset,
                scale            = terrain.scale,
                terrainTransform = terrainTransform,
                processor        = (T*)UnsafeUtility.AddressOf(ref processor)
            };
            terrain.terrainColliderBlob.Value.FindTriangles(minInt.x, minInt.z, maxInt.x, maxInt.z, ref terrainProcessor);
        }

        public static bool ColliderCast(in ConvexCollider convexToCast,
                                        in RigidTransform castStart,
                                        float3 castEnd,
                                        in TerrainCollider targetTerrain,
                                        in RigidTransform targetTerrainTransform,
                                        out ColliderCastResult result)
        {
            var targetTerrainTransformInverse = math.inverse(targetTerrainTransform);
            var casterInTargetSpace           = math.mul(targetTerrainTransformInverse, castStart);
            var aabb                          =
                Physics.AabbFrom(convexToCast, in casterInTargetSpace, casterInTargetSpace.pos + math.rotate(targetTerrainTransformInverse, castEnd - castStart.pos));

            var inverseScale = math.rcp(targetTerrain.scale);
            var validAxes    = math.isfinite(inverseScale);
            result           = default;
            var crosses      = (aabb.min < 0f) & (aabb.max > 0f);
            if (math.any(!crosses & !validAxes))
                return false;

            Physics.GetCenterExtents(aabb, out var center, out var extents);
            center  *= inverseScale;
            extents *= inverseScale;

            var min     = (int3)math.floor(center - extents);
            var max     = (int3)math.ceil(center + extents);
            min.y      -= targetTerrain.baseHeightOffset;
            max.y      -= targetTerrain.baseHeightOffset;
            var minInt  = math.select(short.MinValue, min, validAxes);
            var maxInt  = math.select(short.MaxValue, max, validAxes);

            if (minInt.y > targetTerrain.terrainColliderBlob.Value.maxHeight || maxInt.y < targetTerrain.terrainColliderBlob.Value.minHeight)
                return false;

            var processor = new CastProcessor
            {
                bestDistance     = float.MaxValue,
                bestIndex        = -1,
                found            = false,
                invalid          = false,
                convex           = convexToCast,
                castStart        = castStart,
                castEnd          = castEnd,
                terrainTransform = targetTerrainTransform,
                minHeight        = (short)minInt.y,
                maxHeight        = (short)minInt.y,
                heightOffset     = targetTerrain.baseHeightOffset,
                scale            = targetTerrain.scale,
            };
            targetTerrain.terrainColliderBlob.Value.FindTriangles(minInt.x, minInt.z, maxInt.x, maxInt.z, ref processor);

            if (processor.invalid || !processor.found)
                return false;

            var hitTransform     = castStart;
            hitTransform.pos    += math.normalize(castEnd - castStart.pos) * processor.bestDistance;
            var triangleIndices  = targetTerrain.terrainColliderBlob.Value.GetTriangle(processor.bestIndex);
            var triangle         = PointRayTerrain.CreateLocalTriangle(ref targetTerrain.terrainColliderBlob.Value,
                                                                       triangleIndices,
                                                                       targetTerrain.baseHeightOffset,
                                                                       targetTerrain.scale);
            TriangleConvex.DistanceBetween(in convexToCast,
                                           in hitTransform,
                                           in triangle,
                                           in targetTerrainTransform,
                                           1f,
                                           out var distanceResult);
            result = new ColliderCastResult
            {
                hitpoint                 = distanceResult.hitpointA,
                normalOnCaster           = distanceResult.normalA,
                normalOnTarget           = distanceResult.normalB,
                subColliderIndexOnCaster = processor.bestIndex,
                subColliderIndexOnTarget = distanceResult.subColliderIndexA,
                distance                 = math.distance(hitTransform.pos, castStart.pos)
            };
            return true;
        }

        public static bool ColliderCast(in TerrainCollider terrainToCast,
                                        in RigidTransform castStart,
                                        float3 castEnd,
                                        in ConvexCollider targetConvex,
                                        in RigidTransform targetConvexTransform,
                                        out ColliderCastResult result)
        {
            var castReverse         = castStart.pos - castEnd;
            var worldToCasterSpace  = math.inverse(castStart);
            var targetInCasterSpace = math.mul(worldToCasterSpace, targetConvexTransform);
            var reverseCastEnd      = targetInCasterSpace.pos + math.rotate(worldToCasterSpace, castReverse);
            var aabb                = Physics.AabbFrom(targetConvex, in targetInCasterSpace, reverseCastEnd);

            var inverseScale = math.rcp(terrainToCast.scale);
            var validAxes    = math.isfinite(inverseScale);
            result           = default;
            var crosses      = (aabb.min < 0f) & (aabb.max > 0f);
            if (math.any(!crosses & !validAxes))
                return false;

            Physics.GetCenterExtents(aabb, out var center, out var extents);
            center  *= inverseScale;
            extents *= inverseScale;

            var min     = (int3)math.floor(center - extents);
            var max     = (int3)math.ceil(center + extents);
            min.y      -= terrainToCast.baseHeightOffset;
            max.y      -= terrainToCast.baseHeightOffset;
            var minInt  = math.select(short.MinValue, min, validAxes);
            var maxInt  = math.select(short.MaxValue, max, validAxes);

            if (minInt.y > terrainToCast.terrainColliderBlob.Value.maxHeight || maxInt.y < terrainToCast.terrainColliderBlob.Value.minHeight)
                return false;

            var processor = new CastProcessor
            {
                bestDistance     = float.MaxValue,
                bestIndex        = -1,
                found            = false,
                invalid          = false,
                convex           = targetConvex,
                castStart        = castStart,
                castEnd          = castEnd,
                terrainTransform = RigidTransform.identity,
                minHeight        = (short)minInt.y,
                maxHeight        = (short)minInt.y,
                heightOffset     = terrainToCast.baseHeightOffset,
                scale            = terrainToCast.scale,
            };
            terrainToCast.terrainColliderBlob.Value.FindTriangles(minInt.x, minInt.z, maxInt.x, maxInt.z, ref processor);

            if (processor.invalid || !processor.found)
            {
                result = default;
                return false;
            }

            var hitTransform     = castStart;
            hitTransform.pos    += math.normalize(castEnd - castStart.pos) * processor.bestDistance;
            var triangleIndices  = terrainToCast.terrainColliderBlob.Value.GetTriangle(processor.bestIndex);
            var triangle         = PointRayTerrain.CreateLocalTriangle(ref terrainToCast.terrainColliderBlob.Value,
                                                                       triangleIndices,
                                                                       terrainToCast.baseHeightOffset,
                                                                       terrainToCast.scale);
            TriangleConvex.DistanceBetween(in targetConvex,
                                           in targetConvexTransform,
                                           in triangle,
                                           in hitTransform,
                                           1f,
                                           out var distanceResult);
            result = new ColliderCastResult
            {
                hitpoint                 = distanceResult.hitpointB,
                normalOnCaster           = distanceResult.normalB,
                normalOnTarget           = distanceResult.normalA,
                subColliderIndexOnCaster = distanceResult.subColliderIndexA,
                subColliderIndexOnTarget = processor.bestIndex,
                distance                 = math.distance(hitTransform.pos, castStart.pos)
            };
            return true;
        }

        public static UnitySim.ContactsBetweenResult UnityContactsBetween(in TerrainCollider terrain,
                                                                          in RigidTransform terrainTransform,
                                                                          in ConvexCollider convex,
                                                                          in RigidTransform convexTransform,
                                                                          in ColliderDistanceResult distanceResult)
        {
            var triangleIndices = terrain.terrainColliderBlob.Value.GetTriangle(distanceResult.subColliderIndexA);
            var triangle        = PointRayTerrain.CreateLocalTriangle(ref terrain.terrainColliderBlob.Value, triangleIndices, terrain.baseHeightOffset, terrain.scale);
            return TriangleConvex.UnityContactsBetween(in convex, in convexTransform, in triangle, in terrainTransform, distanceResult.ToFlipped()).ToFlipped();
        }

        struct DistanceBetweenClosestProcessor : IDistanceBetweenAllProcessor
        {
            public int   bestIndex;
            public float bestDistance;

            public void Execute(in ColliderDistanceResult result)
            {
                if (bestIndex < 0 || result.distance < bestDistance)
                {
                    bestIndex    = result.subColliderIndexA;
                    bestDistance = result.distance;
                }
            }
        }

        unsafe struct DistanceBetweenAllProcessor<T> : TerrainColliderBlob.IFindTrianglesProcessor where T : unmanaged, IDistanceBetweenAllProcessor
        {
            public ConvexCollider convex;
            public RigidTransform convexTransform;
            public float          maxDistance;
            public short          minHeight;
            public short          maxHeight;

            public int            heightOffset;
            public float3         scale;
            public RigidTransform terrainTransform;

            public T* processor;

            public ulong FilterPatch(ref TerrainColliderBlob.Patch patch, ulong borderMask, short quadsPerBit)
            {
                var mask  = patch.GetFilteredQuadMaskFromHeights(minHeight, maxHeight);
                mask     &= borderMask;
                return mask;
            }

            public void Execute(ref TerrainColliderBlob blob, int3 triangleHeightIndices, int triangleIndex)
            {
                var triangle                 = PointRayTerrain.CreateLocalTriangle(ref blob, triangleHeightIndices, heightOffset, scale);
                var hit                      = TriangleConvex.DistanceBetween(in convex, in convexTransform, in triangle, in terrainTransform, maxDistance, out var tempResult);
                tempResult.subColliderIndexB = triangleIndex;
                tempResult                   = tempResult.ToFlipped();
                if (hit)
                {
                    processor->Execute(in tempResult);
                }
            }
        }

        struct CastProcessor : TerrainColliderBlob.IFindTrianglesProcessor
        {
            public ConvexCollider convex;
            public RigidTransform castStart;
            public float3         castEnd;
            public RigidTransform terrainTransform;
            public short          minHeight;
            public short          maxHeight;

            public int    heightOffset;
            public float3 scale;

            public float bestDistance;
            public int   bestIndex;
            public bool  found;
            public bool  invalid;

            public ulong FilterPatch(ref TerrainColliderBlob.Patch patch, ulong borderMask, short quadsPerBit)
            {
                if (invalid)
                    return 0;
                var mask  = patch.GetFilteredQuadMaskFromHeights(minHeight, maxHeight);
                mask     &= borderMask;
                return mask;
            }

            public void Execute(ref TerrainColliderBlob blob, int3 triangleHeightIndices, int triangleIndex)
            {
                if (invalid)
                    return;

                var triangle = PointRayTerrain.CreateLocalTriangle(ref blob, triangleHeightIndices, heightOffset, scale);
                Physics.ScaleStretchCollider(ref triangle, 1f, scale);
                // Check that we don't start already intersecting.
                if (TriangleConvex.DistanceBetween(in convex, in castStart, in triangle, in terrainTransform, 0f, out _))
                {
                    invalid = true;
                    return;
                }
                if (TriangleConvex.ColliderCast(in convex, in castStart, castEnd, in triangle, in terrainTransform, out var hit))
                {
                    if (!found || hit.distance < bestDistance)
                    {
                        found        = true;
                        bestDistance = hit.distance;
                        bestIndex    = triangleIndex;
                    }
                }
            }
        }
    }
}

