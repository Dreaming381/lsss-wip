using Latios;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace Lsss
{
    public class DestroyShipsWithNoHealthSystem : SubSystem
    {
        BeginInitializationEntityCommandBufferSystem m_ecbSystem;

        protected override void OnCreate()
        {
            m_ecbSystem = World.GetExistingSystem<BeginInitializationEntityCommandBufferSystem>();
        }

        protected override void OnUpdate()
        {
            var   ecbPackage = m_ecbSystem.CreateCommandBuffer();
            var   ecb        = ecbPackage.ToConcurrent();
            float dt         = Time.DeltaTime;

            Entities.WithChangeFilter<ShipHealth>().ForEach((Entity entity,
                                                             int entityInQueryIndex,
                                                             in ShipHealth health,
                                                             in ShipExplosionPrefab explosionPrefab,
                                                             in LocalToWorld ltw) =>
            {
                if (health.health <= 0f)
                {
                    ecb.DestroyEntity(entityInQueryIndex, entity);

                    var explosion                                                           = ecb.Instantiate(entityInQueryIndex, explosionPrefab.explosionPrefab);
                    ecb.SetComponent(entityInQueryIndex, explosion, new Translation { Value = ltw.Position });
                }
            }).ScheduleParallel();

            m_ecbSystem.AddJobHandleForProducer(Dependency);
        }
    }
}

