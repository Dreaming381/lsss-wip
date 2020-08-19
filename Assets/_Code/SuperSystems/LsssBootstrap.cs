using System;
using System.Collections.Generic;
using Latios;
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

        var initializationSystemGroup = world.GetExistingSystem<InitializationSystemGroup>();
        var simulationSystemGroup     = world.GetExistingSystem<SimulationSystemGroup>();
        var presentationSystemGroup   = world.GetExistingSystem<PresentationSystemGroup>();
        var systems                   = new List<Type>(DefaultWorldInitialization.GetAllSystems(WorldSystemFilterFlags.Default));

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
        var beginProfiling     = world.CreateSystem<Lsss.Tools.BeginFrameProfilingSystem>();
        var beforeGpuProfiling = world.CreateSystem<Lsss.Tools.BeginGpuWaitProfilingSystem>();
        var afterGpuProfiling  = world.CreateSystem<Lsss.Tools.EndGpuWaitProfilingSystem>();
        var loop               = PlayerLoop.GetCurrentPlayerLoop();
        ScriptBehaviourUpdateOrder.AppendSystemToPlayerLoopList(beginProfiling, ref loop, typeof(Initialization));
        PlayerLoop.SetPlayerLoop(loop);
        BootstrapTools.AddWorldToCurrentPlayerLoopWithDelayedSimulation(world);
        loop = PlayerLoop.GetCurrentPlayerLoop();

#if UNITY_EDITOR
        ScriptBehaviourUpdateOrder.AppendSystemToPlayerLoopList(beforeGpuProfiling, ref loop, typeof(PostLateUpdate));
#else
        ScriptBehaviourUpdateOrder.AppendSystemToPlayerLoopList(beforeGpuProfiling, ref loop, typeof(UnityEngine.PlayerLoop.PostLateUpdate.PlayerEmitCanvasGeometry));
#endif

        PlayerLoop.SetPlayerLoop(loop);
        return true;
    }
}

