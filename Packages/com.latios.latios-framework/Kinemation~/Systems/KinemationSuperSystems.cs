using Unity.Entities;
using Unity.Rendering;

namespace Latios.Kinemation.Systems
{
    /// <summary>
    /// Subclass this class and add it to the world prior to installing Kinemation
    /// to customize the culling loop.
    /// </summary>
    [DisableAutoCreation]
    public class KinemationCullingSuperSystem : SuperSystem
    {
        protected override void CreateSystems()
        {
            EnableSystemSorting = false;

            GetOrCreateAndAddSystem<FrustumCullExposedSkeletonsSystem>();
            GetOrCreateAndAddUnmanagedSystem<FrustumCullOptimizedSkeletonsSystem>();
            GetOrCreateAndAddUnmanagedSystem<UpdateLODsSystem>();
            GetOrCreateAndAddUnmanagedSystem<FrustumCullSkinnedEntitiesSystem>();
            GetOrCreateAndAddUnmanagedSystem<AllocateDeformedMeshesSystem>();
            GetOrCreateAndAddUnmanagedSystem<AllocateLinearBlendMatricesSystem>();
            GetOrCreateAndAddSystem<SkinningDispatchSystem>();
            GetOrCreateAndAddUnmanagedSystem<FrustumCullUnskinnedEntitiesSystem>();
            GetOrCreateAndAddUnmanagedSystem<CopySkinWithCullingSystem>();
            GetOrCreateAndAddSystem<UploadMaterialPropertiesSystem>();
            GetOrCreateAndAddSystem<UpdateVisibilitiesSystem>();
        }
    }

    [UpdateInGroup(typeof(UpdatePresentationSystemGroup))]
    [UpdateAfter(typeof(RenderBoundsUpdateSystem))]
    [DisableAutoCreation]
    public class KinemationRenderUpdateSuperSystem : RootSuperSystem
    {
        protected override void CreateSystems()
        {
            GetOrCreateAndAddUnmanagedSystem<UpdateSkeletonBoundsSystem>();
            GetOrCreateAndAddUnmanagedSystem<UpdateSkinnedMeshChunkBoundsSystem>();
            GetOrCreateAndAddSystem<BeginPerFrameMeshSkinningBuffersUploadSystem>();
        }
    }

    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(LatiosHybridRendererSystem))]
    [DisableAutoCreation]
    public class KinemationPostRenderSuperSystem : SuperSystem
    {
        protected override void CreateSystems()
        {
            GetOrCreateAndAddSystem<EndPerFrameMeshSkinningBuffersUploadSystem>();
            GetOrCreateAndAddUnmanagedSystem<UpdateMatrixPreviousSystem>();
            GetOrCreateAndAddSystem<CombineExposedBonesSystem>();
            GetOrCreateAndAddUnmanagedSystem<ClearPerFrameCullingMasksSystem>();
            GetOrCreateAndAddUnmanagedSystem<UpdateChunkComputeDeformMetadataSystem>();
            GetOrCreateAndAddUnmanagedSystem<UpdateChunkLinearBlendMetadataSystem>();
            GetOrCreateAndAddSystem<ResetPerFrameSkinningMetadataJob>();
        }
    }

    [UpdateInGroup(typeof(StructuralChangePresentationSystemGroup))]
    [DisableAutoCreation]
    public class KinemationRenderSyncPointSuperSystem : SuperSystem
    {
        protected override void CreateSystems()
        {
            GetOrCreateAndAddUnmanagedSystem<AddMissingMatrixCacheSystem>();
            GetOrCreateAndAddUnmanagedSystem<AddMissingMasksSystem>();
            GetOrCreateAndAddSystem<SkeletonMeshBindingReactiveSystem>();
        }
    }

    [UpdateInGroup(typeof(Latios.Systems.LatiosWorldSyncGroup))]
    [DisableAutoCreation]
    public class KinemationFrameSyncPointSuperSystem : SuperSystem
    {
        protected override void CreateSystems()
        {
            GetOrCreateAndAddUnmanagedSystem<AddMissingMatrixCacheSystem>();
            GetOrCreateAndAddUnmanagedSystem<AddMissingMasksSystem>();
            GetOrCreateAndAddSystem<SkeletonMeshBindingReactiveSystem>();
        }
    }
}

