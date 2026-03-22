using System;
using System.Diagnostics;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Latios.AuxEcs
{
    public unsafe struct AuxWorld : IDisposable
    {
        #region Construct/Destruct
        public AuxWorld(AllocatorManager.AllocatorHandle allocator)
        {
            impl = AllocatorManager.Allocate<AuxWorldImpl>(allocator);
        }

        public void Dispose()
        {
            CheckIsValid();
            var allocator = impl->allocator;
            impl->Dispose();
            AllocatorManager.Free(allocator, impl);
            impl = null;
        }
        #endregion

        #region Component API
        public void AddComponent<T>(Entity entity, in T component) where T : unmanaged
        {
            CheckIsValid();
            impl->AddComponent(entity, in component);
        }

        public void RemoveComponent<T>(Entity entity) where T : unmanaged
        {
            CheckIsValid();
            impl->RemoveComponent<T>(entity);
        }

        public void RemoveAllComponents(Entity entity)
        {
            CheckIsValid();
        }

        public bool TryGetComponent<T>(Entity entity, out AuxRef<T> componentRef) where T : unmanaged
        {
            CheckIsValid();
            return impl->TryGetComponent<T>(entity, out componentRef);
        }
        #endregion

        #region Query API

        #endregion

        #region State
        internal AuxWorldImpl* impl;

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        void CheckIsValid()
        {
            if (impl == null)
                throw new NullReferenceException("The AuxWorld has not been initialized.");
        }
        #endregion
    }
}

