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
    public partial struct ShipVsWallDamageSystem : ISystem, ILatiosApi
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
            var wallLayer = api.sceneBlackboardEntity.GetCollectionComponent<WallCollisionLayer>(true).layer;

            var processor = new DamageHitShipsProcessor().Inject(api);

            state.Dependency = Physics.FindPairs(wallLayer, shipLayer, processor).ScheduleParallel(state.Dependency);
        }

        //Assumes A is wall and B is ship.
        partial struct DamageHitShipsProcessor : IFindPairsProcessor, IInjectable
        {
            [Inject] PhysicsComponentLookup<ShipHealth> shipHealthLookup;
            [ReadOnly, Inject] ComponentLookup<Damage>  wallDamageLookup;

            public void Execute(in FindPairsResult result)
            {
                if (Physics.AreOverlapping(result.bodyA.collider, result.bodyA.transform, result.bodyB.collider, result.bodyB.transform))
                {
                    var damage = wallDamageLookup[result.entityA];
                    var health = shipHealthLookup[result.entityB];

                    health.health -= damage.damage;

                    shipHealthLookup[result.entityB] = health;
                }
            }
        }
    }
}

