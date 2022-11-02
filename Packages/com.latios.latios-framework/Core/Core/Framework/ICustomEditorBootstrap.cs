using System.Collections.Generic;
using Latios;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace Latios
{
    public interface ICustomEditorBootstrap
    {
        /// <summary>
        /// Modify the existing Editor World, or
        /// </summary>
        /// <param name="defaultEditorWorld"></param>
        /// <returns></returns>
        World InitializeOrModify(World defaultEditorWorld);
    }

#if UNITY_EDITOR
    [UnityEditor.InitializeOnLoad]
#endif
    internal static class EditorBootstrapUtilities
    {
        static EditorBootstrapUtilities()
        {
            RegisterEditorWorldAction();
        }

        static bool m_isRegistered = false;

        internal static void RegisterEditorWorldAction()
        {
            if (!m_isRegistered)
            {
                m_isRegistered                                                         = true;
                Unity.Entities.Exposed.WorldExposedExtensions.DefaultWorldInitialized += InitializeEditorWorld;
            }
        }

        static void InitializeEditorWorld(World defaultEditorWorld)
        {
            if (World.DefaultGameObjectInjectionWorld != defaultEditorWorld || !defaultEditorWorld.Flags.HasFlag(WorldFlags.Editor))
                return;

            IEnumerable<System.Type> bootstrapTypes;
#if UNITY_EDITOR
            bootstrapTypes = UnityEditor.TypeCache.GetTypesDerivedFrom(typeof(ICustomEditorBootstrap));
#else

            var types = new List<System.Type>();
            var type  = typeof(ICustomEditorBootstrap);
            foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                if (!BootstrapTools.IsAssemblyReferencingLatios(assembly))
                    continue;

                try
                {
                    var assemblyTypes = assembly.GetTypes();
                    foreach (var t in assemblyTypes)
                    {
                        if (type.IsAssignableFrom(t))
                            types.Add(t);
                    }
                }
                catch (ReflectionTypeLoadException e)
                {
                    foreach (var t in e.Types)
                    {
                        if (t != null && type.IsAssignableFrom(t))
                            types.Add(t);
                    }

                    UnityEngine.Debug.LogWarning($"EditorWorldBootstrap failed loading assembly: {(assembly.IsDynamic ? assembly.ToString() : assembly.Location)}");
                }
            }

            bootstrapTypes = types;
#endif

            System.Type selectedType = null;

            foreach (var bootType in bootstrapTypes)
            {
                if (bootType.IsAbstract || bootType.ContainsGenericParameters)
                    continue;

                if (selectedType == null)
                    selectedType = bootType;
                else if (selectedType.IsAssignableFrom(bootType))
                    selectedType = bootType;
                else if (!bootType.IsAssignableFrom(selectedType))
                    UnityEngine.Debug.LogError("Multiple custom ICustomEditorBootstrap exist in the project, ignoring " + bootType);
            }
            if (selectedType == null)
                return;

            ICustomEditorBootstrap bootstrap = System.Activator.CreateInstance(selectedType) as ICustomEditorBootstrap;

            var newWorld = bootstrap.InitializeOrModify(defaultEditorWorld);
            if (newWorld != defaultEditorWorld)
            {
                if (defaultEditorWorld.IsCreated)
                {
                    ScriptBehaviourUpdateOrder.RemoveWorldFromCurrentPlayerLoop(defaultEditorWorld);
                    defaultEditorWorld.Dispose();
                }
                ScriptBehaviourUpdateOrder.AppendWorldToCurrentPlayerLoop(newWorld);
                World.DefaultGameObjectInjectionWorld = newWorld;
            }
        }
    }
}

