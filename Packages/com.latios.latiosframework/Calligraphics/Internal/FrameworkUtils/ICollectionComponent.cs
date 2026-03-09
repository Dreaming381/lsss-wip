using Unity.Entities;
using Unity.Jobs;

namespace TextMeshDOTS
{
    // This type is defined to help maintain consistency with Calligraphics.
    // TextMeshDOTS uses these as singletons.
    internal interface ICollectionComponent : IComponentData
    {
        /// <summary>
        /// Attempt to Dispose the collection component. Note that user code could add not-fully-allocated collection components
        /// or a collection component may be default-initialized.
        /// </summary>
        /// <param name="inputDeps"></param>
        /// <returns></returns>
        JobHandle TryDispose(JobHandle inputDeps);
    }
}
