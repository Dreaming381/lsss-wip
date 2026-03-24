using System;
using Latios.Unsafe;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Latios.AuxEcs
{
    public interface IAuxDisposable : IDisposable, IVInterface
    {
        public struct VPtrFunction
        {
            global::Unity.Burst.FunctionPointer<global::Latios.Unsafe.Internal.StaticAPI.BurstDispatchVptrDelegate> __functionPtr;
        }

        public struct VPtr : IAuxDisposable
        {
            global::Latios.Unsafe.Internal.StaticAPI.VPtr __ptr;
            VPtrFunction                                  __function;

            public VPtrFunction vptrFunction => __function;
            public VPtr(global::Latios.Unsafe.UnsafeApiPointer pointer, VPtrFunction function)
            {
                __ptr      = global::Latios.Unsafe.Internal.StaticAPI.VPtr.Create(pointer);
                __function = function;
            }
            public static VPtr Create<T>(global::Latios.Unsafe.UnsafeApiPointer<T> pointer) where T : unmanaged, IAuxDisposable
            {
                var functionPtr = global::Latios.Unsafe.Internal.StaticAPI.GetFunctionChecked<IAuxDisposable, T>();
                return new VPtr(pointer, global::Latios.Unsafe.Internal.StaticAPI.ConvertFunctionPointerToWrapper<VPtrFunction>(functionPtr));
            }
            public void Dispose()
            {
                var vptrDelegate = global::Latios.Unsafe.Internal.StaticAPI.ConvertFunctionPointerFromWrapper(__function);
                global::Latios.Unsafe.Internal.StaticAPI.Dispatch(__ptr, vptrDelegate, 0);
            }

            // For each interface
            [global::AOT.MonoPInvokeCallback(typeof(global::Latios.Unsafe.Internal.StaticAPI.BurstDispatchVptrDelegate))]
            [global::UnityEngine.Scripting.Preserve]
            [global::Unity.Burst.BurstCompile]
            public static void __BurstDispatch_IAuxDisposable(global::Latios.Unsafe.Internal.StaticAPI.ContextPtr context, int operation)
            {
                IAuxDisposable.__Dispatch<VPtr>(context, operation);
            }

            public static void __Initialize()
            {
                // For each interface
                {
                    var functionPtr = global::Unity.Burst.BurstCompiler.CompileFunctionPointer<global::Latios.Unsafe.Internal.StaticAPI.BurstDispatchVptrDelegate>(
                        __BurstDispatch_IAuxDisposable);
                    global::Latios.Unsafe.Internal.StaticAPI.RegisterVptrFunction<IAuxDisposable, VPtr>(functionPtr);
                }
            }

            void IVInterface.__ThisMethodIsSupposedToBeGeneratedByASourceGenerator()
            {
            }
        }

        public static VPtrFunction GetVPtrFunctionFrom<T>() where T : unmanaged, IAuxDisposable
        {
            global::Latios.Unsafe.Internal.StaticAPI.TryGetFunction<IAuxDisposable, T>(out var functionPtr);
            return global::Latios.Unsafe.Internal.StaticAPI.ConvertFunctionPointerToWrapper<VPtrFunction>(functionPtr);
        }

        public static bool TryGetVptrFunctionFrom(long structTypeBurstHash, out VPtrFunction function)
        {
            var result = global::Latios.Unsafe.Internal.StaticAPI.TryGetFunction<IAuxDisposable>(structTypeBurstHash, out var functionPtr);
            function   = result ? global::Latios.Unsafe.Internal.StaticAPI.ConvertFunctionPointerToWrapper<VPtrFunction>(functionPtr) : default;
            return result;
        }

        public static void __Dispatch<T>(global::Latios.Unsafe.Internal.StaticAPI.ContextPtr __context, int __operation) where T : unmanaged, IAuxDisposable
        {
            switch (__operation)
            {
                case 0:
                {
                    ref var obj = ref global::Latios.Unsafe.Internal.StaticAPI.ExtractObject<T>(__context);
                    obj.Dispose();
                    break;
                }
            }
        }
    }

    public struct TestDisposable : IAuxDisposable
    {
        public void Dispose()
        {
        }

        // For each interface
        [global::AOT.MonoPInvokeCallback(typeof(global::Latios.Unsafe.Internal.StaticAPI.BurstDispatchVptrDelegate))]
        [global::UnityEngine.Scripting.Preserve]
        [global::Unity.Burst.BurstCompile]
        public static void __BurstDispatch_IAuxDisposable(global::Latios.Unsafe.Internal.StaticAPI.ContextPtr context, int operation)
        {
            IAuxDisposable.__Dispatch<TestDisposable>(context, operation);
        }

        public static void __Initialize()
        {
            // For each interface
            {
                var functionPtr = global::Unity.Burst.BurstCompiler.CompileFunctionPointer<global::Latios.Unsafe.Internal.StaticAPI.BurstDispatchVptrDelegate>(
                    __BurstDispatch_IAuxDisposable);
                global::Latios.Unsafe.Internal.StaticAPI.RegisterVptrFunction<IAuxDisposable, TestDisposable>(functionPtr);
            }
        }

        void IVInterface.__ThisMethodIsSupposedToBeGeneratedByASourceGenerator()
        {
        }
    }
}

