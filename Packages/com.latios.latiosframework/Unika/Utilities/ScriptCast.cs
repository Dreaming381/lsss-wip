using System.Diagnostics;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Latios.Unika
{
    // This API is useful for generics
    public static class ScriptCast
    {
        public static bool Is<T>(in Script script) where T : unmanaged, IUnikaScript
        {
            return script.m_headerRO.scriptType == ScriptTypeInfoManager.GetScriptRuntimeId<T>().runtimeId;
        }

        public static bool TryCast<T>(in Script script, out Script<T> casted) where T : unmanaged, IUnikaScript
        {
            if (Is<T>(in script))
            {
                casted = new Script<T>
                {
                    m_scriptBuffer = script.m_scriptBuffer,
                    m_entity       = script.m_entity,
                    m_headerOffset = script.m_headerOffset,
                    m_byteOffset   = script.m_byteOffset,
                };
                return true;
            }
            casted = default;
            return false;
        }

        public static bool TryResolve(ref ScriptRef scriptRef, in EntityScriptCollection allScripts, out Script script)
        {
            if (allScripts.entity != scriptRef.entity || allScripts.m_buffer.Length == 0)
            {
                script = default;
                return false;
            }

            if (math.clamp(scriptRef.m_cachedHeaderIndex, 0, allScripts.length) == scriptRef.m_cachedHeaderIndex)
            {
                var candidate = allScripts[scriptRef.m_cachedHeaderIndex];
                if (candidate.m_headerRO.instanceId == scriptRef.m_instanceId)
                {
                    script = candidate;
                    return true;
                }
            }

            int foundIndex = 0;
            foreach (var s in allScripts)
            {
                if (s.m_headerRO.instanceId == scriptRef.m_instanceId)
                {
                    script                        = s;
                    scriptRef.m_cachedHeaderIndex = foundIndex;
                    return true;
                }
                foundIndex++;
            }

            scriptRef.m_cachedHeaderIndex = -1;
            script                        = default;
            return false;
        }

        public static bool TryResolve<T>(ref ScriptRef<T> scriptRef, in EntityScriptCollection allScripts, out Script<T> script) where T : unmanaged, IUnikaScript
        {
            ScriptRef r = scriptRef;
            if (TryResolve(ref r, in allScripts, out var s))
            {
                if (TryCast(s, out script))
                {
                    scriptRef.m_cachedHeaderIndex = r.m_cachedHeaderIndex;
                    return true;
                }
            }
            script = default;
            return false;
        }

        public static bool TryResolve<TResolver>(ref ScriptRef scriptRef, ref TResolver resolver, out Script script) where TResolver : unmanaged, IScriptResolverBase
        {
            if (resolver.TryGet(scriptRef.entity, out var allScripts))
            {
                return TryResolve(ref scriptRef, allScripts, out script);
            }
            script = default;
            return false;
        }

        public static bool TryResolve<TResolver, TType>(ref ScriptRef<TType> scriptRef, ref TResolver resolver, out Script<TType> script)
            where TResolver : unmanaged, IScriptResolverBase
            where TType : unmanaged, IUnikaScript
        {
            if (resolver.TryGet(scriptRef.entity, out var allScripts))
            {
                return TryResolve(ref scriptRef, allScripts, out script);
            }
            script = default;
            return false;
        }

        public static Script Resolve<TResolver>(ref ScriptRef scriptRef, ref TResolver resolver) where TResolver : unmanaged, IScriptResolverBase
        {
            resolver.TryGet(scriptRef.entity, out var allScripts, true);
            bool found = TryResolve(ref scriptRef, allScripts, out var script);
            AssertInCollection(found, allScripts.entity);
            return script;
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        internal static void AssertInCollection(bool inCollection, Entity entity)
        {
            if (!inCollection)
                throw new System.InvalidOperationException($"The script instance could not be found in {entity.ToFixedString()}");
        }
    }
}

