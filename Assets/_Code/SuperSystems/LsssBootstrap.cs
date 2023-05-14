using System;
using System.Collections.Generic;
using Latios;
using Latios.Authoring;
using Latios.Systems;
using Unity.Collections;
using Unity.Entities;
using UnityEngine.LowLevel;
using UnityEngine.PlayerLoop;

[UnityEngine.Scripting.Preserve]
public class LatiosBakingBootstrap : ICustomBakingBootstrap
{
    public void InitializeBakingForAllWorlds(ref CustomBakingBootstrapContext context)
    {
        var systems = DefaultWorldInitialization.GetAllSystems(WorldSystemFilterFlags.BakingSystem);
        foreach (var system in systems)
        {
            if (system == typeof(Lsss.FireGunsSystem))
                UnityEngine.Debug.Log("Why? How? TypeManager corruption?");
        }

        Latios.Transforms.Authoring.TransformsBakingBootstrap.InstallLatiosTransformsBakers(ref context);
        Latios.Psyshock.Authoring.PsyshockBakingBootstrap.InstallUnityColliderBakers(ref context);
        Latios.Kinemation.Authoring.KinemationBakingBootstrap.InstallKinemationBakersAndSystems(ref context);
    }
}

[UnityEngine.Scripting.Preserve]
public class LatiosEditorBootstrap : ICustomEditorBootstrap
{
    public World InitializeOrModify(World defaultEditorWorld)
    {
        var world = new LatiosWorld(defaultEditorWorld.Name);

        var systems = DefaultWorldInitialization.GetAllSystemTypeIndices(WorldSystemFilterFlags.Default, true);

        BootstrapTools.InjectSystems(systems, world, world.simulationSystemGroup);

        Latios.Transforms.TransformsBootstrap.InstallTransforms(world, world.simulationSystemGroup);
        Latios.Kinemation.KinemationBootstrap.InstallKinemation(world);

        world.initializationSystemGroup.SortSystems();
        world.simulationSystemGroup.SortSystems();
        world.presentationSystemGroup.SortSystems();

        return world;
    }
}

[UnityEngine.Scripting.Preserve]
public class LatiosBootstrap : ICustomBootstrap
{
    public bool Initialize(string defaultWorldName)
    {
        var world                             = new LatiosWorld(defaultWorldName);
        World.DefaultGameObjectInjectionWorld = world;
        world.useExplicitSystemOrdering       = true;
        world.zeroToleranceForExceptions      = true;

        var systems = DefaultWorldInitialization.GetAllSystemTypeIndices(WorldSystemFilterFlags.Default);

        BootstrapTools.InjectUnitySystems(systems, world, world.simulationSystemGroup);

        CoreBootstrap.InstallSceneManager(world);
        Latios.Transforms.TransformsBootstrap.InstallTransforms(world, world.simulationSystemGroup, true);
        Latios.Myri.MyriBootstrap.InstallMyri(world);
        Latios.Kinemation.KinemationBootstrap.InstallKinemation(world);

        BootstrapTools.InjectRootSuperSystems(systems, world, world.simulationSystemGroup);

        world.initializationSystemGroup.SortSystems();
        world.simulationSystemGroup.SortSystems();
        world.presentationSystemGroup.SortSystems();

        //Reset playerloop so we don't infinitely add systems.
        PlayerLoop.SetPlayerLoop(PlayerLoop.GetDefaultPlayerLoop());
        //var beforeGpuProfiling = world.CreateSystem<Lsss.Tools.BeginGpuWaitProfilingSystem>();
        //var afterGpuProfiling  = world.CreateSystem<Lsss.Tools.EndGpuWaitProfilingSystem>();

        BootstrapTools.AddWorldToCurrentPlayerLoopWithDelayedSimulation(world);
        var loop = PlayerLoop.GetCurrentPlayerLoop();

#if UNITY_EDITOR
        //ScriptBehaviourUpdateOrder.AppendSystemToPlayerLoop(beforeGpuProfiling, ref loop, typeof(PostLateUpdate));
#else
        //ScriptBehaviourUpdateOrder.AppendSystemToPlayerLoop(beforeGpuProfiling, ref loop, typeof(UnityEngine.PlayerLoop.PostLateUpdate.PlayerEmitCanvasGeometry));
#endif

        PlayerLoop.SetPlayerLoop(loop);
        //world.EntityManager.AddComponent<Latios.DontDestroyOnSceneChangeTag>(world.EntityManager.UniversalQuery);
        return true;
    }
}

