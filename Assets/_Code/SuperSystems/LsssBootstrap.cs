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
public class LatiosConversionBootstrap : ICustomConversionBootstrap
{
    public bool InitializeConversion(World conversionWorldWithGroupsAndMappingSystems, CustomConversionSettings settings, ref List<Type> filteredSystems)
    {
        for (int i = 0; i < filteredSystems.Count; i++)
        {
            var type = filteredSystems[i];
            if (type.Name == "NameChangeSystem")
            {
                filteredSystems.RemoveAtSwapBack(i);
                i--;
            }
            else if (type.Name.Contains("Incremental"))
            {
                filteredSystems.RemoveAtSwapBack(i);
                i--;
            }
            //else if (type.Name == "TransformIncrementalConversionSystem")
            //{
            //    filteredSystems.RemoveAtSwapBack(i);
            //    i--;
            //}
            //else if (type.Name == "SceneSectionIncrementalConversionSystem")
            //{
            //    filteredSystems.RemoveAtSwapBack(i);
            //    i--;
            //}
        }

        var defaultGroup = conversionWorldWithGroupsAndMappingSystems.GetExistingSystemManaged<GameObjectConversionGroup>();
        BootstrapTools.InjectSystems(filteredSystems, conversionWorldWithGroupsAndMappingSystems, defaultGroup);

        Latios.Psyshock.Authoring.PsyshockConversionBootstrap.InstallLegacyColliderConversion(conversionWorldWithGroupsAndMappingSystems);
        //Latios.Kinemation.Authoring.KinemationConversionBootstrap.InstallKinemationConversion(conversionWorldWithGroupsAndMappingSystems);
        return true;
    }
}

public class LatiosBootstrap : ICustomBootstrap
{
    public bool Initialize(string defaultWorldName)
    {
        var world                             = new LatiosWorld(defaultWorldName);
        World.DefaultGameObjectInjectionWorld = world;
        world.useExplicitSystemOrdering       = true;

        var systems = new List<Type>(DefaultWorldInitialization.GetAllSystems(WorldSystemFilterFlags.Default));

        BootstrapTools.InjectUnitySystems(systems, world, world.simulationSystemGroup);
        BootstrapTools.InjectRootSuperSystems(systems, world, world.simulationSystemGroup);

        //world.GetExistingSystemManaged<Unity.Transforms.CopyInitialTransformFromGameObjectSystem>().Enabled = false;  // Leaks LocalToWorld query and generates ECB.

        CoreBootstrap.InstallSceneManager(world);
        CoreBootstrap.InstallExtremeTransforms(world);
        //CoreBootstrap.InstallImprovedTransforms(world);
        Latios.Myri.MyriBootstrap.InstallMyri(world);
        //Latios.Kinemation.KinemationBootstrap.InstallKinemation(world);

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
        return true;
    }
}

