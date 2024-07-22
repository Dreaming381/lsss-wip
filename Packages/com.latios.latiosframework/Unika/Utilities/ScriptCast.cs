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
    }
}

