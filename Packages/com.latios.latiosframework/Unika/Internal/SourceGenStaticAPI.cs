using System.Diagnostics;
using System.Security.Cryptography;
using NUnit.Framework.Internal;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;

namespace Latios.Unika.InternalSourceGen
{
    public static partial class StaticAPI
    {
        #region Types
        public interface IInterfaceData : IScriptTyped
        {
        }

        public interface IInterfaceDataTyped<TInterface, TInterfaceStruct> : IInterfaceData where TInterface : IUnikaInterface
            where TInterfaceStruct : unmanaged, IInterfaceDataTyped<TInterface, TInterfaceStruct>
        {
            TInterfaceStruct assign { set; }

            bool IScriptTyped.TryCastInit(in Script script)
            {
                var idAndMask = ScriptTypeInfoManager.GetInterfaceRuntimeIdAndMask<TInterface>();
                if ((script.m_headerRO.bloomMask & idAndMask.bloomMask) == idAndMask.bloomMask)
                {
                    if (ScriptVTable.TryGet((short)script.m_headerRO.scriptType, idAndMask.runtimeId, out var functionPtr))
                    {
                        var result = new InterfaceData
                        {
                            functionPointer = functionPtr,
                            script          = script
                        };
                        assign = UnsafeUtility.As<InterfaceData, TInterfaceStruct>(ref result);
                        return true;
                    }
                }
                return false;
            }
        }

        public struct InterfaceData
        {
            internal FunctionPointer<BurstDispatchScriptDelegate> functionPointer;
            internal Script                                       script;

            public Entity entity => script.entity;
            public EntityScriptCollection allScripts => script.allScripts;
            public int indexInEntity => script.indexInEntity;
            public byte userByte { get => script.userByte; set => script.userByte    = value; }
            public bool userFlagA { get => script.userFlagA; set => script.userFlagA = value; }
            public bool userFlagB { get => script.userFlagB; set => script.userFlagB = value; }
            public Script ToScript() => script;
            public T ToRef<T>() where T : unmanaged, IInterfaceRefData
            {
                var data = new InterfaceRefData { scriptRef = script };
                return UnsafeUtility.As<InterfaceRefData, T>(ref data);
            }
        }

        public interface IInterfaceRefData
        {
        }

        public struct InterfaceRefData
        {
            internal ScriptRef scriptRef;

            public Entity entity => scriptRef.entity;
            public ScriptRef ToScriptRef() => scriptRef;
        }
        #endregion

        #region Casting
        public static TDst DownCast<TDst, TDstInterface>(InterfaceData src) where TDst : unmanaged, IInterfaceData where TDstInterface : unmanaged, IUnikaInterface
        {
            InterfaceData dst = default;
            dst.script        = src.script;
            var type          = (short)src.script.m_headerRO.scriptType;
            ScriptVTable.TryGet(type, ScriptTypeInfoManager.GetInterfaceRuntimeIdAndMask<TDstInterface>().runtimeId, out dst.functionPointer);
            return UnsafeUtility.As<InterfaceData, TDst>(ref dst);
        }

        public static bool TryResolve<TDst>(ref ScriptRef src, in EntityScriptCollection allScripts, out TDst dst)
            where TDst : unmanaged, IScriptTyped
        {
            if (ScriptCast.TryResolve(ref src, in allScripts, out var script))
            {
                dst = default;
                return dst.TryCastInit(in script);
            }
            dst = default;
            return false;
        }

        public static bool TryResolve<TDst>(ref InterfaceRefData src, in EntityScriptCollection allScripts, out TDst dst)
            where TDst : unmanaged, IScriptTyped
        {
            return TryResolve(ref src.scriptRef, in allScripts, out dst);
        }

        public static bool TryResolve<TDst, TResolver>(ref ScriptRef src, ref TResolver resolver, out TDst dst)
            where TDst : unmanaged, IScriptTyped
            where TResolver : unmanaged, IScriptResolverBase
        {
            if (ScriptCast.TryResolve(ref src, ref resolver, out var script))
            {
                dst = default;
                return dst.TryCastInit(in script);
            }
            dst = default;
            return false;
        }

        public static bool TryResolve<TDst, TResolver>(ref InterfaceRefData src, ref TResolver resolver, out TDst dst)
            where TDst : unmanaged, IScriptTyped
            where TResolver : unmanaged, IScriptResolverBase
        {
            return TryResolve(ref src.scriptRef, ref resolver, out dst);
        }

        public static TDst Resolve<TDst>(ref ScriptRef src, in EntityScriptCollection allScripts)
            where TDst : unmanaged, IScriptTyped
        {
            var found = TryResolve<TDst>(ref src, in allScripts, out var dst);
            ScriptCast.AssertInCollection(found, allScripts.entity);
            return dst;
        }

        public static TDst Resolve<TDst>(ref InterfaceRefData src, in EntityScriptCollection allScripts)
            where TDst : unmanaged, IScriptTyped
        {
            return Resolve<TDst>(ref src.scriptRef, in allScripts);
        }

        public static TDst Resolve<TDst, TResolver>(ref ScriptRef src, ref TResolver resolver)
            where TDst : unmanaged, IScriptTyped
            where TResolver : unmanaged, IScriptResolverBase
        {
            var  script = ScriptCast.Resolve(ref src, ref resolver);
            TDst dst    = default;
            if (dst.TryCastInit(in script))
                return dst;
            ThrowBadCastOnResolve(script);
            return default;
        }

        public static TDst Resolve<TDst, TResolver>(ref InterfaceRefData src, ref TResolver resolver)
            where TDst : unmanaged, IScriptTyped
            where TResolver : unmanaged, IScriptResolverBase
        {
            return Resolve<TDst, TResolver>(ref src.scriptRef, ref resolver);
        }
        #endregion

        #region Dispatch
        unsafe struct ZeroArg
        {
            public void* script;
        }

        public static unsafe void Dispatch(ref InterfaceData data, int operation)
        {
            var context = new ZeroArg
            {
                script = data.script.GetUnsafePtrAsBytePtr()
            };
            data.functionPointer.Invoke(&context, operation);
        }

        unsafe struct OneArg
        {
            public void* script;
            public void* arg0;
        }

        public static unsafe void Dispatch(ref InterfaceData data, int operation, ref byte arg0)
        {
            fixed (byte* a0 = &arg0)
            {
                var context = new OneArg
                {
                    script = data.script.GetUnsafePtrAsBytePtr(),
                    arg0   = a0
                };
                data.functionPointer.Invoke(&context, operation);
            }
        }

        unsafe struct TwoArg
        {
            public void* script;
            public void* arg0;
            public void* arg1;
        }

        public static unsafe void Dispatch(ref InterfaceData data, int operation, ref byte arg0, ref byte arg1)
        {
            fixed (byte* a0 = &arg0, a1 = &arg1)
            {
                var context = new TwoArg
                {
                    script = data.script.GetUnsafePtrAsBytePtr(),
                    arg0   = a0,
                    arg1   = a1,
                };
                data.functionPointer.Invoke(&context, operation);
            }
        }

        // Todo: 3 - 16
        #endregion

        #region Safety
        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        static void ThrowBadCastOnResolve(Script script)
        {
            throw new System.InvalidCastException($"{script.ToFixedString()} does not implement the requested interface.");
        }
        #endregion
    }

    interface ITestInterface : IUnikaInterface
    {
    }

    struct TestStruct : StaticAPI.IInterfaceDataTyped<ITestInterface, TestStruct>
    {
        StaticAPI.InterfaceData data;

        TestStruct StaticAPI.IInterfaceDataTyped<ITestInterface, TestStruct>.assign { set => data = value.data; }

        public Entity entity => throw new System.NotImplementedException();

        public EntityScriptCollection allScripts => throw new System.NotImplementedException();

        public int indexInEntity => throw new System.NotImplementedException();

        public byte userByte { get => throw new System.NotImplementedException(); set => throw new System.NotImplementedException(); }
        public bool userFlagA { get => throw new System.NotImplementedException(); set => throw new System.NotImplementedException(); }
        public bool userFlagB { get => throw new System.NotImplementedException(); set => throw new System.NotImplementedException(); }

        public ScriptRef ToRef()
        {
            throw new System.NotImplementedException();
        }

        public Script ToScript()
        {
            throw new System.NotImplementedException();
        }
    }

    //[BurstCompile]
    static class TestStaticClass
    {
        //[BurstCompile]
        public static void DoTest()
        {
            Script    script    = default;
            ScriptRef scriptRef = script;
            StaticAPI.TryResolve<TestStruct>(ref scriptRef, script.allScripts, out var result);
        }
    }
}

