
using System.Diagnostics;
using Latios.Unsafe.InternalSourceGen;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

namespace Latios.Unsafe
{
    /// <summary>
    /// An interface which another interface can derive from to allow Burst-compatible virtual calls.
    /// Any interface deriving from this must be declared as partial, and any struct implementing such
    /// interface must also be marked partial and implement this interface directly. This allows source
    /// generators to generate the necessary virtualization code.
    ///
    /// The interface will have a generated VPtr nested type which allows you to wrap a pointer behind
    /// the interface, allowing you to store various implementors of the interface within a single collection.
    /// </summary>
    public interface IVInterface
    {
        void __ThisMethodIsSupposedToBeGeneratedByASourceGenerator();
    }

    /// <summary>
    /// An interface generated on an IVInterface.VPtr, which can be used as a generic constraint to ensure
    /// a VPtr is a VPtr and for the correct generic interface.
    /// </summary>
    public interface IVPtrFor<T> where T : IVInterface
    {
    }

    /// <summary>
    /// A struct which contains a void*, and is implicitly castable from one. This allows for source generators
    /// to create an API method that accepts a void*, even if the assembly does not allow unsafe code.
    /// </summary>
    public unsafe struct UnsafeApiPointer
    {
        void* m_ptr;
        public void* ptr
        {
            get => m_ptr;
            set => m_ptr = value;
        }

        public static implicit operator UnsafeApiPointer(void* ptr) => new UnsafeApiPointer
        {
            m_ptr = ptr
        };
    }

    /// <summary>
    /// A struct which contains a void*, and is implicitly castable from T*. This allows for source generators
    /// to create an API method that accepts a T*, even if the assembly does not allow unsafe code.
    /// </summary>
    public unsafe struct UnsafeApiPointer<T> where T : unmanaged
    {
        void* m_ptr;
        public void* ptr
        {
            get => m_ptr;
            set => m_ptr = value;
        }

        public static implicit operator UnsafeApiPointer<T>(T* ptr) => new UnsafeApiPointer<T> {
            m_ptr = ptr
        };
        public static implicit operator UnsafeApiPointer(UnsafeApiPointer<T> ptr) => new UnsafeApiPointer {
            ptr = ptr.ptr
        };
    }

    /// <summary>
    /// A virtual pointer referencing a struct, array, or field inside a blob asset.
    /// It is very important that all interface method implementations do not mutate the referenced struct.
    /// </summary>
    /// <typeparam name="TVptr">The type of VPtr this BlobVPtr exposes.</typeparam>
    /// <typeparam name="TInterface">The type of interface this BlobVPtr virtualizes.</typeparam>
    [MayOnlyLiveInBlobStorage]
    public unsafe struct BlobVPtr<TVptr, TInterface> where TVptr : unmanaged, IVPtrFor<TInterface> where TInterface : IVInterface
    {
        internal BlobPtr<byte> allocation;
        internal long          stableHash;

        public TVptr vptr
        {
            get
            {
                if (!allocation.IsValid)
                    return default;
                bool found = VTable.TryGetStable<TInterface>(stableHash, out var functionPtr);
                CheckFunctionFound(found);
                var result = new VPtrImplAlias
                {
                    functionPtr = functionPtr,
                    vptr        = StaticAPI.VPtr.Create(allocation.GetUnsafePtr())
                };
                return UnsafeUtility.As<VPtrImplAlias, TVptr>(ref result);
            }
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        internal static void CheckFunctionFound(bool found)
        {
            if (!found)
            {
                throw new System.InvalidOperationException(
                    "Failed to find the VPtrFunction for the BlobVPtr. Please make sure you declared your struct as partial, loaded the assembly, and did not make your IDE generate empty IVInterface implementations.");
            }
        }
    }

    public static class BlobBuilderVPtrExtensions
    {
        /// <summary>
        /// Allocates enough memory to store a struct of type <typeparamref name="T"/> into a virtual interface.
        /// </summary>
        /// <param name="blobVPtr">A reference to a blob pointer field in a blob asset.</param>
        /// <typeparam name="TStruct">The struct data type.</typeparam>
        /// <typeparam name="TVptr">The type of VPtr this BlobVPtr exposes.</typeparam>
        /// <typeparam name="TInterface">The type of interface this BlobVPtr virtualizes.</typeparam>
        /// <returns>A reference to the newly allocated struct.</returns>
        public static ref TStruct AllocateVPtr<TStruct, TVptr, TInterface>(ref this BlobBuilder blobBuilder, ref BlobVPtr<TVptr, TInterface> blobVPtr) where TStruct : unmanaged,
        TInterface
            where TVptr : unmanaged, IVPtrFor<TInterface> where TInterface : IVInterface
        {
            bool found = VTable.TryGetStableHashFor<TStruct>(out var stableHash);
            BlobVPtr<TVptr, TInterface>.CheckFunctionFound(found);
            blobVPtr.stableHash = stableHash;
            ref var blobPtr     = ref UnsafeUtility.As<BlobPtr<byte>, BlobPtr<TStruct> >(ref blobVPtr.allocation);
            return ref blobBuilder.Allocate(ref blobPtr);
        }

        /// <summary>
        /// Allocates an instance of <typeparamref name="TStruct"/> into a virtual interface and initializes it with a copy of <paramref name="structData"/>
        /// </summary>
        /// <param name="blobVPtr">A reference to a blob pointer field in a blob asset.</param>
        /// <param name="structData">The data to copy into the blob</param>
        /// <typeparam name="TStruct">The struct data type.</typeparam>
        /// <typeparam name="TVptr">The type of VPtr this BlobVPtr exposes.</typeparam>
        /// <typeparam name="TInterface">The type of interface this BlobVPtr virtualizes.</typeparam>
        public static void ConstructVPtr<TStruct, TVptr, TInterface>(ref this BlobBuilder blobBuilder, ref BlobVPtr<TVptr, TInterface> blobVPtr,
                                                                     in TStruct structData) where TStruct : unmanaged, TInterface
            where TVptr : unmanaged, IVPtrFor<TInterface> where TInterface : IVInterface
        {
            ref var dst = ref blobBuilder.AllocateVPtr<TStruct, TVptr, TInterface>(ref blobVPtr);
            dst         = structData;
        }

        /// <summary>
        /// Sets a BlobVPtr to point to the given object inside the blob, encoding it into a virtual interface.
        /// </summary>
        /// <param name="blobVPtr">A reference to a blob pointer field in a blob asset.</param>
        /// <param name="obj">The struct that exists in the blob that you want to point to.</param>
        /// <typeparam name="TStruct">The struct data type.</typeparam>
        /// <typeparam name="TVptr">The type of VPtr this BlobVPtr exposes.</typeparam>
        /// <typeparam name="TInterface">The type of interface this BlobVPtr virtualizes.</typeparam>
        public static void SetVPtr<TStruct, TVptr, TInterface>(ref this BlobBuilder blobBuilder, ref BlobVPtr<TVptr, TInterface> blobVPtr,
                                                               ref TStruct obj) where TStruct : unmanaged, TInterface
            where TVptr : unmanaged, IVPtrFor<TInterface> where TInterface : IVInterface
        {
            bool found = VTable.TryGetStableHashFor<TStruct>(out var stableHash);
            BlobVPtr<TVptr, TInterface>.CheckFunctionFound(found);
            blobVPtr.stableHash = stableHash;
            ref var blobPtr     = ref UnsafeUtility.As<BlobPtr<byte>, BlobPtr<TStruct> >(ref blobVPtr.allocation);
            blobBuilder.SetPointer(ref blobPtr, ref obj);
        }
    }
}

