using System.Collections.Generic;
using Latios;
using Latios.Psyshock;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

using static Unity.Entities.SystemAPI;

namespace Lsss
{
    [BurstCompile]
    public partial struct ShipVsShipDamageSystem : ISystem
    {
        LatiosWorldUnmanaged latiosWorld;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            latiosWorld = state.GetLatiosWorldUnmanaged();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var shipLayer = latiosWorld.sceneBlackboardEntity.GetCollectionComponent<ShipsCollisionLayer>(true).layer;

            var processor = new DamageCollidingShipsProcessor
            {
                shipHealthLookup = GetComponentLookup<ShipHealth>(),
                shipDamageLookup = GetComponentLookup<Damage>()
            };
            state.Dependency = Physics.FindPairs(shipLayer, processor).ScheduleParallel(state.Dependency);
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

