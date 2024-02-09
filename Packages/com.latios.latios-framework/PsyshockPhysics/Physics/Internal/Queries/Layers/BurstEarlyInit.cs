using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.Jobs;
using UnityEngine;

// Todo: This is just a hack until we can do those with source generators and ILPP.

namespace Latios.Psyshock
{
    static class InitJobsForProcessors
    {
#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        static void InitEditor()
        {
            var pairsTypes   = UnityEditor.TypeCache.GetTypesDerivedFrom<IFindPairsProcessor>();
            var objectsTypes = UnityEditor.TypeCache.GetTypesDerivedFrom<IFindObjectsProcessor>();

            InitProcessors(pairsTypes, objectsTypes);
        }
#else
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        static void InitRuntime()
        {
            var pairsTypes   = new List<Type>();
            var objectsTypes = new List<Type>();
            var pairsType    = typeof(IFindPairsProcessor);
            var objectsType  = typeof(IFindObjectsProcessor);
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (!BootstrapTools.IsAssemblyReferencingSubstring(assembly, "Psyshock"))
                    continue;

                try
                {
                    var assemblyTypes = assembly.GetTypes();
                    foreach (var t in assemblyTypes)
                    {
                        if (pairsType.IsAssignableFrom(t))
                            pairsTypes.Add(t);
                        if (objectsType.IsAssignableFrom(t))
                            objectsTypes.Add(t);
                    }
                }
                catch (ReflectionTypeLoadException e)
                {
                    foreach (var t in e.Types)
                    {
                        if (t != null && pairsType.IsAssignableFrom(t))
                            pairsTypes.Add(t);
                        if (t != null && objectsType.IsAssignableFrom(t))
                            objectsTypes.Add(t);
                    }

                    Debug.LogWarning($"Psyshock BurstEarlyInit.cs failed loading assembly: {(assembly.IsDynamic ? assembly.ToString() : assembly.Location)}");
                }
            }

            InitProcessors(pairsTypes, objectsTypes);
        }
#endif

        static void InitProcessors(IEnumerable<Type> findPairsTypes, IEnumerable<Type> findObjectsTypes)
        {
            RuntimeConstants.InitConstants();

            var pairIniterType = typeof(FindPairsIniter<>);

            foreach (var pairType in findPairsTypes)
            {
                try
                {
                    if (pairType.IsGenericType || pairType.IsInterface)
                        continue;

                    var type   = pairIniterType.MakeGenericType(pairType);
                    var initer = Activator.CreateInstance(type) as IIniter;
                    initer.Init();
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }

            var objectsIniterType = typeof(FindObjectsIniter<>);

            foreach (var objectType in findObjectsTypes)
            {
                try
                {
                    if (objectType.IsGenericType || objectType.IsInterface)
                        continue;

                    var type   = objectsIniterType.MakeGenericType(objectType);
                    var initer = Activator.CreateInstance(type) as IIniter;
                    initer.Init();
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }
        }

        interface IIniter
        {
            void Init();
        }

        struct FindPairsIniter<T> : IIniter where T : struct, IFindPairsProcessor
        {
            public void Init()
            {
                IJobForExtensions.EarlyJobInit<FindPairsLayerSelfConfig<T>.FindPairsInternal.LayerSelfJob>();
                IJobForExtensions.EarlyJobInit<FindPairsLayerLayerConfig<T>.FindPairsInternal.LayerLayerJob>();
            }
        }

        struct FindObjectsIniter<T> : IIniter where T : struct, IFindObjectsProcessor
        {
            public void Init()
            {
                IJobExtensions.EarlyJobInit<FindObjectsConfig<T>.FindObjectsInternal.Single>();
            }
        }
    }
}

