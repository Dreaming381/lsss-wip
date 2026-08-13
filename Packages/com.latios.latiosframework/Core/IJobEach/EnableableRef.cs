using Unity.Collections;
using Unity.Entities;

namespace Latios
{
    /// <summary>
    /// A read-write reference to both the value and the enabled bit of an enableable component.
    /// </summary>
    /// <typeparam name="T">The enableable component type</typeparam>
    public readonly struct EnableableRefRW<T> where T : unmanaged, IComponentData, IEnableableComponent
    {
        readonly RefRW<T>        m_value;
        readonly EnabledRefRW<T> m_enabled;

        /// <summary>
        /// Constructs the reference from a chunk's component array and enabled bit reference
        /// </summary>
        /// <param name="componentDataArray">The chunk's array of components</param>
        /// <param name="index">The index of the entity within the chunk</param>
        /// <param name="enabled">The enabled bit reference for the same entity</param>
        public EnableableRefRW(NativeArray<T> componentDataArray, int index, EnabledRefRW<T> enabled)
        {
            m_value   = new RefRW<T>(componentDataArray, index);
            m_enabled = enabled;
        }

        EnableableRefRW(RefRW<T> value, EnabledRefRW<T> enabled)
        {
            m_value   = value;
            m_enabled = enabled;
        }

        /// <summary>
        /// Constructs the reference, nulling the value if the component array is empty.
        /// The enabled bit is passed through unchanged.
        /// </summary>
        public static EnableableRefRW<T> Optional(NativeArray<T> componentDataArray, int index, EnabledRefRW<T> enabled)
        {
            return new EnableableRefRW<T>(RefRW<T>.Optional(componentDataArray, index), enabled);
        }

        /// <summary>
        /// Converts to a read-only version of this reference
        /// </summary>
        public static explicit operator EnableableRefRO<T>(EnableableRefRW<T> refRW) => new EnableableRefRO<T>((RefRO<T>)refRW.m_value, refRW.m_enabled);

        /// <summary>
        /// Whether it is safe to read or write this reference's value.
        /// This is always false for zero-sized components, even when the component is present.
        /// </summary>
        public bool IsValid => m_value.IsValid;

        /// <summary>
        /// A read-write reference to the component value
        /// </summary>
        public ref T ValueRW => ref m_value.ValueRW;
        /// <summary>
        /// A read-only reference to the component value
        /// </summary>
        public ref readonly T ValueRO => ref m_value.ValueRO;

        /// <summary>
        /// The enabled state of the component
        /// </summary>
        public bool EnabledRO => m_enabled.ValueRO;
        /// <summary>
        /// The enabled state of the component, which can be written to
        /// </summary>
        public bool EnabledRW
        {
            get => m_enabled.ValueRW;
            set => m_enabled.ValueRW = value;
        }

        /// <summary>
        /// The underlying value reference, for APIs which require it
        /// </summary>
        public RefRW<T> asRefRW => m_value;
        /// <summary>
        /// The underlying enabled bit reference, for APIs which require it
        /// </summary>
        public EnabledRefRW<T> asEnabledRefRW => m_enabled;
    }

    /// <summary>
    /// A read-only reference to both the value and the enabled bit of an enableable component.
    /// </summary>
    /// <typeparam name="T">The enableable component type</typeparam>
    public readonly struct EnableableRefRO<T> : IQueryTypeParameter where T : unmanaged, IComponentData, IEnableableComponent
    {
        readonly RefRO<T>        m_value;
        readonly EnabledRefRO<T> m_enabled;

        /// <summary>
        /// Constructs the reference from a chunk's component array and enabled bit reference
        /// </summary>
        /// <param name="componentDataArray">The chunk's array of components</param>
        /// <param name="index">The index of the entity within the chunk</param>
        /// <param name="enabled">The enabled bit reference for the same entity</param>
        public EnableableRefRO(NativeArray<T> componentDataArray, int index, EnabledRefRO<T> enabled)
        {
            m_value   = new RefRO<T>(componentDataArray, index);
            m_enabled = enabled;
        }

        internal EnableableRefRO(RefRO<T> value, EnabledRefRO<T> enabled)
        {
            m_value   = value;
            m_enabled = enabled;
        }

        /// <summary>
        /// Constructs the reference, nulling the value if the component array is empty.
        /// The enabled bit is passed through unchanged.
        /// </summary>
        public static EnableableRefRO<T> Optional(NativeArray<T> componentDataArray, int index, EnabledRefRO<T> enabled)
        {
            return new EnableableRefRO<T>(RefRO<T>.Optional(componentDataArray, index), enabled);
        }

        /// <summary>
        /// Whether it is safe to read this reference's value.
        /// This is always false for zero-sized components, even when the component is present.
        /// </summary>
        public bool IsValid => m_value.IsValid;

        /// <summary>
        /// A read-only reference to the component value
        /// </summary>
        public ref readonly T ValueRO => ref m_value.ValueRO;

        /// <summary>
        /// The enabled state of the component
        /// </summary>
        public bool EnabledRO => m_enabled.ValueRO;

        /// <summary>
        /// The underlying value reference, for APIs which require it
        /// </summary>
        public RefRO<T> asRefRO => m_value;
        /// <summary>
        /// The underlying enabled bit reference, for APIs which require it
        /// </summary>
        public EnabledRefRO<T> asEnabledRefRO => m_enabled;
    }
}

