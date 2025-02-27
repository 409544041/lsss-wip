﻿using System;
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
        //throw new NotImplementedException();
    }
}

public class LatiosBootstrap : ICustomBootstrap
{
    public bool Initialize(string defaultWorldName)
    {
        var world                             = new LatiosWorld(defaultWorldName);
        World.DefaultGameObjectInjectionWorld = world;
        world.useExplicitSystemOrdering       = true;
        world.zeroToleranceForExceptions      = true;

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

