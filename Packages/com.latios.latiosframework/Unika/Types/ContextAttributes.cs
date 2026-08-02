using System;

namespace Latios.Unika
{
    /// <summary>
    /// Add this to a context struct to mark component types as off-limits for scripts invoked
    /// within that context. If a [Needs] attribute targeting the context violates the blacklist
    /// an exception will be thrown.
    /// </summary>
    [AttributeUsage(AttributeTargets.Struct, AllowMultiple = true)]
    public class ComponentBlacklistAttribute : Attribute
    {
        public Type[] componentTypes;
        public bool   writeOnly;

        /// <param name="writeOnly">If true, only write access to the component types is blocked (read-only access is still allowed).
        /// If false, both read and write access to the component types is blocked.</param>
        /// <param name="componentTypes">The component types that are off-limits within this context.</param>
        public ComponentBlacklistAttribute(bool writeOnly, params Type[] componentTypes)
        {
            this.writeOnly      = writeOnly;
            this.componentTypes = componentTypes;
        }
    }

    /// <summary>
    /// Add this to a Unika script type to declare which component types it needs from a
    /// ComponentBroker when invoked within the specified context.
    /// </summary>
    [AttributeUsage(AttributeTargets.Struct, AllowMultiple = true)]
    public class NeedsAttribute : Attribute
    {
        public Type   context;
        public bool   readOnly;
        public Type[] componentTypes;

        /// <param name="context">The context type this need applies to.</param>
        /// <param name="readOnly">Whether the component types are needed read-only. If false, they are needed read-write.</param>
        /// <param name="componentTypes">The component types needed.</param>
        public NeedsAttribute(Type context, bool readOnly, params Type[] componentTypes)
        {
            this.context        = context;
            this.readOnly       = readOnly;
            this.componentTypes = componentTypes;
        }
    }

    /// <summary>
    /// Add this to a Unika script type to duplicate that same script's own [Needs] attributes
    /// specified for one context onto other context types, avoiding repeated identical [Needs]
    /// declarations for scripts that can run under multiple contexts.
    /// </summary>
    [AttributeUsage(AttributeTargets.Struct, AllowMultiple = true)]
    public class CopyNeedsAttribute : Attribute
    {
        public Type   fromContext;
        public Type[] toContexts;

        /// <param name="fromContext">The context whose [Needs] attributes (declared on this same script type) should be copied.</param>
        /// <param name="toContexts">The context types that should also receive those same needs.</param>
        public CopyNeedsAttribute(Type fromContext, params Type[] toContexts)
        {
            this.fromContext = fromContext;
            this.toContexts  = toContexts;
        }
    }
}

