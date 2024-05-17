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
    public partial struct ShipVsExplosionDamageSystem : ISystem
    {
        LatiosWorldUnmanaged latiosWorld;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            latiosWorld = state.GetLatiosWorldUnmanaged();
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var shipLayer      = latiosWorld.sceneBlackboardEntity.GetCollectionComponent<ShipsCollisionLayer>(true).layer;
            var explosionLayer = latiosWorld.sceneBlackboardEntity.GetCollectionComponent<ExplosionCollisionLayer>(true).layer;

            var processor = new DamageHitShipsProcessor
            {
                explosionDamageLookup = GetComponentLookup<Damage>(true),
                shipHealthLookup      = GetComponentLookup<ShipHealth>(),
            };

            state.Dependency = Physics.FindPairs(explosionLayer, shipLayer, processor).ScheduleParallel(state.Dependency);
        }

        //Assumes A is explosion and B is ship.
        struct DamageHitShipsProcessor : IFindPairsProcessor
        {
            public PhysicsComponentLookup<ShipHealth> shipHealthLookup;
            [ReadOnly] public ComponentLookup<Damage> explosionDamageLookup;

            public void Execute(in FindPairsResult result)
            {
                if (Physics.DistanceBetween(result.colliderA, result.transformA, result.colliderB, result.transformB, 0f, out _))
                {
                    var     damage = explosionDamageLookup[result.entityA];
                    ref var health = ref shipHealthLookup.GetRW(result.entityB).ValueRW;

                    health.health -= damage.damage;
                }
            }
        }
    }
}

