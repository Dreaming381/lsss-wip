using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;

namespace Latios.Unika.Authoring
{
    public interface IUnikaInterfaceAuthoring<T> where T : unmanaged, Unika.InternalSourceGen.StaticAPI.IInterfaceRefData
    {
        T GetInterfaceRef(IBaker baker, TransformUsageFlags transformUsageFlags = TransformUsageFlags.None);
    }

    [Serializable]
    public struct InterfaceAuthoring<T> where T : unmanaged, Unika.InternalSourceGen.StaticAPI.IInterfaceRefData
    {
        [UnityEngine.SerializeField, UnityEngine.HideInInspector]
        UnikaScriptAuthoringBase authoringReference;

        public IUnikaInterfaceAuthoring<T> authoringInterface => authoringReference as IUnikaInterfaceAuthoring<T>;

        public InterfaceAuthoring(UnikaScriptAuthoringBase authoringBase)
        {
            authoringReference = authoringBase;
        }

        public InterfaceAuthoring(IUnikaInterfaceAuthoring<T> authoringInterface)
        {
            authoringReference = authoringInterface as UnikaScriptAuthoringBase;
        }
    }

    internal interface IUnikaAuthoringScriptCounter
    {
        public int CountScripts();
    }

    namespace InternalSourceGen
    {
        public static class StaticAPI
        {
            public interface IUnikaInterfaceAuthoringImpl<TInterfaceRef, TScriptType> : IUnikaInterfaceAuthoring<TInterfaceRef>
                where TInterfaceRef : unmanaged, Unika.InternalSourceGen.StaticAPI.IInterfaceRefData
                where TScriptType : unmanaged, IUnikaScript, IUnikaScriptGen
            {
                TInterfaceRef IUnikaInterfaceAuthoring<TInterfaceRef>.GetInterfaceRef(IBaker baker, TransformUsageFlags transformUsageFlags)
                {
                    var scriptRef = (this as UnikaScriptAuthoring<TScriptType>).GetScriptRef(baker, transformUsageFlags);
                    return UnsafeUtility.As<ScriptRef<TScriptType>, TInterfaceRef>(ref scriptRef);
                }
            }
        }
    }
}

