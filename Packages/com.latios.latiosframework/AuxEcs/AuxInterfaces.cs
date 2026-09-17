using Latios.Unsafe;

namespace Latios.AuxEcs
{
    /// <summary>
    /// An interface that a component stored in an AuxWorld can implement to receive automatic callbacks when the component is removed or replaced.
    /// This allows components to store dynamic data structures safely. Such structures should be allocated with the AuxWorld's allocator.
    /// </summary>
    public partial interface IAuxDisposable : System.IDisposable, IVInterface
    {
    }
}

