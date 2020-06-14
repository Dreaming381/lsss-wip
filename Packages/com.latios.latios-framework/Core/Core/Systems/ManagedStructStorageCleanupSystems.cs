using System;
using System.Diagnostics;
using System.Reflection;
using Unity.Collections;
using Unity.Entities;

namespace Latios.Systems
{
    [DisableAutoCreation]
    public class ManagedComponentsReactiveSystemGroup : RootSuperSystem
    {
        private EntityQuery m_anythingNeedsCreationQuery;
        private EntityQuery m_anythingNeedsDestructionQuery;

        public override bool ShouldUpdateSystem()
        {
            return (m_anythingNeedsCreationQuery.IsEmptyIgnoreFilter && m_anythingNeedsDestructionQuery.IsEmptyIgnoreFilter) == false;
        }

        protected override void CreateSystems()
        {
            var managedCreateType     = typeof(ManagedComponentCreateSystem<>);
            var managedDestroyType    = typeof(ManagedComponentDestroySystem<>);
            var collectionCreateType  = typeof(CollectionComponentCreateSystem<>);
            var collectionDestroyType = typeof(CollectionComponentDestroySystem<>);
            //var managedTagType         = typeof(ManagedComponentTag<>);
            var managedSysStateType = typeof(ManagedComponentSystemStateTag<>);
            //var collectionTagType      = typeof(CollectionComponentTag<>);
            var collectionSysStateType = typeof(CollectionComponentSystemStateTag<>);

            var tagTypes = new NativeList<ComponentType>(Allocator.TempJob);
            var sysTypes = new NativeHashMap<ComponentType, byte>(128, Allocator.TempJob);

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var assembly in assemblies)
            {
                if (!BootstrapTools.IsAssemblyReferencingLatios(assembly))
                    continue;

                foreach (var type in assembly.GetTypes())
                {
                    if (type.GetCustomAttribute(typeof(DisableAutoTypeRegistration)) != null)
                        continue;

                    if (type == typeof(IManagedComponent) || type == typeof(ICollectionComponent))
                        continue;

                    if (typeof(IManagedComponent).IsAssignableFrom(type))
                    {
                        GetOrCreateAndAddSystem(managedCreateType.MakeGenericType(type));
                        GetOrCreateAndAddSystem(managedDestroyType.MakeGenericType(type));
                        sysTypes.TryAdd(ComponentType.ReadOnly(managedSysStateType.MakeGenericType(type)), 0);
                        //tagTypes.Add(ComponentType.ReadOnly(managedTagType.MakeGenericType(type)));
                        tagTypes.Add(ComponentType.ReadOnly((Activator.CreateInstance(type) as IManagedComponent).AssociatedComponentType));
                    }
                    else if (typeof(ICollectionComponent).IsAssignableFrom(type))
                    {
                        GetOrCreateAndAddSystem(collectionCreateType.MakeGenericType(type));
                        GetOrCreateAndAddSystem(collectionDestroyType.MakeGenericType(type));
                        sysTypes.TryAdd(ComponentType.ReadOnly(collectionSysStateType.MakeGenericType(type)), 0);
                        //tagTypes.Add(ComponentType.ReadOnly(collectionTagType.MakeGenericType(type)));
                        tagTypes.Add(ComponentType.ReadOnly((Activator.CreateInstance(type) as ICollectionComponent).AssociatedComponentType));
                    }
                }
            }

            var tags = tagTypes.ToArray();
            var nss  = sysTypes.GetKeyArray(Allocator.Temp);
            var ss   = nss.ToArray();
            nss.Dispose();  //Todo: Is this necessary? I keep getting conflicting info from Unity on Allocator.Temp

            EntityQueryDesc descCreate = new EntityQueryDesc
            {
                Any  = tags,
                None = ss
            };
            m_anythingNeedsCreationQuery = GetEntityQuery(descCreate);
            EntityQueryDesc descDestroy  = new EntityQueryDesc
            {
                Any  = ss,
                None = tags
            };
            m_anythingNeedsDestructionQuery = GetEntityQuery(descDestroy);

            tagTypes.Dispose();
            sysTypes.Dispose();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            latiosWorld.CollectionComponentStorage.Dispose();
        }
    }

    internal class ManagedComponentCreateSystem<T> : SubSystem where T : struct, IManagedComponent
    {
        private EntityQuery m_query;

        protected override void OnCreate()
        {
            m_query = GetEntityQuery(ComponentType.Exclude<ManagedComponentSystemStateTag<T> >(), ComponentType.ReadOnly(new T().AssociatedComponentType));
        }

        protected override void OnUpdate()
        {
            var entities = m_query.ToEntityArray(Allocator.TempJob);
            foreach (var e in entities)
            {
                EntityManager.AddManagedComponent(e, new T());
            }
            entities.Dispose();
        }
    }

    internal class ManagedComponentDestroySystem<T> : SubSystem where T : struct, IManagedComponent
    {
        private EntityQuery m_query;

        protected override void OnCreate()
        {
            m_query = GetEntityQuery(ComponentType.ReadOnly<ManagedComponentSystemStateTag<T> >(), ComponentType.Exclude(new T().AssociatedComponentType));
        }

        protected override void OnUpdate()
        {
            var entities = m_query.ToEntityArray(Allocator.TempJob);
            foreach(var e in entities)
            {
                EntityManager.RemoveManagedComponent<T>(e);
            }
            entities.Dispose();
        }
    }

    internal class CollectionComponentCreateSystem<T> : SubSystem where T : struct, ICollectionComponent
    {
        private EntityQuery m_query;

        protected override void OnCreate()
        {
            m_query = GetEntityQuery(ComponentType.Exclude<CollectionComponentSystemStateTag<T> >(), ComponentType.ReadOnly(new T().AssociatedComponentType));
        }

        protected override void OnUpdate()
        {
            var entities = m_query.ToEntityArray(Allocator.TempJob);
            foreach (var e in entities)
            {
                EntityManager.AddCollectionComponent(e, new T(), false);
            }
            entities.Dispose();
        }
    }

    internal class CollectionComponentDestroySystem<T> : SubSystem where T : struct, ICollectionComponent
    {
        private EntityQuery m_query;

        protected override void OnCreate()
        {
            m_query = GetEntityQuery(ComponentType.ReadOnly<CollectionComponentSystemStateTag<T> >(), ComponentType.Exclude(new T().AssociatedComponentType));
        }

        protected override void OnUpdate()
        {
            var entities = m_query.ToEntityArray(Allocator.TempJob);
            foreach (var e in entities)
            {
                EntityManager.RemoveCollectionComponentAndDispose<T>(e);
            }
            entities.Dispose();
        }
    }
}

