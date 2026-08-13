using Unity.Entities;

namespace Latios
{
    /// <summary>
    /// An IJobEach Execute() parameter reporting whether the entity's chunk has a component type,
    /// without requiring that type in the EntityQuery.
    /// </summary>
    /// <remarks>This is the way to branch on a zero-sized tag component, whose RefRO would never report IsValid.</remarks>
    /// <typeparam name="T">The component type to check for</typeparam>
    [IJobEach.ParameterHandle(typeof(HasHandle<>), IJobEach.ScheduleModeMask.All)]
    public readonly struct Has<T> : IJobEach.IParameter
    {
        readonly bool m_value;

        /// <param name="value">Whether the chunk has the component type</param>
        public Has(bool value) => m_value = value;

        /// <summary>
        /// Whether the entity's chunk has the component type
        /// </summary>
        public bool value => m_value;

        public static implicit operator bool(Has<T> has) => has.m_value;
    }

    /// <summary>
    /// The backing handle for a Has&lt;T&gt; Execute() parameter.
    /// </summary>
    /// <typeparam name="T">The component type to check for</typeparam>
    public struct HasHandle<T> : IJobEach.IParameterHandle<Has<T> >, ILatiosApiGettable
    {
        HasChecker<T> m_checker;
        bool          m_chunkHasType;

        /// <inheritdoc />
        public FluentQuery AppendToQuery(FluentQuery query) => query;

        /// <inheritdoc />
        public bool OnChunkBegin(in IJobEach.JobContext context)
        {
            m_chunkHasType = m_checker[context.chunk];
            return true;
        }

        /// <inheritdoc />
        public void OnChunkEnd(in IJobEach.JobContext context, bool chunkWasExecuted)
        {
        }

        /// <inheritdoc />
        public Has<T> GetParameter(in IJobEach.JobContext context) => new Has<T>(m_chunkHasType);

        void ILatiosApiGettable.CreateForApi(ref SystemState state)
        {
        }

        void ILatiosApiGettable.UpdateForApi(ref SystemState state)
        {
        }
    }
}

