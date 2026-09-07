using Latios.Systems;
using Unity.Entities;
using Unity.Transforms;

namespace Latios
{
    /// <summary>
    /// Static class containing installers for optional runtime features in the Core module
    /// </summary>
    public static unsafe class CoreBootstrap
    {
        /// <summary>
        /// Installs the Scene Management features into the World
        /// </summary>
        /// <param name="world">The World where systems should be installed.</param>
        public static void InstallSceneManager(World world)
        {
            BootstrapTools.InjectSystem(TypeManager.GetSystemTypeIndex<SceneManagerSystem>(),                 world);
            BootstrapTools.InjectSystem(TypeManager.GetSystemTypeIndex<DestroyEntitiesOnSceneChangeSystem>(), world);
        }

        public struct LocalTickingConfiguration
        {
            /// <summary>
            /// The number of ticks to fill a second of realtime
            /// </summary>
            public float ticksPerSecond;
            /// <summary>
            /// If true, if the time difference between last frame and this frame crosses over into a new tick,
            /// then input for this frame starts at the start of the new tick, and the previous tick is not
            /// resimulated with new input. If this is false, the previous tick is rolled back
            /// </summary>
            public bool snapInputToNextTick;
        }

        /// <summary>
        /// Installs the Ticking mechanisms into the World for local (singleplayer) ticking
        /// </summary>
        /// <param name="world">The World where systems should be installed.</param>
        /// <param name="config">The configuration to control ticking</param>
        public static void InstallLocalTicking(World world, LocalTickingConfiguration config)
        {
            var setupSystemHandle = BootstrapTools.InjectSystem(TypeManager.GetSystemTypeIndex<TickedLocalSetupSystem>(),       world);
            BootstrapTools.InjectSystem(TypeManager.GetSystemTypeIndex<TickedLocalSuperSystem>(),       world);
            BootstrapTools.InjectSystem(TypeManager.GetSystemTypeIndex<TickedInterpolateSuperSystem>(), world);
            ref var setupSystem = ref world.Unmanaged.GetUnsafeSystemRef<TickedLocalSetupSystem>(setupSystemHandle.systemHandle);
            if (config.ticksPerSecond > 0f)
                setupSystem.tickDeltaTime = 1f / config.ticksPerSecond;
            setupSystem.snapInputToTick   = config.snapInputToNextTick;
        }

#if NETCODE_PROJECT
        /// <summary>
        /// When installed in the Editor World, this removes the Disabled component from prespawned ghosts, allowing you to see them.
        /// </summary>
        /// <param name="world">The World where systems should be installed.</param>
        public static void InstallNetCodePreSpawnEnableInEditorSystem(World world)
        {
            BootstrapTools.InjectSystem(TypeManager.GetSystemTypeIndex<Latios.Compatibility.UnityNetCode.Systems.EnablePreSpawnedGhostsInEditorSystem>(), world);
        }
#endif
    }
}

