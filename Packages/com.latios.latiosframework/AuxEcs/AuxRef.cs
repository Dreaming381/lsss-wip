using System;
using System.Diagnostics;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Latios.AuxEcs
{
    public unsafe struct AuxRef<T> where T : unmanaged
    {
        internal T*   componentPtr;
        internal int* versionPtr;
        internal int  version;

        public ref T aux
        {
            get
            {
                CheckValid();
                return ref *componentPtr;
            }
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        void CheckValid()
        {
            if (componentPtr == null)
                throw new NullReferenceException("The AuxRef is not initialized.");
            if (version != *versionPtr)
                throw new InvalidOperationException("The AuxRef has been invalidated from when the component was removed from the entity");
        }
    }
}

