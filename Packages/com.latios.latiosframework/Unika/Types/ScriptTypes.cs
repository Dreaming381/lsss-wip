using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;

// Todo: Add IEquatable, IComparable, and Null

namespace Latios.Unika
{
    public unsafe struct Script
    {
        internal NativeArray<ScriptHeader> m_scriptBuffer;
        internal Entity                    m_entity;
        internal int                       m_byteOffset;
        internal int                       m_headerOffset;

        internal ref ScriptHeader m_header => ref *(ScriptHeader*)((byte*)m_scriptBuffer.GetUnsafePtr() + m_headerOffset);
        internal ref readonly ScriptHeader m_headerRO => ref *(ScriptHeader*)((byte*)m_scriptBuffer.GetUnsafeReadOnlyPtr() + m_headerOffset);

        public byte userByte
        {
            get => m_headerRO.userByte;
            set => m_header.userByte = value;
        }

        public bool userFlagA
        {
            get => m_headerRO.userFlagA;
            set => m_header.userFlagA = value;
        }

        public bool userFlagB
        {
            get => m_headerRO.userFlagB;
            set => m_header.userFlagB = value;
        }

        public ScriptRef ToRef() => new ScriptRef
        {
            m_entity            = m_entity,
            m_instanceId        = m_headerRO.instanceId,
            m_cachedHeaderIndex = m_headerOffset / sizeof(ScriptHeader)
        };
    }

    public unsafe struct Script<T> where T : unmanaged, IUnikaScript
    {
        internal NativeArray<ScriptHeader> m_scriptBuffer;
        internal Entity                    m_entity;
        internal int                       m_headerOffset;
        internal int                       m_byteOffset;

        internal ref ScriptHeader m_header => ref *(ScriptHeader*)((byte*)m_scriptBuffer.GetUnsafePtr() + m_headerOffset);
        internal ref readonly ScriptHeader m_headerRO => ref *(ScriptHeader*)((byte*)m_scriptBuffer.GetUnsafeReadOnlyPtr() + m_headerOffset);

        public ref T valueRW => ref *(T*)((byte*)m_scriptBuffer.GetUnsafePtr() + m_byteOffset);
        public ref readonly T valueRO => ref *(T*)((byte*)m_scriptBuffer.GetUnsafeReadOnlyPtr() + m_byteOffset);

        public byte userByte
        {
            get => m_headerRO.userByte;
            set => m_header.userByte = value;
        }

        public bool userFlagA
        {
            get => m_headerRO.userFlagA;
            set => m_header.userFlagA = value;
        }

        public bool userFlagB
        {
            get => m_headerRO.userFlagB;
            set => m_header.userFlagB = value;
        }

        public ScriptRef<T> ToRef() => new ScriptRef<T>
        {
            m_entity            = m_entity,
            m_instanceId        = m_headerRO.instanceId,
            m_cachedHeaderIndex = m_headerOffset / UnsafeUtility.SizeOf<ScriptHeader>()
        };

        public static implicit operator Script(Script<T> script)
        {
            return new Script
            {
                m_scriptBuffer = script.m_scriptBuffer,
                m_entity       = script.m_entity,
                m_headerOffset = script.m_headerOffset,
                m_byteOffset   = script.m_byteOffset,
            };
        }
    }

    public unsafe struct ScriptRef
    {
        internal Entity m_entity;
        internal int    m_instanceId;
        internal int    m_cachedHeaderIndex;
    }

    public unsafe struct ScriptRef<T>
    {
        internal Entity m_entity;
        internal int    m_instanceId;
        internal int    m_cachedHeaderIndex;

        public static implicit operator ScriptRef(ScriptRef<T> script)
        {
            return new ScriptRef
            {
                m_entity            = script.m_entity,
                m_instanceId        = script.m_instanceId,
                m_cachedHeaderIndex = script.m_cachedHeaderIndex
            };
        }
    }
}

