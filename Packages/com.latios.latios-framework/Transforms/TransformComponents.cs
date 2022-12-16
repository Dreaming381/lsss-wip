using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Latios.Transforms
{
    // World space transform for all entities. Is also local space for root entities. It is always present, and is the only required transform component for rendering.
    public struct WorldTransform : IComponentData
    {
        public TransformQvvs worldTransform;

        public float3 position => worldTransform.position;
        public quaternion rotation => worldTransform.rotation;
        public float scale => worldTransform.scale;
        public float3 stretch => worldTransform.stretch;
        public float3 nonUniformScale => scale * stretch;
        public int worldIndex => worldTransform.worldIndex;
    }

    // Typically read-only by user code
    public struct ParentToWorldTransform : IComponentData
    {
        public TransformQvvs parentToWorldTransform;

        public float3 position => parentToWorldTransform.position;
        public quaternion rotation => parentToWorldTransform.rotation;
        public float scale => parentToWorldTransform.scale;
        public float3 stretch => parentToWorldTransform.stretch;
        public float3 nonUniformScale => scale * stretch;
        public int worldIndex => parentToWorldTransform.worldIndex;
    }

    // Local space transform relative to the parent, only valid if parent exists
    public struct LocalToParentTransform : IComponentData
    {
        // Stretch comes from the WorldTransform and is not duplicated here so-as to improve chunk occupancy
        public TransformQvs localToParentTransform;

        public float3 position => localToParentTransform.position;
        public quaternion rotation => localToParentTransform.rotation;
        public float scale => localToParentTransform.scale;
    }

    // Can replace LocalToParentTransform and ParentToWorldTransform to improve chunk occupancy if the entity copies the parent's transform exactly
    public struct IdentityLocalToParentTransformTag : IComponentData { }

    public struct Parent : IComponentData
    {
        public Entity parent;
    }
    // Usually doesn't need to be touched by user code
    public struct PreviousParent : ICleanupComponentData
    {
        public Entity previousParent;
    }

    [InternalBufferCapacity(0)]
    public struct Child : ICleanupBufferElementData
    {
        public EntityWith<Parent> child;
    }

    // Optional matrix that is applied after computing the final WorldTransform
    public struct PostProcessMatrix : IComponentData
    {
        public float3x4 postProcessMatrix;
    }
}

