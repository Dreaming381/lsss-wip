using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

namespace Latios.Unika
{
    // Resolves [Needs], [CopyNeeds], and [ComponentBlacklist] attributes into a per-context,
    // de-duplicated set of component type dependencies, stored in shared statics so that
    // ComponentBrokerBuilderContextExtensions.WithContext<TContext>() can consume them without
    // reflection at call time.
    internal static class ContextNeedsManager
    {
        static readonly MethodInfo s_registerNeedsMethod =
            typeof(ContextNeedsManager).GetMethod(nameof(RegisterNeedsGeneric), BindingFlags.NonPublic | BindingFlags.Static);

        // Each context has its own shared static type, so we can't enumerate them for disposal.
        // Instead, each context records its own disposer the first time it is touched.
        static List<Action> s_disposers = new List<Action>();

        public static void InitializeStatics()
        {
            s_disposers.Clear();
        }

        public static void DisposeStatics()
        {
            foreach (var dispose in s_disposers)
                dispose();
            s_disposers.Clear();
        }

        public static void ProcessScriptType(Type scriptType)
        {
            var needsAttributes = (NeedsAttribute[])Attribute.GetCustomAttributes(scriptType, typeof(NeedsAttribute));
            foreach (var needs in needsAttributes)
            {
                if (needs.context == null)
                    throw new InvalidOperationException($"On {scriptType.FullName}, a [Needs] attribute specifies a null context.");
                RegisterNeeds(needs.context, needs.readOnly, needs.componentTypes, scriptType);
            }

            if (needsAttributes.Length == 0)
                return;

            var copyAttributes = (CopyNeedsAttribute[])Attribute.GetCustomAttributes(scriptType, typeof(CopyNeedsAttribute));
            foreach (var copy in copyAttributes)
            {
                if (copy.fromContext == null)
                    throw new InvalidOperationException($"On {scriptType.FullName}, a [CopyNeeds] attribute specifies a null fromContext.");
                if (copy.toContexts == null)
                    continue;

                foreach (var needs in needsAttributes)
                {
                    if (needs.context != copy.fromContext)
                        continue;

                    foreach (var toContext in copy.toContexts)
                    {
                        if (toContext == null)
                            throw new InvalidOperationException($"On {scriptType.FullName}, a [CopyNeeds] attribute specifies a null target context.");
                        RegisterNeeds(toContext, needs.readOnly, needs.componentTypes, scriptType);
                    }
                }
            }
        }

        static void RegisterNeeds(Type contextType, bool readOnly, Type[] componentTypes, Type scriptTypeForErrors)
        {
            if (componentTypes == null || componentTypes.Length == 0)
                return;

            var method = s_registerNeedsMethod.MakeGenericMethod(contextType);
            try
            {
                method.Invoke(null, new object[] { readOnly, componentTypes, scriptTypeForErrors });
            }
            catch (TargetInvocationException e) when (e.InnerException != null)
            {
                throw e.InnerException;
            }
        }

        static void RegisterNeedsGeneric<TContext>(bool readOnly, Type[] componentTypes, Type scriptTypeForErrors) where TContext : struct
        {
            ref var needsSet = ref NeedsLookup<TContext>.s_needs.Data;
            if (!needsSet.IsCreated)
            {
                needsSet = new UnsafeHashSet<ComponentType>(8, Allocator.Persistent);
                s_disposers.Add(() => NeedsLookup<TContext>.s_needs.Data.Dispose());
                BuildBlacklist<TContext>();
            }

            foreach (var componentType in componentTypes)
            {
                TypeIndex typeIndex;
                try
                {
                    typeIndex = TypeManager.GetTypeIndex(componentType);
                }
                catch
                {
                    throw new InvalidOperationException(
                        $"On {scriptTypeForErrors.FullName}, a [Needs] or [CopyNeeds] attribute specifies a type {componentType.FullName} which is not a known component type.");
                }

                CheckBlacklist<TContext>(typeIndex, componentType, readOnly, scriptTypeForErrors);

                var type = readOnly ? ComponentType.ReadOnly(typeIndex) : ComponentType.ReadWrite(typeIndex);
                AddDeduped(ref needsSet, type);
            }
        }

        // Mirrors ComponentBrokerBuilder.With(ComponentType)'s dedup rule: if both read-only and
        // read-write are requested for the same type, read-write wins.
        static void AddDeduped(ref UnsafeHashSet<ComponentType> types, ComponentType type)
        {
            bool readOnly      = type.AccessModeType == ComponentType.AccessMode.ReadOnly;
            var  readOnlyType  = ComponentType.ReadOnly(type.TypeIndex);
            var  readWriteType = ComponentType.ReadWrite(type.TypeIndex);
            if (!readOnly && types.Contains(readOnlyType))
            {
                types.Remove(readOnlyType);
                types.Add(readWriteType);
            }
            else if (!readOnly || !types.Contains(readWriteType))
                types.Add(readOnly ? readOnlyType : readWriteType);
        }

        static void BuildBlacklist<TContext>() where TContext : struct
        {
            ref var blacklist = ref BlacklistLookup<TContext>.s_blacklist.Data;
            blacklist         = new UnsafeList<BlacklistEntry>(4, Allocator.Persistent);
            s_disposers.Add(() => BlacklistLookup<TContext>.s_blacklist.Data.Dispose());

            var attributes = (ComponentBlacklistAttribute[])Attribute.GetCustomAttributes(typeof(TContext), typeof(ComponentBlacklistAttribute));
            foreach (var attribute in attributes)
            {
                if (attribute.componentTypes == null)
                    continue;

                foreach (var componentType in attribute.componentTypes)
                {
                    TypeIndex typeIndex;
                    try
                    {
                        typeIndex = TypeManager.GetTypeIndex(componentType);
                    }
                    catch
                    {
                        throw new InvalidOperationException(
                            $"On {typeof(TContext).FullName}, a [ComponentBlacklist] attribute specifies a type {componentType.FullName} which is not a known component type.");
                    }
                    blacklist.Add(new BlacklistEntry { typeIndex = typeIndex, writeOnly = attribute.writeOnly });
                }
            }
        }

        static void CheckBlacklist<TContext>(TypeIndex typeIndex, Type componentType, bool readOnly, Type scriptTypeForErrors) where TContext : struct
        {
            ref var blacklist = ref BlacklistLookup<TContext>.s_blacklist.Data;
            for (int i = 0; i < blacklist.Length; i++)
            {
                var entry = blacklist[i];
                if (entry.typeIndex != typeIndex)
                    continue;

                // A writeOnly blacklist entry only blocks read-write needs; a full blacklist entry blocks everything.
                if (!entry.writeOnly || !readOnly)
                {
                    throw new InvalidOperationException(
                        $"On {scriptTypeForErrors.FullName}, a [Needs] attribute requests {componentType.FullName} for context {typeof(TContext).FullName}, " +
                        $"but that component type is blacklisted in that context.");
                }
            }
        }

        internal struct BlacklistEntry
        {
            public TypeIndex typeIndex;
            public bool      writeOnly;  // True if only writing is blacklisted
        }

        struct NeedsLookup<TContext>
        {
            public static readonly SharedStatic<UnsafeHashSet<ComponentType> > s_needs = SharedStatic<UnsafeHashSet<ComponentType> >.GetOrCreate<NeedsLookup<TContext> >();
        }

        struct BlacklistLookup<TContext>
        {
            public static readonly SharedStatic<UnsafeList<BlacklistEntry> > s_blacklist =
                SharedStatic<UnsafeList<BlacklistEntry> >.GetOrCreate<BlacklistLookup<TContext> >();
        }

        internal static ref UnsafeHashSet<ComponentType> GetNeeds<TContext>() where TContext : struct => ref NeedsLookup<TContext>.s_needs.Data;
    }
}

