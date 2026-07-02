using System.Diagnostics;
using AccessType = Latios.Transforms.TickedTransformAspect.AccessType;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Entities.Exposed;
using Unity.Mathematics;

namespace Latios.Transforms
{
    [NativeContainer]
    public unsafe struct TickedTransformReadAspect
    {
        internal RefRO<TickedWorldTransform> m_worldTransform;
        internal EntityInHierarchyHandle     m_handle;
        internal void*                       m_access;
        internal EntityStorageInfoLookup     m_esil;

        internal AccessType m_accessType;

        #region Read/Write Properties
        /// <summary>
        /// The world-space position of the entity that can be read.
        /// </summary>
        public float3 worldPosition => m_worldTransform.ValueRO.position;

        /// <summary>
        /// The world-space rotation of the entity that can be read.
        /// </summary>
        public quaternion worldRotation => m_worldTransform.ValueRO.rotation;

        /// <summary>
        /// The world-space scale of the entity that can be read.
        /// </summary>
        public float worldScale => m_worldTransform.ValueRO.scale;

        /// <summary>
        /// The local-space position of the entity that can be read. If there is no parent, this is
        /// the same as worldPosition.
        /// When reading, float3.zero is returned if the entity has an CopyParentWorldTransformTag.
        /// </summary>
        public float3 localPosition => localTransform.position;

        /// <summary>
        /// The local-space rotation of the entity that can be read. If there is no parent, this is
        /// the same as worldRotation.
        /// When reading, quaternion.identity is returned if the entity has an CopyParentWorldTransformTag.
        /// </summary>
        public quaternion localRotation => localTransform.rotation;

        /// <summary>
        /// The local-space scale of the entity that can be read. If there is no parent, this is
        /// the same as worldScale.
        /// When reading, 1f is returned if the entity has an CopyParentWorldTransformTag.
        /// </summary>
        public float localScale => localTransform.scale;

        /// <summary>
        /// The stretch of the entity that can be read.
        /// This value affects children's positions but nothing else.
        /// </summary>
        public float3 stretch => m_worldTransform.ValueRO.stretch;

        /// <summary>
        /// The context32 of the entity that can be read.
        /// It is a user value (do what you want with it) and not used directly by Latios Transforms (though other modules may support specific use cases).
        /// </summary>
        public int context32 => m_worldTransform.ValueRO.context32;

        /// <summary>
        /// The world-space QVVS transform of the entity that can be read.
        /// </summary>
        public TransformQvvs worldTransform => m_worldTransform.ValueRO.worldTransform;

        /// <summary>
        /// The local-space QVS transform of the entity that can be read. If there is no parent, this reads
        /// the position, rotation, and scale properties of the worldTransform.
        /// When reading, TransformQvs.identity is returned if the entity has an CopyParentWorldTransformTag.
        /// </summary>
        public TransformQvs localTransform
        {
            get
            {
                if (m_handle.isNull || m_handle.isRoot)
                {
                    var transform = m_worldTransform.ValueRO.worldTransform;
                    return new TransformQvs(transform.position, transform.rotation, transform.scale);
                }
                switch (m_accessType)
                {
                    case AccessType.EntityManager:
                        return TransformTools.Unsafe.TickedLocalTransformFrom(m_handle, in m_worldTransform.ValueRO, *(EntityManager*)m_access, out _);
                    case AccessType.ComponentBroker:
                        return TransformTools.Unsafe.TickedLocalTransformFrom(m_handle, in m_worldTransform.ValueRO, ref *(ComponentBroker*)m_access, out _);
                    case AccessType.ComponentBrokerKeyed:
                        var key = TransformsKey.CreateFromExclusivelyAccessedRoot(m_handle.root.entity, m_esil);
                        return TransformTools.Unsafe.TickedLocalTransformFrom(m_handle, in m_worldTransform.ValueRO, key, ref *(ComponentBroker*)m_access, out _);
                    case AccessType.ComponentLookup:
                        return TransformTools.Unsafe.TickedLocalTransformFrom(m_handle,
                                                                              in m_worldTransform.ValueRO,
                                                                              m_esil,
                                                                              ref *(ComponentLookup<TickedWorldTransform>*)m_access,
                                                                              out _);
                    default: return default;
                }
            }
        }

        /// <summary>
        /// The local-space transform in full QVVS representation that can be read. If there is no parent, this is
        /// the same as worldTransform.
        /// </summary>
        public TransformQvvs localTransformQvvs
        {
            get
            {
                if (m_handle.isNull || m_handle.isRoot)
                    return m_worldTransform.ValueRO.worldTransform;
                var local          = localTransform;
                var transform      = m_worldTransform.ValueRO.worldTransform;
                transform.position = local.position;
                transform.rotation = local.rotation;
                transform.scale    = local.scale;
                return transform;
            }
        }
        #endregion

        #region ReadOnly Properties
        /// <summary>
        /// Retrieves the EntityInHierarchyHandle of this TickedTransformReadAspect. Note that if the entity
        /// does not belong to a hierarchy, EntityInHierarchyHandle.isNull will be true.
        /// </summary>
        public EntityInHierarchyHandle entityInHierarchyHandle => m_handle;

        /// <summary>
        /// Retrieves the TickedTransformReadAspect for the specified handle belonging to the same hierarchy
        /// as this TickedTransformReadAspect. When safety checks exist, this method throws if the specifed
        /// handle comes from another hierarchy or if either its handle or this TickedTransformReadAspect's
        /// handle is null.
        /// </summary>
        public TickedTransformReadAspect this[EntityInHierarchyHandle otherHandle]
        {
            get
            {
                CheckBelongsToSameHierarchy(in otherHandle);
                var result      = this;
                result.m_handle = otherHandle;
                switch (m_accessType)
                {
                    case AccessType.EntityManager:
                        result.m_worldTransform = ((EntityManager*)m_access)->GetComponentDataRO<TickedWorldTransform>(otherHandle.entity);
                        break;
                    case AccessType.ComponentBroker:
                        result.m_worldTransform = ((ComponentBroker*)m_access)->GetRO<TickedWorldTransform>(otherHandle.entity);
                        break;
                    case AccessType.ComponentBrokerKeyed:
                        result.m_worldTransform = ((ComponentBroker*)m_access)->GetROIgnoreParallelSafety<TickedWorldTransform>(otherHandle.entity);
                        break;
                    case AccessType.ComponentLookup:
                        result.m_worldTransform = ((ComponentLookup<TickedWorldTransform>*)m_access)->GetRefRO(otherHandle.entity);
                        break;
                    default:
                        result.m_worldTransform = default;
                        break;
                }
                CheckWorldTransformIsValid(in result.m_worldTransform);
                return result;
            }
        }
        /// <summary>
        /// Attempts to retrieve the TickedTransformReadAspect for the specified handle belonging to the same
        /// hierarchy as this TickedTransformReadAspect. When safety checks exist, this method throws if the
        /// specifed handle comes from another hierarchy. This method returns false if the transform
        /// is not present.
        /// </summary>
        public bool TryGetAspect(in EntityInHierarchyHandle otherHandle, out TickedTransformReadAspect otherTickedTransformReadAspect)
        {
            CheckBelongsToSameHierarchy(in otherHandle);
            var result      = this;
            result.m_handle = otherHandle;
            switch (m_accessType)
            {
                case AccessType.EntityManager:
                    if (((EntityManager*)m_access)->HasComponent<TickedWorldTransform>(otherHandle.entity))
                        result.m_worldTransform = ((EntityManager*)m_access)->GetComponentDataRO<TickedWorldTransform>(otherHandle.entity);
                    else
                        result.m_worldTransform = default;
                    break;
                case AccessType.ComponentBroker:
                    result.m_worldTransform = ((ComponentBroker*)m_access)->GetRO<TickedWorldTransform>(otherHandle.entity);
                    break;
                case AccessType.ComponentBrokerKeyed:
                    result.m_worldTransform = ((ComponentBroker*)m_access)->GetROIgnoreParallelSafety<TickedWorldTransform>(otherHandle.entity);
                    break;
                case AccessType.ComponentLookup:
                    ((ComponentLookup<TickedWorldTransform>*)m_access)->TryGetRefRO(otherHandle.entity, out result.m_worldTransform);
                    break;
                default:
                    result.m_worldTransform = default;
                    break;
            }
            otherTickedTransformReadAspect = result;
            return result.m_worldTransform.IsValid;
        }
        /// <summary>
        /// True if the entity has a parent and not a CopyParent inheritance flag.
        /// </summary>
        public bool hasMutableLocalTransform => hasParent && !m_handle.isCopyParent;
        /// <summary>
        /// True if the entity has a parent
        /// </summary>
        public bool hasParent => !m_handle.isNull && !m_handle.isRoot;

        /// <summary>
        /// The unit forward vector (local Z+) of the entity in world-space
        /// </summary>
        public float3 forwardDirection => math.rotate(worldRotation, new float3(0f, 0f, 1f));
        /// <summary>
        /// The unit backward vector (local Z-) of the entity in world-space
        /// </summary>
        public float3 backwardDirection => math.rotate(worldRotation, new float3(0f, 0f, -1f));
        /// <summary>
        /// The unit left vector (local X-) of the entity in world-space
        /// </summary>
        public float3 leftDirection => math.rotate(worldRotation, new float3(-1f, 0f, 0f));
        /// <summary>
        /// The unit right vector (local X+) of the entity in world-space
        /// </summary>
        public float3 rightDirection => math.rotate(worldRotation, new float3(1f, 0f, 0f));
        /// <summary>
        /// The unit up vector (local Y+) of the entity in world-space
        /// </summary>
        public float3 upDirection => math.rotate(worldRotation, new float3(0f, 1f, 0f));
        /// <summary>
        /// The unit down vector (local Y-) of the entity in world-space
        /// </summary>
        public float3 downDirection => math.rotate(worldRotation, new float3(0f, -1f, 0f));

        /// <summary>
        /// The matrix that represents the transformation of the entity from local-space to world-space including stretch.
        /// This version discards the bottom row of a typical 4x4 matrix as that row is assumed to be (0, 0, 0, 1).
        /// </summary>
        public float3x4 worldMatrix3x4 => m_worldTransform.ValueRO.worldTransform.ToMatrix3x4();
        /// <summary>
        /// The matrix that represents the transformation of the entity from local-space to world-space including stretch.
        /// </summary>
        public float4x4 worldMatrix4x4 => m_worldTransform.ValueRO.worldTransform.ToMatrix4x4();
        /// <summary>
        /// The matrix that represents the transformation of the entity from world-space to local-space including stretch.
        /// This version discards the bottom row of a typical 4x4 matrix as that row is assumed to be (0, 0, 0, 1).
        /// </summary>
        public float3x4 inverseWorldMatrix3x4 => m_worldTransform.ValueRO.worldTransform.ToInverseMatrix3x4();
        /// <summary>
        /// The matrix that represents the transformation of the entity from world-space to local-space including stretch.
        /// </summary>
        public float4x4 inverseWorldMatrix4x4 => m_worldTransform.ValueRO.worldTransform.ToInverseMatrix4x4();
        /// <summary>
        /// The matrix that represents the transformation of the entity from world-space to local-space ignoring stretch.
        /// This version discards the bottom row of a typical 4x4 matrix as that row is assumed to be (0, 0, 0, 1).
        /// </summary>
        public float3x4 inverseWorldMatrix3x4IgnoreStretch => m_worldTransform.ValueRO.worldTransform.ToInverseMatrix3x4IgnoreStretch();
        /// <summary>
        /// The matrix that represents the transformation of the entity from world-space to local-space ignoring stretch.
        /// </summary>
        public float4x4 inverseWorldMatrix4x4IgnoreStretch => m_worldTransform.ValueRO.worldTransform.ToInverseMatrix4x4IgnoreStretch();

        /// <summary>
        /// The matrix that represent's the entity's local transform relative to its parent, or relative to the world if it does not have a parent.
        /// Stretch is included.
        /// This version discards the bottom row of a typical 4x4 matrix as that row is assumed to be (0, 0, 0, 1).
        /// </summary>
        public float3x4 localMatrix3x4 => hasMutableLocalTransform? localTransform.ToMatrix3x4(stretch) :
            hasParent? float3x4.Scale(stretch) : worldMatrix3x4;
        /// <summary>
        /// The matrix that represent's the entity's local transform relative to its parent, or relative to the world if it does not have a parent.
        /// Stretch is included.
        /// </summary>
        public float4x4 localMatrix4x4 => hasMutableLocalTransform? localTransform.ToMatrix4x4(stretch) :
            hasParent? float4x4.Scale(stretch) : worldMatrix4x4;
        /// <summary>
        /// The inverse of localMatrix3x4, computed directly from the QVS or QVVS data. Stretch is included.
        /// This version discards the bottom row of a typical 4x4 matrix as that row is assumed to be (0, 0, 0, 1).
        /// </summary>
        public float3x4 inverseLocalMatrix3x4 => hasMutableLocalTransform? localTransform.ToInverseMatrix3x4(stretch) :
            hasParent? float3x4.Scale(math.rcp(stretch)) : inverseWorldMatrix3x4;
        /// <summary>
        /// The inverse of localMatrix4x4, computed directly from the QVS or QVVS data. Stretch is included.
        /// </summary>
        public float4x4 inverseLocalMatrix4x4 => hasMutableLocalTransform? localTransform.ToInverseMatrix4x4(stretch) :
            hasParent? float4x4.Scale(math.rcp(stretch)) : inverseWorldMatrix4x4;
        /// <summary>
        /// The inverse of localMatrix3x4, computed directly from the QVS or QVVS data, except stretch is ignored.
        /// This version discards the bottom row of a typical 4x4 matrix as that row is assumed to be (0, 0, 0, 1).
        /// </summary>
        public float3x4 inverseLocalMatrix3x4IgnoreStretch => hasMutableLocalTransform? localTransform.ToInverseMatrix3x4() :
            hasParent ? float3x4.identity : inverseWorldMatrix3x4IgnoreStretch;
        /// <summary>
        /// The inverse of localMatrix4x4, computed directly from the QVS or QVVS data, except stretch is ignored.
        /// </summary>
        public float4x4 inverseLocalMatrix4x4IgnoreStretch => hasMutableLocalTransform? localTransform.ToInverseMatrix4x4() :
            hasParent ? float4x4.identity : inverseWorldMatrix4x4IgnoreStretch;
        #endregion

        #region ReadOnly Transformation Methods
        /// <summary>Transform a point from local space into world space.</summary>
        /// <param name="point">The point to transform</param>
        /// <returns>The transformed point</returns>>
        public float3 TransformPointLocalToWorld(float3 point)
        {
            return qvvs.TransformPoint(worldTransform, point);
        }

        /// <summary>Transform a point from world space into local space.</summary>
        /// <param name="point">The point to transform</param>
        /// <returns>The transformed point</returns>>
        public float3 TransformPointWorldToLocal(float3 point)
        {
            return qvvs.InverseTransformPoint(worldTransform, point);
        }

        /// <summary>Transforms a direction vector from local space into world space, ignoring the effects of stretch.</summary>
        /// <param name="direction">The direction to transform</param>
        /// <returns>The transformed direction</returns>>
        public float3 TransformDirectionLocalToWorld(float3 direction)
        {
            return qvvs.TransformDirection(worldTransform, direction);
        }

        /// <summary>Transforms a direction vector from local space into world space, including directional changes caused by stretch while preserving magnitude.</summary>
        /// <param name="direction">The direction to transform</param>
        /// <returns>The transformed direction</returns>>
        public float3 TransformDirectionLocalToWorldWithStretch(float3 direction)
        {
            return qvvs.TransformDirectionWithStretch(worldTransform, direction);
        }

        /// <summary>Transforms a direction vector from local space into world space, including directional and magnitude changes caused by scale and stretch.</summary>
        /// <param name="direction">The direction to transform</param>
        /// <returns>The transformed direction</returns>>
        public float3 TransformDirectionLocalToWorldScaledAndStretched(float3 direction)
        {
            return qvvs.TransformDirectionScaledAndStretched(worldTransform, direction);
        }

        /// <summary>Transforms a direction vector from world space into local space, ignoring the effects of stretch.</summary>
        /// <param name="direction">The direction to transform</param>
        /// <returns>The transformed direction</returns>>
        public float3 TransformDirectionWorldToLocal(float3 direction)
        {
            return qvvs.TransformDirection(worldTransform, direction);
        }

        /// <summary>Transforms a direction vector from world space into local space, including directional changes caused by stretch while preserving magnitude.</summary>
        /// <param name="direction">The direction to transform</param>
        /// <returns>The transformed direction</returns>>
        public float3 TransformDirectionWorldToLocalWithStretch(float3 direction)
        {
            return qvvs.TransformDirectionWithStretch(worldTransform, direction);
        }

        /// <summary>Transforms a direction vector from world space into local space, including directional and magnitude changes caused by scale and stretch.</summary>
        /// <param name="direction">The direction to transform</param>
        /// <returns>The transformed direction</returns>>
        public float3 TransformDirectionWorldToLocalScaledAndStretched(float3 direction)
        {
            return qvvs.TransformDirectionScaledAndStretched(worldTransform, direction);
        }
        #endregion

        #region Safety
        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        void CheckBelongsToSameHierarchy(in EntityInHierarchyHandle otherHandle)
        {
            if (m_handle.isNull || otherHandle.isNull || m_handle.m_hierarchy != otherHandle.m_hierarchy)
                throw new System.ArgumentException("The EntityInHierarchyHandle does not belong to the same hierarchy as this TickedTransformReadAspect.");
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        void CheckWorldTransformIsValid(in RefRO<TickedWorldTransform> transform)
        {
            if (!transform.IsValid)
                throw new System.ArgumentException("The Entity did not have a TickedWorldTransform, either because it is not a ticking entity or because it is no longer alive.");
        }
        #endregion
    }
}

