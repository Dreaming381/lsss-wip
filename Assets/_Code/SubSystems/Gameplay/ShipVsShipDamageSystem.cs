using System.Collections.Generic;
using Latios;
using Latios.Psyshock;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace Lsss
{
    public class ShipVsShipDamageSystem : SubSystem
    {
        private List<CollisionLayer> m_layers = new List<CollisionLayer>();

        protected override void OnUpdate()
        {
            var backup = Dependency;
            Dependency = default;

            m_layers.Clear();
            Entities.WithAll<FactionTag>().ForEach((Entity entity, int entityInQueryIndex) =>
            {
                if (entityInQueryIndex == 0)
                    Dependency = backup;

                var layer = EntityManager.GetCollectionComponent<FactionShipsCollisionLayer>(entity, true);
                m_layers.Add(layer.layer);
            }).WithoutBurst().Run();

            var processor = new DamageCollidingShipsProcessor
            {
                shipHealthCdfe = GetComponentDataFromEntity<ShipHealth>(),
                shipDamageCdfe = GetComponentDataFromEntity<Damage>()
            };

            foreach (var layer in m_layers)
            {
                Dependency = Physics.FindPairs(layer, processor).ScheduleParallel(Dependency);
            }

            for (int i = 0; i < m_layers.Count - 1; i++)
            {
                for (int j = i + 1; j < m_layers.Count; j++)
                {
                    Dependency = Physics.FindPairs(m_layers[i], m_layers[j], processor).ScheduleParallel(Dependency);
                }
            }
        }

        struct DamageCollidingShipsProcessor : IFindPairsProcessor
        {
            public PhysicsComponentDataFromEntity<ShipHealth> shipHealthCdfe;
            [ReadOnly] public ComponentDataFromEntity<Damage> shipDamageCdfe;

            public void Execute(FindPairsResult result)
            {
                if (Physics.DistanceBetween(result.bodyA.collider, result.bodyA.transform, result.bodyB.collider, result.bodyB.transform, 0f, out _))
                {
                    var healthA = shipHealthCdfe[result.entityA];
                    var healthB = shipHealthCdfe[result.entityB];
                    var damageA = shipDamageCdfe[result.entityA];
                    var damageB = shipDamageCdfe[result.entityB];

                    healthA.health -= damageB.damage;
                    healthB.health -= damageA.damage;

                    shipHealthCdfe[result.entityA] = healthA;
                    shipHealthCdfe[result.entityB] = healthB;
                }
            }
        }
    }
}

