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
    public partial struct ShipVsExplosionDamageSystem : ISystem, ILatiosApi
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            this.OnCreateForLatios(ref state);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var api            = this.GetApi(ref state);
            var shipLayer      = api.sceneBlackboardEntity.GetCollectionComponent<ShipsCollisionLayer>(true).layer;
            var explosionLayer = api.sceneBlackboardEntity.GetCollectionComponent<ExplosionCollisionLayer>(true).layer;

            var processor = new DamageHitShipsProcessor().Inject(api);

            state.Dependency = Physics.FindPairs(explosionLayer, shipLayer, processor).ScheduleParallel(state.Dependency);
        }

        //Assumes A is explosion and B is ship.
        partial struct DamageHitShipsProcessor : IFindPairsProcessor, IInjectable
        {
            [Inject] PhysicsComponentLookup<ShipHealth> shipHealthLookup;
            [ReadOnly, Inject] ComponentLookup<Damage>  explosionDamageLookup;

            public void Execute(in FindPairsResult result)
            {
                if (Physics.AreOverlapping(result.colliderA, result.transformA, result.colliderB, result.transformB))
                {
                    var     damage = explosionDamageLookup[result.entityA];
                    ref var health = ref shipHealthLookup.GetRW(result.entityB).ValueRW;

                    health.health -= damage.damage;
                }
            }
        }
    }
}

