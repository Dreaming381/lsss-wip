using Latios;
using Latios.Transforms;
using Lsss.SuperSystems;
using Lsss.Tools;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace Lsss.Systems
{
    [UpdateInGroup(typeof(Latios.Systems.PreSyncPointGroup))]
    public partial class LsssPreSyncRootSuperSystem : RootSuperSystem
    {
        protected override void CreateSystems()
        {
            GetOrCreateAndAddManagedSystem<BeginFrameProfilingSystem>();
        }
    }

    [UpdateInGroup(typeof(Latios.Systems.LatiosWorldSyncGroup), OrderLast = true)]
    public partial class LsssInitializationRootSuperSystem : RootSuperSystem
    {
        protected override void CreateSystems()
        {
            GetOrCreateAndAddUnmanagedSystem<OrbitalSpawnersProcGenSystem>();
            GetOrCreateAndAddUnmanagedSystem<SpawnFleetsSystem>();
            GetOrCreateAndAddUnmanagedSystem<SpawnShipsEnqueueSystem>();
            GetOrCreateAndAddUnmanagedSystem<SpawnShipsEnableSystem>();
        }
    }
}

