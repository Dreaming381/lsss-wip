using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Rendering;

namespace Latios.Kinemation.Systems
{
    [DisableAutoCreation]
    [BurstCompile]
    public partial struct FrustumCullUnskinnedEntitiesSystem : ISystem
    {
        EntityQuery m_metaQuery;

        public void OnCreate(ref SystemState state)
        {
            m_metaQuery = state.Fluent().WithAll<ChunkWorldRenderBounds>(true).WithAll<HybridChunkInfo>(true).WithAll<ChunkHeader>(true).WithAll<ChunkPerFrameCullingMask>(true)
                          .WithAll<ChunkPerCameraCullingMask>(false).UseWriteGroups().Build();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var planesBuffer = state.GetWorldBlackboardEntity().GetBuffer<CullingPlane>(true);
            var unmanaged    = state.WorldUnmanaged;
            var planes       = CullingUtilities.BuildSOAPlanePackets(planesBuffer, ref unmanaged);

            state.Dependency = new SimpleCullingJob
            {
                cullingPlanes         = planes,
                boundsHandle          = state.GetComponentTypeHandle<WorldRenderBounds>(true),
                hybridChunkInfoHandle = state.GetComponentTypeHandle<HybridChunkInfo>(true),
                chunkHeaderHandle     = state.GetComponentTypeHandle<ChunkHeader>(true),
                chunkBoundsHandle     = state.GetComponentTypeHandle<ChunkWorldRenderBounds>(true),
                chunkMaskHandle       = state.GetComponentTypeHandle<ChunkPerCameraCullingMask>(false)
            }.ScheduleParallel(m_metaQuery, state.Dependency);
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state) {
        }

        [BurstCompile]
        unsafe struct SimpleCullingJob : IJobEntityBatch
        {
            [ReadOnly] public NativeArray<FrustumPlanes.PlanePacket4> cullingPlanes;

            [ReadOnly] public ComponentTypeHandle<WorldRenderBounds>      boundsHandle;
            [ReadOnly] public ComponentTypeHandle<HybridChunkInfo>        hybridChunkInfoHandle;
            [ReadOnly] public ComponentTypeHandle<ChunkHeader>            chunkHeaderHandle;
            [ReadOnly] public ComponentTypeHandle<ChunkWorldRenderBounds> chunkBoundsHandle;
            public ComponentTypeHandle<ChunkPerCameraCullingMask>         chunkMaskHandle;

            public void Execute(ArchetypeChunk archetypeChunk, int chunkIndex)
            {
                var hybridChunkInfoArray = archetypeChunk.GetNativeArray(hybridChunkInfoHandle);
                var chunkHeaderArray     = archetypeChunk.GetNativeArray(chunkHeaderHandle);
                var chunkBoundsArray     = archetypeChunk.GetNativeArray(chunkBoundsHandle);
                var chunkMaskArray       = archetypeChunk.GetNativeArray(chunkMaskHandle);

                for (var metaIndex = 0; metaIndex < archetypeChunk.Count; metaIndex++)
                {
                    var hybridChunkInfo = hybridChunkInfoArray[metaIndex];
                    if (!hybridChunkInfo.Valid)
                        continue;

                    var chunkHeader = chunkHeaderArray[metaIndex];
                    var chunkBounds = chunkBoundsArray[metaIndex];

                    ref var chunkCullingData = ref hybridChunkInfo.CullingData;

                    var chunkInstanceCount    = chunkHeader.ArchetypeChunk.Count;
                    var chunkEntityLodEnabled = chunkCullingData.InstanceLodEnableds;
                    var anyLodEnabled         = (chunkEntityLodEnabled.Enabled[0] | chunkEntityLodEnabled.Enabled[1]) != 0;

                    if (anyLodEnabled)
                    {
                        var perInstanceCull = 0 != (chunkCullingData.Flags & HybridChunkCullingData.kFlagInstanceCulling);

                        var chunkIn = perInstanceCull ?
                                      FrustumPlanes.Intersect2(cullingPlanes, chunkBounds.Value) :
                                      FrustumPlanes.Intersect2NoPartial(cullingPlanes, chunkBounds.Value);

                        if (chunkIn == FrustumPlanes.IntersectResult.Partial)
                        {
                            var chunk = chunkHeader.ArchetypeChunk;

                            var chunkInstanceBounds = chunk.GetNativeArray(boundsHandle);

                            var        lodWord = chunkEntityLodEnabled.Enabled[0];
                            BitField64 maskWordLower;
                            maskWordLower.Value = 0;
                            for (int i = math.tzcnt(lodWord); i < 64; lodWord ^= 1ul << i, i = math.tzcnt(lodWord))
                            {
                                bool isIn            = FrustumPlanes.Intersect2NoPartial(cullingPlanes, chunkInstanceBounds[i].Value) != FrustumPlanes.IntersectResult.Out;
                                maskWordLower.Value |= math.select(0ul, 1ul, isIn) << i;
                            }
                            lodWord = chunkEntityLodEnabled.Enabled[1];
                            BitField64 maskWordUpper;
                            maskWordUpper.Value = 0;
                            for (int i = math.tzcnt(lodWord); i < 64; lodWord ^= 1ul << i, i = math.tzcnt(lodWord))
                            {
                                bool isIn            = FrustumPlanes.Intersect2NoPartial(cullingPlanes, chunkInstanceBounds[i + 64].Value) != FrustumPlanes.IntersectResult.Out;
                                maskWordUpper.Value |= math.select(0ul, 1ul, isIn) << i;
                            }

                            chunkMaskArray[metaIndex] = new ChunkPerCameraCullingMask { lower = maskWordLower, upper = maskWordUpper };
                        }
                        else if (chunkIn == FrustumPlanes.IntersectResult.In)
                        {
                            var chunk = chunkHeader.ArchetypeChunk;

                            BitField64 lodWordLower   = new BitField64(chunkEntityLodEnabled.Enabled[0]);
                            BitField64 lodWordUpper   = new BitField64(chunkEntityLodEnabled.Enabled[1]);
                            chunkMaskArray[metaIndex] = new ChunkPerCameraCullingMask { lower = lodWordLower, upper = lodWordUpper };
                        }
                    }
                }
            }
        }
    }
}

