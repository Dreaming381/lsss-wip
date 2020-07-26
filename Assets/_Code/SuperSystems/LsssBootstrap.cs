using System;
using System.Collections.Generic;
using Latios;
using Unity.Collections;
using Unity.Entities;

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

        BootstrapTools.AddWorldToCurrentPlayerLoopWithDelayedSimulation(world);
        return true;
    }
}

