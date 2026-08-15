using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using Latios.Unsafe;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;

namespace Latios
{
    /// <summary>
    /// A struct implementing this type can be used in a CustomCommandBuffer to provide custom
    /// logic during playback on the main thread.
    /// To create your own type, implement the interface and provide a custom Burst function pointer.
    /// Don't forget to add the [BurstCompile] and [MonoPInvokeCallback] attributes to the static method.
    /// </summary>
    public interface ICustomCommand
    {
        public struct Context
        {
            /// <summary>
            /// An EntityManager to use for processing commands.
            /// </summary>
            public EntityManager entityManager { get; internal set; }
            /// <summary>
            /// Total number of commands of this payload type being played back.
            /// </summary>
            public int count { get; internal set; }

            internal NativeArray<UnsafeIndexedBlockList.ElementPtr> dataPtrs;
            internal int                                            commandOffset;
            internal int                                            expectedSize;

            /// <summary>
            /// Reads the command corresponding to the specified index.
            /// The type should be of this particular type of ICustomCommand,
            /// or a size-aliasable equivalent.
            /// </summary>
            public unsafe T ReadCommand<T>(int index) where T : unmanaged, ICustomCommand
            {
                CheckTypeAccess<T>(expectedSize);
                var ptr = dataPtrs[index].ptr + commandOffset;
                UnsafeUtility.CopyPtrToStructure<T>(ptr, out var result);
                return result;
            }

            [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS"), Conditional("UNITY_DOTS_DEBUG")]
            void CheckTypeAccess<T>(int expectedSize) where T : unmanaged, ICustomCommand
            {
                if (UnsafeUtility.SizeOf<T>() != expectedSize)
                    throw new InvalidOperationException($"Attempted to access a type of size {UnsafeUtility.SizeOf<T>()} when the stored command type is of size {expectedSize}");
            }
        }

        public delegate void OnPlayback(ref Context context);

        /// <summary>
        /// This is called once on application startup on a defaulted instance outside of Burst.
        /// This method should define either a Burst-compiled function pointer or a managed function pointer
        /// that will be invoked once per playback of a CustomCommandBuffer variant containing
        /// the implementing type of ICustomCommand.
        /// </summary>
        /// <returns></returns>
        public FunctionPointer<OnPlayback> GetFunctionPointer();
    }

    internal static class CustomCommandRegistry
    {
        struct SharedKey { }

        public struct TypeToPointer<T> where T : unmanaged, ICustomCommand
        {
            public static readonly SharedStatic< FunctionPointer<ICustomCommand.OnPlayback> > functionPtr =
                SharedStatic< FunctionPointer<ICustomCommand.OnPlayback> >.GetOrCreate<SharedKey, T>();
        }

        static FunctionPointer<ICustomCommand.OnPlayback> GetFunctionPtr<T>() where T : unmanaged, ICustomCommand
        {
            T t = default;
            return t.GetFunctionPointer();
        }

        static void InitCommands(IEnumerable<Type> types)
        {
            var method  = typeof(CustomCommandRegistry).GetMethod(nameof(GetFunctionPtr), BindingFlags.Static | BindingFlags.NonPublic);
            var keyType = typeof(SharedKey);

            foreach (var type in types)
            {
                if (type.IsGenericType || type.IsInterface)
                    continue;

                var generic     = method.MakeGenericMethod(type);
                var functionPtr = (FunctionPointer<ICustomCommand.OnPlayback>)generic.Invoke(
                    null,
                    null);
                SharedStatic<FunctionPointer<ICustomCommand.OnPlayback> >.GetOrCreate(keyType, type).Data = functionPtr;
            }
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        static void InitEditor()
        {
            var commandTypes = UnityEditor.TypeCache.GetTypesDerivedFrom<ICustomCommand>();

            InitCommands(commandTypes);
        }
#else
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.AfterAssembliesLoaded)]
#endif
        internal static void InitRuntime()
        {
            var commandTypes = new List<Type>();
            var commandType  = typeof(ICustomCommand);
            var coreAssembly = commandType.Assembly;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (!BootstrapTools.IsAssemblyReferencingOtherAssembly(assembly, coreAssembly))
                    continue;

                try
                {
                    var assemblyTypes = assembly.GetTypes();
                    foreach (var t in assemblyTypes)
                    {
                        if (commandType.IsAssignableFrom(t))
                            commandTypes.Add(t);
                    }
                }
                catch (ReflectionTypeLoadException e)
                {
                    foreach (var t in e.Types)
                    {
                        if (t != null && commandType.IsAssignableFrom(t))
                            commandTypes.Add(t);
                    }

                    UnityEngine.Debug.LogWarning($"Core ICustomCommand.cs failed loading assembly: {(assembly.IsDynamic ? assembly.ToString() : assembly.Location)}");
                }
            }

            InitCommands(commandTypes);
        }
    }
}
