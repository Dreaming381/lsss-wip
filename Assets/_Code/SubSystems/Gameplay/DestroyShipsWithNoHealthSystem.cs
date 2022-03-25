using Latios;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace Lsss
{
    public partial class DestroyShipsWithNoHealthSystem : SubSystem
    {
        EntityQuery m_query;

        protected override void OnCreate()
        {
            m_query = Fluent.WithAll<ShipHealth>(true).WithAll<ShipExplosionPrefab>(true).WithAll<LocalToWorld>(true).Build();
        }

        protected override void OnUpdate()
        {
            var   icb = latiosWorld.syncPoint.CreateInstantiateCommandBuffer<Translation>().AsParallelWriter();
            var   dcb = latiosWorld.syncPoint.CreateDestroyCommandBuffer().AsParallelWriter();
            float dt  = Time.DeltaTime;

            // The below seems to cause a sync point in Entities 0.50. So we use IJobEntityBatch instead.

            //Entities.WithChangeFilter<ShipHealth>().ForEach((Entity entity,
            //                                                 int entityInQueryIndex,
            //                                                 in ShipHealth health,
            //                                                 in ShipExplosionPrefab explosionPrefab,
            //                                                 in LocalToWorld ltw) =>
            //{
            //    if (health.health <= 0f)
            //    {
            //        dcb.Add(entity, entityInQueryIndex);
            //        if (explosionPrefab.explosionPrefab != Entity.Null)
            //            icb.Add(explosionPrefab.explosionPrefab, new Translation { Value = ltw.Position }, entityInQueryIndex);
            //    }
            //}).ScheduleParallel();

            Dependency = new DestroyJob
            {
                entityHandle          = GetEntityTypeHandle(),
                healthHandle          = GetComponentTypeHandle<ShipHealth>(true),
                explosionPrefabHandle = GetComponentTypeHandle<ShipExplosionPrefab>(true),
                ltwHandle             = GetComponentTypeHandle<LocalToWorld>(true),
                icb                   = icb,
                dcb                   = dcb,
                dt                    = dt,
                lastSystemVersion     = LastSystemVersion
            }.ScheduleParallel(m_query, Dependency);
        }

        [BurstCompile]
        struct DestroyJob : IJobEntityBatch
        {
            [ReadOnly] public EntityTypeHandle                         entityHandle;
            [ReadOnly] public ComponentTypeHandle<ShipHealth>          healthHandle;
            [ReadOnly] public ComponentTypeHandle<ShipExplosionPrefab> explosionPrefabHandle;
            [ReadOnly] public ComponentTypeHandle<LocalToWorld>        ltwHandle;

            public InstantiateCommandBuffer<Translation>.ParallelWriter icb;
            public DestroyCommandBuffer.ParallelWriter                  dcb;

            public float dt;
            public uint  lastSystemVersion;

            public void Execute(ArchetypeChunk batchInChunk, int batchIndex)
            {
                if (!batchInChunk.DidChange(healthHandle, lastSystemVersion))
                    return;

                var entities         = batchInChunk.GetNativeArray(entityHandle);
                var healths          = batchInChunk.GetNativeArray(healthHandle);
                var explosionPrefabs = batchInChunk.GetNativeArray(explosionPrefabHandle);
                var ltws             = batchInChunk.GetNativeArray(ltwHandle);

                for (int i = 0; i < batchInChunk.Count; i++)
                {
                    if (healths[i].health <= 0f)
                    {
                        dcb.Add(entities[i], batchIndex);
                        if (explosionPrefabs[i].explosionPrefab != Entity.Null)
                            icb.Add(explosionPrefabs[i].explosionPrefab, new Translation { Value = ltws[i].Position }, batchIndex);
                    }
                }
            }
        }
    }
}

