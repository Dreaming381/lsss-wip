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
    public partial class ShipVsShipDamageSystem : SubSystem
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
                shipHealthLookup = GetComponentLookup<ShipHealth>(),
                shipDamageLookup = GetComponentLookup<Damage>()
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
            public PhysicsComponentLookup<ShipHealth> shipHealthLookup;
            [ReadOnly] public ComponentLookup<Damage> shipDamageLookup;

            public void Execute(in FindPairsResult result)
            {
                //if (math.distance(result.bodyA.transform.pos, result.bodyB.transform.pos) > 250f)
                //    UnityEngine.Debug.LogWarning("Corrupted AABB pair");

                //var marker = new Unity.Profiling.ProfilerMarker("Process pair");
                //marker.Begin();
                if (Physics.DistanceBetween(result.bodyA.collider, result.bodyA.transform, result.bodyB.collider, result.bodyB.transform, 0f, out _))
                {
                    var healthA = shipHealthLookup[result.entityA];
                    var healthB = shipHealthLookup[result.entityB];
                    var damageA = shipDamageLookup[result.entityA];
                    var damageB = shipDamageLookup[result.entityB];

                    healthA.health -= damageB.damage;
                    healthB.health -= damageA.damage;

                    shipHealthLookup[result.entityA] = healthA;
                    shipHealthLookup[result.entityB] = healthB;
                }
                //marker.End();
            }
        }
    }
}

