using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Core;
using Unity.Entities;
using Unity.Entities.Exposed;
using Unity.Mathematics;

namespace Latios
{
    /// <summary>
    /// Implement this inteface on an ISystem, but don't implement any of the methods yourself.
    /// </summary>
    public interface ILatiosApi
    {
        #region Impl
        public void __OnCreateForLatios(ref SystemState state)
        {
        }

        public T __Get<T>() where T : unmanaged
        {
            throw new System.NotImplementedException("This method should have been replaced by codegen.");
        }

        public T __Get<T>(bool b) where T : unmanaged
        {
            throw new System.NotImplementedException("This method should have been replaced by codegen.");
        }

        public LatiosWorldUnmanaged __GetLatiosWorldUnmanaged()
        {
            throw new System.NotImplementedException("This method should have been replaced by codegen.");
        }

        public EntityQuery __GetJobDefaultQuery<T>() where T : struct, IJobEach
        {
            throw new System.NotImplementedException("This method should have been replaced by codegen.");
        }
        #endregion
    }

    /// <summary>
    /// An interface that allows various handle types with the [Inject] attribute to be injected via calling the Inject()
    /// or InjectByRef() extension method
    /// </summary>
    public interface IInjectable
    {
        #region Impl
        public void __CreateForApi(ref SystemState state)
        {
            throw new System.NotImplementedException("This method should have been replaced by codegen.");
        }

        public void __Inject<T>(ref SystemState state, ref T target) where T : unmanaged, IInjectable
        {
            throw new System.NotImplementedException("This method should have been replaced by codegen.");
        }
        #endregion
    }

    /// <summary>
    /// Marks that the field should be injected with system handles.
    /// Applicable to Entity/Component/BufferTypeHandle, EntityStorageInfo/Component/BufferLookup, ILatiosApiGettable, and ILatiosApiGettableBool
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class InjectAttribute : Attribute
    {
    }

    /// <summary>
    /// An interface that allows an unmanaged type to be cached and accessed by an ISystem implementing ILatiosApi using source generators.
    /// This version takes no arguments other than SystemState to construct.
    /// </summary>
    public interface ILatiosApiGettable
    {
        /// <summary>
        /// The method used to construct this instance after default initialization
        /// </summary>
        /// <param name="state">The SystemState to use for construction</param>
        public void CreateForApi(ref SystemState state);
        /// <summary>
        /// The method used to update this instance prior to use
        /// </summary>
        /// <param name="state">The SystemState used for updating</param>
        public void UpdateForApi(ref SystemState state);
    }

    /// <summary>
    /// An interface that allows an unmanaged type to be cached and accessed by an ISystem implementing ILatiosApi using source generators.
    /// This version takes a bool argument in addition to SystemState to construct. The bool argument can be used for anything compile-time dependent,
    /// but is typically used to specify a read-only mode.
    /// </summary>
    public interface ILatiosApiGettableBool
    {
        /// <summary>
        /// The method used to construct this instance after default initialization
        /// </summary>
        /// <param name="state">The SystemState to use for construction</param>
        /// <param name="b">A bool parameter to use for construction</param>
        public void CreateForApi(ref SystemState state, bool b);
        /// <summary>
        /// The method used to update this instance prior to use
        /// </summary>
        /// <param name="state">The SystemState used for updating</param>
        public void UpdateForApi(ref SystemState state);
    }

    /// <summary>
    /// A struct that serves a similar purpose to SystemAPI, but used for Latios Framework source generators.
    /// Construct an instance using the following line at the start of each method you want to use this API:
    /// <code>
    /// var api = this.GetApi(ref state);
    /// </code>
    /// </summary>
    /// <typeparam name="TSystem"></typeparam>
    public unsafe ref struct LatiosApiInvoker<TSystem> where TSystem : unmanaged, ISystem, ILatiosApi
    {
        internal SystemState*         m_state;
        internal TSystem*             m_thisPtr;
        internal LatiosWorldUnmanaged m_latiosWorld;

        /// <summary>
        /// The LatiosWorldUnmanaged this system belongs to
        /// </summary>
        public LatiosWorldUnmanaged latiosWorld => m_latiosWorld;
        /// <summary>
        /// The worldBlackboardEntity associated with the world of this system
        /// </summary>
        public BlackboardEntity worldBlackboardEntity => latiosWorld.worldBlackboardEntity;
        /// <summary>
        /// The sceneBlackboardEntity associated with the world of this system
        /// </summary>
        public BlackboardEntity sceneBlackboardEntity => latiosWorld.sceneBlackboardEntity;
        /// <summary>
        /// The main syncPoint system from which to get command buffers.
        /// Command buffers retrieved from this property from within a system will have dependencies managed automatically.
        /// </summary>
        public ref Systems.SyncPointPlaybackSystem syncPoint => ref latiosWorld.syncPoint;
        /// <summary>
        /// The Ticked syncPoint system from which to get command buffers. Only valid when ticking is installed.
        /// Command buffers retrieved from this property from within a system will have dependencies managed automatically.
        /// Playback will not occur if the tick this buffer was recorded on is discarded.
        /// </summary>
        public ref Systems.TickedSyncPointPlaybackSystem tickedSyncPoint => ref latiosWorld.tickedSyncPoint;

        /// <summary>
        /// The current time of the current system
        /// </summary>
        public TimeData time => m_state->WorldUnmanaged.Time;
        /// <summary>
        /// The current timestep of the current system
        /// </summary>
        public float deltaTime => time.DeltaTime;
        /// <summary>
        /// The elapsed time of the current system
        /// </summary>
        public double elapsedTime => time.ElapsedTime;
        /// <summary>
        /// The global system version of the previous time this system updated,
        /// for use with change filtering methods such as ArchetypeChunk.DidChange()
        /// </summary>
        public uint lastSystemVersion => m_state->LastSystemVersion;

        /// <summary>
        /// Gets the cached default EntityQuery of an IJobEach, which is the query used when the job is dispatched
        /// without one. The query is only created if it is used.
        /// </summary>
        /// <typeparam name="T">The IJobEach whose query to retrieve</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public EntityQuery GetDefaultQuery<T>() where T : struct, IJobEach
        {
            return m_thisPtr->__GetJobDefaultQuery<T>();
        }

        /// <summary>
        /// Gets the cached TGettable and updates it
        /// </summary>
        /// <typeparam name="TGettable">Some type of handle or lookup that needs to be cached by the system</typeparam>
        /// <returns>The updated TGettable</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TGettable Get<TGettable>() where TGettable : unmanaged, ILatiosApiGettable
        {
            var result = m_thisPtr->__Get<TGettable>();
            result.UpdateForApi(ref *m_state);
            return result;
        }
        /// <summary>
        /// Gets the cached TGettable that was constructed by the specified bool parameter (must be a literal in the code) and updates it
        /// </summary>
        /// <typeparam name="TGettable">Some type of handle or lookup that needs to be cached by the system</typeparam>
        /// <returns>The updated TGettable</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TGettable Get<TGettable>(bool b) where TGettable : unmanaged, ILatiosApiGettableBool
        {
            var result = m_thisPtr->__Get<TGettable>(b);
            result.UpdateForApi(ref *m_state);
            return result;
        }
        /// <summary>
        /// Gets the cached ComponentTypeHandle from the system using the specified access.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ComponentTypeHandle<T> GetComponentHandle<T>(bool readOnly) where T : unmanaged, IComponentData
        {
            var result = m_thisPtr->__Get<ComponentTypeHandle<T> >(readOnly);
            result.Update(ref *m_state);
            return result;
        }
        /// <summary>
        /// Gets the cached ComponentLookup from the system using the specified access.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ComponentLookup<T> GetComponentLookup<T>(bool readOnly) where T : unmanaged, IComponentData
        {
            var result = m_thisPtr->__Get<ComponentLookup<T> >(readOnly);
            result.Update(ref *m_state);
            return result;
        }
        /// <summary>
        /// Gets the cached BufferTypeHandle from the system using the specified access.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BufferTypeHandle<T> GetBufferHandle<T>(bool readOnly) where T : unmanaged, IBufferElementData
        {
            var result = m_thisPtr->__Get<BufferTypeHandle<T> >(readOnly);
            result.Update(ref *m_state);
            return result;
        }
        /// <summary>
        /// Gets the cached BufferLookup from the system using the specified access.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BufferLookup<T> GetBufferLookup<T>(bool readOnly) where T : unmanaged, IBufferElementData
        {
            var result = m_thisPtr->__Get<BufferLookup<T> >(readOnly);
            result.Update(ref *m_state);
            return result;
        }
        /// <summary>
        /// Gets the cached BufferTypeHandle from the system using the specified access.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SharedComponentTypeHandle<T> GetSharedComponentHandle<T>(bool readOnly) where T : unmanaged, ISharedComponentData
        {
            var result = m_thisPtr->__Get<SharedComponentTypeHandle<T> >(readOnly);
            result.Update(ref *m_state);
            return result;
        }
        /// <summary>
        /// Gets the cached EntityTypeHandle.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public EntityTypeHandle GetEntityHandle()
        {
            var result = m_thisPtr->__Get<EntityTypeHandle>();
            result.Update(ref *m_state);
            return result;
        }
        /// <summary>
        /// Gets the cached EntityStorageInfoLookup.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public EntityStorageInfoLookup GetEntityStorageInfoLookup()
        {
            var result = m_thisPtr->__Get<EntityStorageInfoLookup>();
            result.Update(ref *m_state);
            return result;
        }
    }

    public static class LatiosApiCreateExtensions
    {
        /// <summary>
        /// Gets the API providing access to source-generated caches for the ISystem
        /// </summary>
        public static unsafe LatiosApiInvoker<T> GetApi<T>(ref this T system, ref SystemState state) where T : unmanaged, ISystem, ILatiosApi
        {
            // Todo: We could grab the pointer from the system parameter directly. Doing it this way seems safer though.
            var ptr   = (T*)UnsafeUtility.AddressOf(ref state.WorldUnmanaged.GetUnsafeSystemRefFromSystemState<T>(ref state));
            var world = ptr->__GetLatiosWorldUnmanaged();
            CheckOnCreateForLatiosWasCalled(world);
            fixed (SystemState* statePtr = &state)
            return new LatiosApiInvoker<T>
            {
                m_thisPtr     = ptr,
                m_state       = statePtr,
                m_latiosWorld = world
            };
        }

        /// <summary>
        /// Call at the very beginning of OnCreate() to setup future calls to GetApi().
        /// </summary>
        public static unsafe LatiosApiInvoker<T> OnCreateForLatios<T>(ref this T system, ref SystemState state) where T : unmanaged, ISystem, ILatiosApi
        {
            system.__OnCreateForLatios(ref state);
            return GetApi(ref system, ref state);
        }

        /// <summary>
        /// Populates compatible fields possessing the [Inject] attribute with cached instances.
        /// Must be called from a method within the ILatiosApi type that created the api.
        /// </summary>
        /// <param name="api">The api of the ILatiosApi</param>
        /// <returns>Returns the injected result</returns>
        public static TInject Inject<TInject, TSystem>(this TInject injectable, LatiosApiInvoker<TSystem> api) where TInject : unmanaged, IInjectable where TSystem : unmanaged,
        ISystem, ILatiosApi
        {
            return injectable.InjectByRef(api);
        }

        /// <summary>
        /// Populates in-place the compatible fields possessing the [Inject] attribute with cached instances.
        /// Must be called from a method within the ILatiosApi type that created the api.
        /// </summary>
        /// <param name="api">The api of the ILatiosApi</param>
        /// <returns>Returns the injected result by ref, the same instance that this was called on</returns>
        public static unsafe ref TInject InjectByRef<TInject, TSystem>(ref this TInject injectable, LatiosApiInvoker<TSystem> api) where TInject : unmanaged,
        IInjectable where TSystem : unmanaged, ISystem, ILatiosApi
        {
            var cache = api.m_thisPtr->__Get<TInject>();
            cache.__Inject(ref *api.m_state, ref injectable);
            return ref injectable;
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS"), Conditional("UNITY_DOTS_DEBUG")]
        static void CheckOnCreateForLatiosWasCalled(LatiosWorldUnmanaged latiosWorld)
        {
            if (!latiosWorld.isValid)
                throw new System.InvalidOperationException("GetApi() failed because the backing cache has not been initialized. Did you forget to call OnCreateForLatios()?");
        }
    }
}

