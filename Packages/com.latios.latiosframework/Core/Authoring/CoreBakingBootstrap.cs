using Latios.Authoring.Systems;
using Unity.Entities;

namespace Latios.Authoring
{
    /// <summary>
    /// Static class containing installers for optional authoring time features in the Core module
    /// </summary>
    public static class CoreBakingBootstrap
    {
        /// <summary>
        /// Forces LinkedEntityGroup to be removed when the buffer length is 1, as this is redundant
        /// and causes unnecessary heap allocations beginning in Entities 1.3.0-exp.1
        /// </summary>
        /// <param name="context">The custom context passed into ICustomBakingBootstrap</param>
        public static void ForceRemoveLinkedEntityGroupsOfLength1(ref CustomBakingBootstrapContext context)
        {
            context.bakingSystemTypesToInject.Add(TypeManager.GetSystemTypeIndex<RemoveBadLinkedEntityGroupBakingSystem>());
        }

        /// <summary>
        /// Installs the mechanism that allows ticking to be baked into the simulation.
        /// </summary>
        /// <param name="context"></param>
        public static void InstallTicking(ref CustomBakingBootstrapContext context)
        {
            context.filteredBakerTypes.Add(typeof(TickedEntityAuthoringBaker));
            context.optimizationSystemTypesToInject.Add(TypeManager.GetSystemTypeIndex<Latios.Systems.TickedArchetypeCorrectionSystemGroup>());
        }
    }
}

