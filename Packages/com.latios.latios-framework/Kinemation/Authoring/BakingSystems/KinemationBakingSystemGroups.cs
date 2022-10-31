using Latios.Authoring.Systems;
using Unity.Entities;

namespace Latios.Kinemation.Authoring.Systems
{
    [WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
    [UpdateInGroup(typeof(SmartBlobberBakingGroup))]
    public class KinemationSmartBlobberBakingGroup : ComponentSystemGroup
    {
        void GetOrCreateAndAddSystem<T>() where T : unmanaged, ISystem
        {
            AddSystemToUpdateList(World.GetOrCreateSystem<T>());
        }

        void GetOrCreateAndAddManagedSystem<T>() where T : ComponentSystemBase
        {
            AddSystemToUpdateList(World.GetOrCreateSystemManaged<T>());
        }

        protected override void OnCreate()
        {
            base.OnCreate();

            GetOrCreateAndAddManagedSystem<CreateShadowHierarchiesSystem>();  // sync
            GetOrCreateAndAddManagedSystem<GatherBindingPathsFromShadowHierarchySystem>();  // sync
            GetOrCreateAndAddManagedSystem<PruneShadowHierarchiesSystem>();  // sync
            GetOrCreateAndAddManagedSystem<BuildOptimizedBoneToRootSystem>();  // sync
            GetOrCreateAndAddManagedSystem<AssignExportedBoneIndicesSystem>();  // sync
            GetOrCreateAndAddManagedSystem<GatherOptimizedHierarchyFromShadowHierarchySystem>();  // sync -> async

            GetOrCreateAndAddSystem<BindSkinnedMeshesToSkeletonsSystem>();  // async -> sync
            GetOrCreateAndAddManagedSystem<MeshSkinningSmartBlobberSystem>();  // sync -> async
            GetOrCreateAndAddSystem<FindExposedBonesBakingSystem>();  // async -> sync
            GetOrCreateAndAddManagedSystem<SkeletonClipSetSmartBlobberSystem>();  // sync -> async

            GetOrCreateAndAddSystem<MeshPathsSmartBlobberSystem>();  // async
            GetOrCreateAndAddSystem<SkeletonPathsSmartBlobberSystem>();  // async
            GetOrCreateAndAddSystem<SkeletonHierarchySmartBlobberSystem>();  // async

            GetOrCreateAndAddSystem<SetupExportedBonesSystem>();  // async -> sync
            GetOrCreateAndAddManagedSystem<DestroyShadowHierarchiesSystem>();  // sync
        }
    }

    [WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
    [UpdateInGroup(typeof(SmartBakerBakingGroup))]
    public class KinemationSmartBlobberResolverBakingGroup : ComponentSystemGroup
    {
        void GetOrCreateAndAddSystem<T>() where T : unmanaged, ISystem
        {
            AddSystemToUpdateList(World.GetOrCreateSystem<T>());
        }

        void GetOrCreateAndAddManagedSystem<T>() where T : ComponentSystemBase
        {
            AddSystemToUpdateList(World.GetOrCreateSystemManaged<T>());
        }

        protected override void OnCreate()
        {
            base.OnCreate();

            GetOrCreateAndAddSystem<ResolveSkeletonAndSkinnedMeshBlobsSystem>();  // async
        }
    }
}

