using System;
using Unity.Entities;
using Unity.Jobs;

namespace Latios
{
    /// <summary>
    /// A pseudo-component that can be attached to entities.
    /// It does not allocate GC but can store managed references.
    /// </summary>
    public interface IManagedStructComponent
    {
        ComponentType AssociatedComponentType { get; }
    }

    /// <summary>
    /// A Pseduo-component that can be attached to entities.
    /// It can store NativeContainers and automatically tracks their dependencies.
    /// </summary>
    public interface ICollectionComponent
    {
        JobHandle TryDispose(JobHandle inputDeps);
        ComponentType AssociatedComponentType { get; }
    }

    //public struct ManagedComponentTag<T> : IComponentData where T : struct, IManagedComponent { }

    internal struct ManagedComponentCleanupTag<T> : ICleanupComponentData where T : struct, IManagedStructComponent { }

    //public struct CollectionComponentTag<T> : IComponentData where T : struct, ICollectionComponent { }

    internal struct CollectionComponentCleanupTag<T> : ICleanupComponentData where T : struct, ICollectionComponent { }
}

