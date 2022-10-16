using System.Collections.Generic;
using Latios;
using Latios.Psyshock;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

using static Unity.Entities.SystemAPI;

namespace Lsss
{
    [BurstCompile]
    public partial struct ShipVsShipDamageSystem : ISystem
    {
        private NativeList<CollisionLayer> m_layers;

        LatiosWorldUnmanaged latiosWorld;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            m_layers = new NativeList<CollisionLayer>(Allocator.Persistent);

            latiosWorld = state.GetLatiosWorldUnmanaged();
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            m_layers.Dispose();
        }

        //[BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            m_layers.Clear();
            var factionEntities = QueryBuilder().WithAll<Faction, FactionTag>().Build().ToEntityArray(Allocator.Temp);
            foreach (var entity in factionEntities)
            {
                var layer = latiosWorld.GetCollectionComponent<FactionShipsCollisionLayer>(entity, true);
                m_layers.Add(layer.layer);
            }

            var processor = new DamageCollidingShipsProcessor
            {
                shipHealthLookup = GetComponentLookup<ShipHealth>(),
                shipDamageLookup = GetComponentLookup<Damage>()
            };

            foreach (var layer in m_layers)
            {
                state.Dependency = Physics.FindPairs(layer, processor).ScheduleParallel(state.Dependency);
            }

            for (int i = 0; i < m_layers.Length - 1; i++)
            {
                for (int j = i + 1; j < m_layers.Length; j++)
                {
                    state.Dependency = Physics.FindPairs(m_layers[i], m_layers[j], processor).ScheduleParallel(state.Dependency);
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

