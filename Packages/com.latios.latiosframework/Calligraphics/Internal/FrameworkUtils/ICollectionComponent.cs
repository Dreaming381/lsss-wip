using Unity.Entities;
using Unity.Jobs;

namespace Latios.Calligraphics
{
    // This type is defined to help maintain consistency with Calligraphics.
    // Latios.Calligraphics uses these as singletons.
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
