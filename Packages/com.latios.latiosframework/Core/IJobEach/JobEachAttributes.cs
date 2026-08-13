using System;

namespace Latios
{
    /// <summary>
    /// Adds the specified types to the IJobEach's EntityQuery as required, ignoring enabled states.
    /// This is the attribute form of FluentQuery.With().
    /// </summary>
    [AttributeUsage(AttributeTargets.Struct, AllowMultiple = true)]
    public sealed class WithAttribute : Attribute
    {
        /// <param name="componentTypes">The types to require</param>
        public WithAttribute(params Type[] componentTypes)
        {
        }
    }

    /// <summary>
    /// Requires the specified enableable types be present and enabled.
    /// This is the attribute form of FluentQuery.WithEnabled().
    /// </summary>
    /// <remarks>
    /// If applied to an Execute() parameter instead, this requires that parameter's own component be enabled.
    /// Valid on any parameter whose component type is an IEnableableComponent.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Struct | AttributeTargets.Parameter, AllowMultiple = true)]
    public sealed class WithEnabledAttribute : Attribute
    {
        /// <summary>
        /// Requires the decorated Execute() parameter's component be enabled
        /// </summary>
        public WithEnabledAttribute()
        {
        }

        /// <param name="componentTypes">The enableable types which must be present and enabled</param>
        public WithEnabledAttribute(params Type[] componentTypes)
        {
        }
    }

    /// <summary>
    /// Requires at least one of the specified types be present and enabled.
    /// This is the attribute form of FluentQuery.WithAnyEnabled().
    /// </summary>
    [AttributeUsage(AttributeTargets.Struct, AllowMultiple = true)]
    public sealed class WithAnyEnabledAttribute : Attribute
    {
        /// <param name="componentTypes">The types of which at least one must be present and enabled</param>
        public WithAnyEnabledAttribute(params Type[] componentTypes)
        {
        }
    }

    /// <summary>
    /// Requires the specified types be absent from the archetype. This is the attribute form of FluentQuery.Without().
    /// A present but disabled component does not satisfy this; use [WithoutEnabled] for that.
    /// </summary>
    [AttributeUsage(AttributeTargets.Struct, AllowMultiple = true)]
    public sealed class WithoutAttribute : Attribute
    {
        /// <param name="componentTypes">The types which must be absent</param>
        public WithoutAttribute(params Type[] componentTypes)
        {
        }
    }

    /// <summary>
    /// Requires the specified enableable types be either absent or disabled.
    /// This is the attribute form of FluentQuery.WithoutEnabled().
    /// </summary>
    [AttributeUsage(AttributeTargets.Struct, AllowMultiple = true)]
    public sealed class WithoutEnabledAttribute : Attribute
    {
        /// <param name="componentTypes">The enableable types which must be absent or disabled</param>
        public WithoutEnabledAttribute(params Type[] componentTypes)
        {
        }
    }

    /// <summary>
    /// Disables all enabled state filtering for the IJobEach's EntityQuery.
    /// This is the attribute form of FluentQuery.IgnoreEnableableBits().
    /// </summary>
    [AttributeUsage(AttributeTargets.Struct)]
    public sealed class IgnoreEnableableBitsAttribute : Attribute
    {
    }

    /// <summary>
    /// Includes entities with the Disabled component in the IJobEach's EntityQuery.
    /// This is the attribute form of FluentQuery.IncludeDisabledEntities().
    /// </summary>
    [AttributeUsage(AttributeTargets.Struct)]
    public sealed class IncludeDisabledEntitiesAttribute : Attribute
    {
    }

    /// <summary>
    /// Includes prefab entities in the IJobEach's EntityQuery.
    /// This is the attribute form of FluentQuery.IncludePrefabs().
    /// </summary>
    [AttributeUsage(AttributeTargets.Struct)]
    public sealed class IncludePrefabsAttribute : Attribute
    {
    }

    /// <summary>
    /// Includes system entities in the IJobEach's EntityQuery.
    /// This is the attribute form of FluentQuery.IncludeSystemEntities().
    /// </summary>
    [AttributeUsage(AttributeTargets.Struct)]
    public sealed class IncludeSystemEntitiesAttribute : Attribute
    {
    }

    /// <summary>
    /// Includes chunk meta entities in the IJobEach's EntityQuery.
    /// This is the attribute form of FluentQuery.IncludeMetaEntities().
    /// </summary>
    [AttributeUsage(AttributeTargets.Struct)]
    public sealed class IncludeMetaEntitiesAttribute : Attribute
    {
    }

    /// <summary>
    /// Enables write group filtering for the IJobEach's EntityQuery.
    /// This is the attribute form of FluentQuery.UseWriteGroups().
    /// </summary>
    [AttributeUsage(AttributeTargets.Struct)]
    public sealed class UseWriteGroupsAttribute : Attribute
    {
    }

    /// <summary>
    /// Makes JobContext.entityIndexInQuery available to a parallel IJobEach dispatch, at the cost of an extra setup job.
    /// Unnecessary for single-threaded dispatches and for jobs using a grouped parameter, which always provide it.
    /// </summary>
    [AttributeUsage(AttributeTargets.Struct)]
    public sealed class RequireEntityIndexInQueryAttribute : Attribute
    {
    }

    /// <summary>
    /// Makes an Execute() parameter's component optional, leaving it out of the EntityQuery.
    /// Valid on the Ref, EnableableRef, EnabledRef, and DynamicBuffer parameter kinds.
    /// A default value is passed in for a not-present component.
    /// </summary>
    /// <remarks>
    /// Test the parameter with IsValid, or IsCreated for a DynamicBuffer, before accessing it. IsValid is always false for
    /// a zero-sized component, so use Has&lt;T&gt; for those. On a custom parameter type, this has no effect unless a
    /// parameter maps it to a specific handle type.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Parameter)]
    public sealed class NotRequiredAttribute : Attribute
    {
    }

    /// <summary>
    /// Restricts the IJobEach to the specified dispatch modes, such as single-threaded only for a job using a container
    /// which cannot be written in parallel. The job's parameters may restrict the modes further.
    /// </summary>
    [AttributeUsage(AttributeTargets.Struct)]
    public sealed class AllowScheduleModesAttribute : Attribute
    {
        /// <param name="modeMask">The dispatch modes the job is allowed to use</param>
        public AllowScheduleModesAttribute(IJobEach.ScheduleModeMask modeMask)
        {
        }
    }
}

