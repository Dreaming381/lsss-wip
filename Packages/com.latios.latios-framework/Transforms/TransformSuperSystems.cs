using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace Latios.Transforms
{
    /// <summary>
    /// The Transform System Group of Latios Transforms which performs hierarchy synchronization.
    /// Do not add systems to this group directly, but instead add systems to [Pre/Post]TransformSuperSystem.
    /// </summary>
    [DisableAutoCreation]
    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
    public partial class TransformSuperSystem : SuperSystem
    {
        protected override void CreateSystems()
        {
            EnableSystemSorting = false;

            GetOrCreateAndAddManagedSystem<PreTransformSuperSystem>();
            GetOrCreateAndAddUnmanagedSystem<ParentChangeSystem>();
            GetOrCreateAndAddUnmanagedSystem<TransformHierarchyUpdateSystem>();
            GetOrCreateAndAddManagedSystem<PostTransformSuperSystem>();
        }
    }

    /// <summary>
    /// You can use [UpdateInGroup(typeof(PreTransformSuperSystem))] to add systems that should be updated
    /// right before the main transform hierarchy synchronization step. This is useful for any entities which
    /// need to copy a local or world transform from a GameObject or another entity.
    /// </summary>
    [DisableAutoCreation]
    public partial class PreTransformSuperSystem : SuperSystem
    {
        protected override void CreateSystems()
        {
            EnableSystemSorting = true;
        }
    }

    /// <summary>
    /// You can use [UpdateInGroup(typeof(PostTransformSuperSystem))] to add systems that should be updated
    /// right after the main transform hierarchy synchronization step. This is useful for any entities which
    /// need to copy a local or world transform to a GameObject or another entity.
    /// </summary>
    [DisableAutoCreation]
    public partial class PostTransformSuperSystem : SuperSystem
    {
        protected override void CreateSystems()
        {
            EnableSystemSorting = true;
        }
    }
}

