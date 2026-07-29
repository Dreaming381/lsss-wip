using Latios;
using Latios.Psyshock;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace Lsss
{
    [BurstCompile]
    public partial struct ShipVsShipDamageSystem : ISystem, ILatiosApi
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            this.OnCreateForLatios(ref state);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var api       = this.GetApi(ref state);
            var shipLayer = api.sceneBlackboardEntity.GetCollectionComponent<ShipsCollisionLayer>(true).layer;

            var processor    = new DamageCollidingShipsProcessor().Inject(api);
            state.Dependency = Physics.FindPairs(shipLayer, processor).ScheduleParallel(state.Dependency);
        }

        partial struct DamageCollidingShipsProcessor : IFindPairsProcessor, IInjectable
        {
            [Inject] PhysicsComponentLookup<ShipHealth> shipHealthLookup;
            [ReadOnly, Inject] ComponentLookup<Damage>  shipDamageLookup;

            public void Execute(in FindPairsResult result)
            {
                if (Physics.AreOverlapping(result.colliderA, result.transformA, result.colliderB, result.transformB))
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

