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
    public partial class TravelThroughWormholeSystem : SubSystem
    {
        protected override void OnUpdate()
        {
            var wormholeLayer = sceneBlackboardEntity.GetCollectionComponent<WormholeCollisionLayer>(true).layer;
            var bulletLayer   = sceneBlackboardEntity.GetCollectionComponent<BulletCollisionLayer>(true).layer;

            var processor = new TeleportWormholeTravelersProcessor
            {
                posCDFE     = GetComponentLookup<Translation>(),
                rotCDFE     = GetComponentLookup<Rotation>(),
                destCDFE    = GetComponentLookup<WormholeDestination>(true),
                ltwCDFE     = GetComponentLookup<LocalToWorld>(true),
                colCDFE     = GetComponentLookup<Collider>(true),
                prevPosCDFE = GetComponentLookup<BulletPreviousPosition>()
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
            public PhysicsComponentLookup<Translation>             posCDFE;
            public PhysicsComponentLookup<Rotation>                rotCDFE;
            [ReadOnly] public ComponentLookup<WormholeDestination> destCDFE;
            [ReadOnly] public ComponentLookup<LocalToWorld>        ltwCDFE;
            [ReadOnly] public ComponentLookup<Collider>            colCDFE;

            //Todo: Remove hack
            public PhysicsComponentLookup<BulletPreviousPosition> prevPosCDFE;

            public void Execute(in FindPairsResult result)
            {
                if (Physics.DistanceBetween(result.bodyA.collider, result.bodyA.transform, result.bodyB.collider, result.bodyB.transform, 0f, out _))
                {
                    var   aabb          = result.aabbB;
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

