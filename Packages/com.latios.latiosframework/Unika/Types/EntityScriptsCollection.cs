using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;

namespace Latios.Unika
{
    public struct EntityScriptCollection
    {
        internal NativeArray<ScriptHeader> buffer;
        internal Entity                    entity;

        public Enumerator GetEnumerator() => new Enumerator(this);

        public struct Enumerator
        {
            NativeArray<ScriptHeader> buffer;
            Entity                    entity;
            int                       index;
            int                       count;
            int                       baseByteOffset;

            public Enumerator(EntityScriptCollection collection)
            {
                buffer         = collection.buffer;
                entity         = collection.entity;
                index          = -1;
                count          = buffer.Length > 0 ? buffer[0].instanceCount : 0;
                baseByteOffset = (1 + math.ceilpow2(count)) * UnsafeUtility.SizeOf<ScriptHeader>();
            }

            public Script Current => new Script
            {
                m_scriptBuffer = buffer,
                m_entity       = entity,
                m_headerOffset = (1 + index) * UnsafeUtility.SizeOf<ScriptHeader>(),
                m_byteOffset   = baseByteOffset + buffer[index + 1].byteOffset
            };

            public bool MoveNext()
            {
                index++;
                return index < count;
            }
        }
    }

    public static class ScriptsDynamicBufferExtensions
    {
        // Note: The wrong entity here could lead to crashes or other bad behavior
        public static EntityScriptCollection AllScripts(this DynamicBuffer<UnikaScripts> buffer, Entity entity)
        {
            return new EntityScriptCollection
            {
                buffer = buffer.Reinterpret<ScriptHeader>().AsNativeArray(),
                entity = entity
            };
        }

        public static EntityScriptCollection AllScripts(this NativeArray<UnikaScripts> buffer, Entity entity)
        {
            return new EntityScriptCollection
            {
                buffer = buffer.Reinterpret<ScriptHeader>(),
                entity = entity
            };
        }
    }
}

