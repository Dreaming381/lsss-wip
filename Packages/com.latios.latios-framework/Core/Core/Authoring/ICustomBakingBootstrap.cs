using System.Collections.Generic;
using System.Reflection;
using Unity.Burst;
using Unity.Entities;
using Unity.Entities.Exposed;

namespace Latios.Authoring
{
    /// <summary>
    /// Implement this interface in a bootstrap to customize the baking world similar to runtime ICustomBootstrap.
    /// </summary>
    public interface ICustomBakingBootstrap
    {
        /// <summary>
        /// This function behaves similarly to ICustomBootstrap in that you can customize the conversion process
        /// at startup. However, unlike ICustomBootstrap, this is only invoked once per domain load.
        /// </summary>
        void InitializeBakingForAllWorlds(ref CustomBakingBootstrapContext context);
    }

    public struct CustomBakingBootstrapContext
    {
        public List<System.Type> filteredBakerTypes;
        public List<System.Type> systemTypesToDisable;
        public List<System.Type> systemTypesToInject;
    }

    internal class BakingOverride
    {
        OverrideBakers                       m_overrideBakers;
        public List<ICreateSmartBakerSystem> m_smartBakerSystemCreators;
        public List<System.Type>             m_systemTypesToDisable;
        public List<System.Type>             m_systemTypesToInject;

        public BakingOverride()
        {
#if UNITY_EDITOR
            // Todo: We always do this regardless of if the bakers are used.
            // These bakers we create don't actually get added to the map.
            // And the systems don't do anything without the bakers.
            // Still, it would be nice to not have the systems created at all.
            m_smartBakerSystemCreators = new List<ICreateSmartBakerSystem>();
            foreach (var creatorType in UnityEditor.TypeCache.GetTypesDerivedFrom<ICreateSmartBakerSystem>())
            {
                if (creatorType.IsAbstract || creatorType.ContainsGenericParameters)
                    continue;

                m_smartBakerSystemCreators.Add(System.Activator.CreateInstance(creatorType) as ICreateSmartBakerSystem);
            }

            IEnumerable<System.Type> bootstrapTypes;

            bootstrapTypes = UnityEditor.TypeCache.GetTypesDerivedFrom(typeof(ICustomBakingBootstrap));

            System.Type selectedType = null;

            foreach (var bootType in bootstrapTypes)
            {
                if (bootType.IsAbstract || bootType.ContainsGenericParameters)
                    continue;

                if (selectedType == null)
                    selectedType = bootType;
                else if (selectedType.IsAssignableFrom(bootType))
                    selectedType = bootType;
                else if (!bootType.IsAssignableFrom(selectedType))
                    UnityEngine.Debug.LogError("Multiple custom ICustomConversionBootstrap exist in the project, ignoring " + bootType);
            }
            if (selectedType == null)
                return;

            ICustomBakingBootstrap bootstrap = System.Activator.CreateInstance(selectedType) as ICustomBakingBootstrap;

            var candidateBakers = new List<System.Type>();

            foreach (var type in UnityEditor.TypeCache.GetTypesDerivedFrom(typeof(Baker<>)))
            {
                if (!type.IsAbstract && !type.IsDefined(typeof(DisableAutoCreationAttribute)))
                {
                    candidateBakers.Add(type);
                }
            }

            var context = new CustomBakingBootstrapContext
            {
                filteredBakerTypes   = candidateBakers,
                systemTypesToDisable = new List<System.Type>(),
                systemTypesToInject  = new List<System.Type>()
            };
            bootstrap.InitializeBakingForAllWorlds(ref context);

            m_overrideBakers = new OverrideBakers(true, context.filteredBakerTypes.ToArray());

            m_systemTypesToDisable = context.systemTypesToDisable;
            m_systemTypesToInject  = context.systemTypesToInject;
#endif
        }

        public void Shutdown() => m_overrideBakers.Dispose();
    }

    [WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
    [UpdateInGroup(typeof(PreBakingSystemGroup))]
    internal partial class BakingBootstrapSystem : SystemBase, IRateManager
    {
        static BakingOverride s_bakingOverride;

        public float Timestep { get; set; }

        protected override void OnCreate()
        {
            if (s_bakingOverride == null)
            {
                s_bakingOverride = new BakingOverride();
            }

            World.GetExistingSystemManaged<PreBakingSystemGroup>().RateManager = this;
        }

        bool m_initialized = false;
        bool m_ranOnce     = false;
        protected override void OnUpdate()
        {
            m_ranOnce = true;
        }

        public bool ShouldGroupUpdate(ComponentSystemGroup group)
        {
            if (!m_initialized)
            {
                var smartBakingSystemGroup = World.GetOrCreateSystemManaged<Systems.SmartBakerBakingGroup>();
                foreach (var creator in s_bakingOverride.m_smartBakerSystemCreators)
                    creator.Create(World, smartBakingSystemGroup);

                foreach (var disableType in s_bakingOverride.m_systemTypesToDisable)
                {
                    var handle = World.GetExistingSystem(disableType);
                    if (handle != SystemHandle.Null)
                    {
                        World.Unmanaged.ResolveSystemStateRef(handle).Enabled = false;
                    }
                }

                BootstrapTools.InjectSystems(s_bakingOverride.m_systemTypesToInject, World, World.GetExistingSystemManaged<BakingSystemGroup>());

                m_initialized = true;
            }

            var result = !m_ranOnce;
            m_ranOnce  = false;
            return result;
        }
    }
}

