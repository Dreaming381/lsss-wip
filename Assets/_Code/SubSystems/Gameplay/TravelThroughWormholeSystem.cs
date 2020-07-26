using Latios;
using Latios.PhysicsEngine;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace Lsss
{
    public class TravelThroughWormholeSystem : SubSystem
    {
        protected override void OnUpdate()
        {
            var wormholeLayer = sceneGlobalEntity.GetCollectionComponent<WormholeCollisionLayer>(true).layer;
            var bulletLayer   = sceneGlobalEntity.GetCollectionComponent<BulletCollisionLayer>(true).layer;

            var processor = new TeleportWormholeTravelersProcessor
            {
                posCDFE     = this.GetPhysicsComponentDataFromEntity<Translation>(),
                rotCDFE     = this.GetPhysicsComponentDataFromEntity<Rotation>(),
                destCDFE    = GetComponentDataFromEntity<WormholeDestination>(true),
                ltwCDFE     = GetComponentDataFromEntity<LocalToWorld>(true),
                colCDFE     = GetComponentDataFromEntity<Collider>(true),
                prevPosCDFE = this.GetPhysicsComponentDataFromEntity<BulletPreviousPosition>()
            };

            var backup = Dependency;
            Dependency = default;

            Entities.WithAll<FactionTag>().ForEach((Entity entity, int entityInQueryIndex) =>
            {
                if (entityInQueryIndex == 0)
                    Dependency = backup;

                var shipLayer = EntityManager.GetCollectionComponent<FactionShipsCollisionLayer>(entity, true).layer;
                Dependency    = Physics.FindPairs(wormholeLayer, shipLayer, processor).ScheduleParallel(Dependency);
            }).WithoutBurst().Run();

            Dependency = Physics.FindPairs(wormholeLayer, bulletLayer, processor).ScheduleParallel(Dependency);
        }

        //Assumes B is traveler
        struct TeleportWormholeTravelersProcessor : IFindPairsProcessor
        {
            public PhysicsComponentDataFromEntity<Translation>             posCDFE;
            public PhysicsComponentDataFromEntity<Rotation>                rotCDFE;
            [ReadOnly] public ComponentDataFromEntity<WormholeDestination> destCDFE;
            [ReadOnly] public ComponentDataFromEntity<LocalToWorld>        ltwCDFE;
            [ReadOnly] public ComponentDataFromEntity<Collider>            colCDFE;

            //Todo: Remove hack
            public PhysicsComponentDataFromEntity<BulletPreviousPosition> prevPosCDFE;

            public void Execute(FindPairsResult result)
            {
                if (Physics.DistanceBetween(result.bodyA.collider, result.bodyA.transform, result.bodyB.collider, result.bodyB.transform, 0f, out _))
                {
                    var   aabb          = Physics.CalculateAabb(result.bodyB.collider, RigidTransform.identity);
                    float forwardOffset = -aabb.min.z * 2;  //Distance to butt doubled for safety

                    var            destEntity = destCDFE[result.entityA].wormholeDestination;
                    SphereCollider destCol    = colCDFE[destEntity];
                    float          destRad    = destCol.radius;
                    var            destLtw    = ltwCDFE[destEntity];

                    var destInSrcRot = math.mul(math.inverse(result.bodyA.transform.rot), destLtw.Rotation);
                    //var newRot       = math.mul(result.bodyB.transform.rot, destInSrcRot);
                    var newRot = math.mul(destInSrcRot, result.bodyB.transform.rot);

                    var newPos                  = destLtw.Position + math.rotate(destInSrcRot, result.bodyB.transform.pos - result.bodyA.transform.pos);
                    var wormholeProjectedOffset = math.dot(math.forward(newRot), destLtw.Position - newPos);

                    newPos += (wormholeProjectedOffset + forwardOffset + destRad) * math.forward(newRot) * 1f;

                    posCDFE[result.entityB] = new Translation { Value = newPos };
                    rotCDFE[result.entityB]                           = new Rotation { Value = newRot };

                    //This could cause bullets to phase past ships, but will have to suffice until we have layer-scope spherecasts and the butt is fixed-length.
                    if (prevPosCDFE.HasComponent(result.entityB))
                    {
                        prevPosCDFE[result.entityB] = new BulletPreviousPosition { previousPosition = newPos };
                    }
                }
            }
        }
    }
}

