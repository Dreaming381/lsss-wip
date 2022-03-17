using System;
using System.Collections.Generic;
using Latios;
using Latios.Systems;
using Unity.Collections;
using Unity.Entities;
using UnityEngine.LowLevel;
using UnityEngine.PlayerLoop;

public class LatiosBootstrap : ICustomBootstrap
{
    public bool Initialize(string defaultWorldName)
    {
        var world                             = new LatiosWorld(defaultWorldName);
        World.DefaultGameObjectInjectionWorld = world;
        world.useExplicitSystemOrdering       = true;

        var initializationSystemGroup = world.initializationSystemGroup;
        var simulationSystemGroup     = world.simulationSystemGroup;
        var presentationSystemGroup   = world.presentationSystemGroup;
        var systems                   = new List<Type>(DefaultWorldInitialization.GetAllSystems(WorldSystemFilterFlags.Default));

        systems.RemoveSwapBack(typeof(LatiosInitializationSystemGroup));
        systems.RemoveSwapBack(typeof(LatiosSimulationSystemGroup));
        systems.RemoveSwapBack(typeof(LatiosPresentationSystemGroup));
        systems.RemoveSwapBack(typeof(InitializationSystemGroup));
        systems.RemoveSwapBack(typeof(SimulationSystemGroup));
        systems.RemoveSwapBack(typeof(PresentationSystemGroup));

        BootstrapTools.InjectUnitySystems(systems, world, simulationSystemGroup);
        BootstrapTools.InjectRootSuperSystems(systems, world, simulationSystemGroup);

        initializationSystemGroup.SortSystems();
        simulationSystemGroup.SortSystems();
        presentationSystemGroup.SortSystems();

        //Reset playerloop so we don't infinitely add systems.
        PlayerLoop.SetPlayerLoop(PlayerLoop.GetDefaultPlayerLoop());
        var beforeGpuProfiling = world.CreateSystem<Lsss.Tools.BeginGpuWaitProfilingSystem>();
        var afterGpuProfiling  = world.CreateSystem<Lsss.Tools.EndGpuWaitProfilingSystem>();

        BootstrapTools.AddWorldToCurrentPlayerLoopWithDelayedSimulation(world);
        var loop = PlayerLoop.GetCurrentPlayerLoop();

#if UNITY_EDITOR
        ScriptBehaviourUpdateOrder.AppendSystemToPlayerLoop(beforeGpuProfiling, ref loop, typeof(PostLateUpdate));
#else
        ScriptBehaviourUpdateOrder.AppendSystemToPlayerLoop(beforeGpuProfiling, ref loop, typeof(UnityEngine.PlayerLoop.PostLateUpdate.PlayerEmitCanvasGeometry));
#endif

        PlayerLoop.SetPlayerLoop(loop);
        return true;
    }
}

