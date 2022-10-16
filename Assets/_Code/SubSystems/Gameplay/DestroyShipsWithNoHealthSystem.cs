using Latios;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

using static Unity.Entities.SystemAPI;

namespace Lsss
{
    [BurstCompile]
    public partial struct DestroyShipsWithNoHealthSystem : ISystem
    {
        //EntityQuery m_query;

        LatiosWorldUnmanaged latiosWorld;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            latiosWorld = state.GetLatiosWorldUnmanaged();
            //m_query = QueryBuilder().WithAll<ShipHealth, ShipExplosionPrefab, LocalToWorld>().Build();
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var icb = latiosWorld.syncPoint.CreateInstantiateCommandBuffer<Translation>().AsParallelWriter();
            var dcb = latiosWorld.syncPoint.CreateDestroyCommandBuffer().AsParallelWriter();

            // The below seems to cause a sync point in Entities 0.50. So we use IJobEntityBatch instead.

            new Job { dcb = dcb, icb = icb }.ScheduleParallel();

            //Dependency = new DestroyJob
            //{
            //    entityHandle          = GetEntityTypeHandle(),
            //    healthHandle          = GetComponentTypeHandle<ShipHealth>(true),
            //    explosionPrefabHandle = GetComponentTypeHandle<ShipExplosionPrefab>(true),
            //    ltwHandle             = GetComponentTypeHandle<LocalToWorld>(true),
            //    icb                   = icb,
            //    dcb                   = dcb,
            //    lastSystemVersion     = LastSystemVersion
            //}.ScheduleParallel(m_query, Dependency);
        }

        [BurstCompile]
        [WithChangeFilter(typeof(ShipHealth))]
        partial struct Job : IJobEntity
        {
            public InstantiateCommandBuffer<Translation>.ParallelWriter icb;
            public DestroyCommandBuffer.ParallelWriter                  dcb;

            public void Execute(Entity entity,
                                [ChunkIndexInQuery] int chunkIndexInQuery,
                                in ShipHealth health,
                                in ShipExplosionPrefab explosionPrefab,
                                in LocalToWorld ltw)
            {
                if (health.health <= 0f)
                {
                    dcb.Add(entity, chunkIndexInQuery);
                    if (explosionPrefab.explosionPrefab != Entity.Null)
                        icb.Add(explosionPrefab.explosionPrefab, new Translation { Value = ltw.Position }, chunkIndexInQuery);
                }
            }
        }

        //[BurstCompile]
        //struct DestroyJob : IJobChunk
        //{
        //    [ReadOnly] public EntityTypeHandle                         entityHandle;
        //    [ReadOnly] public ComponentTypeHandle<ShipHealth>          healthHandle;
        //    [ReadOnly] public ComponentTypeHandle<ShipExplosionPrefab> explosionPrefabHandle;
        //    [ReadOnly] public ComponentTypeHandle<LocalToWorld>        ltwHandle;
        //
        //    public InstantiateCommandBuffer<Translation>.ParallelWriter icb;
        //    public DestroyCommandBuffer.ParallelWriter                  dcb;
        //
        //    public uint  lastSystemVersion;
        //
        //    public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        //    {
        //        if (!chunk.DidChange(healthHandle, lastSystemVersion))
        //            return;
        //
        //        var entities         = chunk.GetNativeArray(entityHandle);
        //        var healths          = chunk.GetNativeArray(healthHandle);
        //        var explosionPrefabs = chunk.GetNativeArray(explosionPrefabHandle);
        //        var ltws             = chunk.GetNativeArray(ltwHandle);
        //
        //        for (int i = 0; i < chunk.Count; i++)
        //        {
        //            if (healths[i].health <= 0f)
        //            {
        //                dcb.Add(entities[i], unfilteredChunkIndex);
        //                if (explosionPrefabs[i].explosionPrefab != Entity.Null)
        //                    icb.Add(explosionPrefabs[i].explosionPrefab, new Translation { Value = ltws[i].Position }, unfilteredChunkIndex);
        //            }
        //        }
        //    }
        //}
    }
}

