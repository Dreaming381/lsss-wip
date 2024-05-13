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
                shipDamageLookup = GetComponentLookup<Damage>(true)
            };
            state.Dependency = Physics.FindPairs(shipLayer, processor).ScheduleParallel(state.Dependency);
        }

        struct DamageCollidingShipsProcessor : IFindPairsProcessor
        {
            public PhysicsComponentLookup<ShipHealth> shipHealthLookup;
            [ReadOnly] public ComponentLookup<Damage> shipDamageLookup;

            public void Execute(in FindPairsResult result)
            {
                if (Physics.DistanceBetween(result.colliderA, result.transformA, result.colliderA, result.transformA, 0f, out _))
                {
                    ref var healthA = ref shipHealthLookup.GetRW(result.entityA).ValueRW;
                    ref var healthB = ref shipHealthLookup.GetRW(result.entityB).ValueRW;

                    var damageA = shipDamageLookup[result.entityA];
                    var damageB = shipDamageLookup[result.entityB];

                    healthA.health -= damageB.damage;
                    healthB.health -= damageA.damage;
                }
            }
        }
    }
}

