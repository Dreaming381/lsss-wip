namespace Latios.Unika
{
    /// <summary>
    /// Extension methods for wiring a ComponentBrokerBuilder up to Unika's [Needs] attribute system.
    /// </summary>
    public static class ComponentBrokerBuilderContextExtensions
    {
        /// <summary>
        /// Adds all component type dependencies accumulated from [Needs] (and [CopyNeeds]) attributes
        /// declared by Unika script types for the given context type.
        /// </summary>
        /// <typeparam name="TContext">The context type whose accumulated needs should be added.</typeparam>
        public static ComponentBrokerBuilder WithContext<TContext>(this ComponentBrokerBuilder builder) where TContext : struct
        {
            ref var needs = ref ContextNeedsManager.GetNeeds<TContext>();
            if (!needs.IsCreated)
                return builder;

            foreach (var type in needs)
                builder = builder.With(type);
            return builder;
        }
    }
}
