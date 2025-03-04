using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Latios.Unika.Authoring
{
    /// <summary>
    /// The base authoring class for a script of an explicit type. Derived types must implement an IsValid() method and a Bake() method.
    /// </summary>
    /// <typeparam name="T">The type of script this authoring component generates</typeparam>
    public abstract class UnikaScriptAuthoring<T> : UnikaScriptAuthoringBase where T : unmanaged, IUnikaScript, IUnikaScriptGen
    {
        /// <summary>
        /// Gets the explicitly-typed ScriptRef<typeparamref name="T"/> for this script. Call this from other authoring scripts or bakers.
        /// </summary>
        /// <param name="baker">The baker being used to bake whatever consumes the ScriptRef</param>
        /// <param name="transformUsageFlags">The transform flags that should be added to the script's entity, if any</param>
        /// <returns>An explicitly-typed ScriptRef for the script generated by this authoring component, or a Null ScriptRef if this authoring is not valid.</returns>
        new public ScriptRef<T> GetScriptRef(IBaker baker, TransformUsageFlags transformUsageFlags = TransformUsageFlags.None)
        {
            var untypedRef = base.GetScriptRef(baker, transformUsageFlags);
            return new ScriptRef<T>
            {
                m_cachedHeaderIndex = untypedRef.m_cachedHeaderIndex,
                m_entity            = untypedRef.m_entity,
                m_instanceId        = untypedRef.m_instanceId,
            };
        }
    }

    public static class UnikaScriptAuthoringBakerExtensions
    {
        /// <summary>
        /// Convenient method to get the typed ScriptRef from an authoring component only if that reference is valid.
        /// If null is passed in, returns default.
        /// </summary>
        /// <typeparam name="T">The type of script to retrieve</typeparam>
        /// <param name="scriptAuthoring">The authoring script or null</param>
        /// <param name="transformUsageFlags">The transform flags that should be added to the script's entity, if any</param>
        /// <returns>An explicitly-typed ScriptRef for the script generated by this authoring component, or a Null ScriptRef if this authoring is null or not valid.</returns>
        public static ScriptRef<T> GetScriptRefOrDefaultFrom<T>(this IBaker baker,
                                                                UnikaScriptAuthoring<T> scriptAuthoring,
                                                                TransformUsageFlags transformUsageFlags = TransformUsageFlags.None) where T : unmanaged, IUnikaScript,
        IUnikaScriptGen
        {
            if (scriptAuthoring == null)
                return default;
            return scriptAuthoring.GetScriptRef(baker, transformUsageFlags);
        }

        /// <summary>
        /// Convenient method to get the typed ScriptRef from an authoring component only if that reference is valid.
        /// If null is passed in, returns default.
        /// </summary>
        /// <typeparam name="T">The type of script to retrieve</typeparam>
        /// <param name="scriptAuthoring">The authoring script or null</param>
        /// <param name="transformUsageFlags">The transform flags that should be added to the script's entity, if any</param>
        /// <returns>An explicitly-typed ScriptRef for the script generated by this authoring component, or a Null ScriptRef if this authoring is null or not valid.</returns>
        public static T GetInterfaceRefOrDefaultFrom<T>(this IBaker baker,
                                                        IUnikaInterfaceAuthoring<T> interfaceAuthoring,
                                                        TransformUsageFlags transformUsageFlags = TransformUsageFlags.None)
            where T : unmanaged, Unika.InternalSourceGen.StaticAPI.IInterfaceRefData
        {
            if (interfaceAuthoring == null)
                return default;
            return interfaceAuthoring.GetInterfaceRef(baker, transformUsageFlags);
        }

        /// <summary>
        /// Convenient method to get the untyped ScriptRef from an authoring component only if that reference is valid.
        /// If null is passed in, returns default.
        /// </summary>
        /// <param name="scriptAuthoring">The authoring script or null</param>
        /// <param name="transformUsageFlags">The transform flags that should be added to the script's entity, if any</param>
        /// <returns>An untyped ScriptRef for the script generated by this authoring component, or a Null ScriptRef if this authoring is null or not valid.</returns>
        public static ScriptRef GetScriptRefOrDefaultFrom(this IBaker baker,
                                                          UnikaScriptAuthoringBase scriptAuthoring,
                                                          TransformUsageFlags transformUsageFlags = TransformUsageFlags.None)
        {
            if (scriptAuthoring == null)
                return default;
            return scriptAuthoring.GetScriptRef(baker, transformUsageFlags);
        }
    }
}

