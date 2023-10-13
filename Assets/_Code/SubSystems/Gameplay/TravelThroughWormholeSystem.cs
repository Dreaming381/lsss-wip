using Latios;
using Latios.Psyshock;
using Latios.Transforms;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

using static Unity.Entities.SystemAPI;

// Todo: Need to redesign this feature.
/*
   namespace Lsss
   {
    [BurstCompile]
    public partial struct TravelThroughWormholeSystem : ISystem
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

        //[BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var wormholeLayer = latiosWorld.sceneBlackboardEntity.GetCollectionComponent<WormholeCollisionLayer>(true).layer;
            var bulletLayer   = latiosWorld.sceneBlackboardEntity.GetCollectionComponent<BulletCollisionLayer>(true).layer;

            var processor = new TeleportWormholeTravelersProcessor
            {
                posCDFE     = GetComponentLookup<Translation>(),
                rotCDFE     = GetComponentLookup<Rotation>(),
                destCDFE    = GetComponentLookup<WormholeDestination>(true),
                ltwCDFE     = GetComponentLookup<LocalToWorld>(true),
                colCDFE     = GetComponentLookup<Collider>(true),
                prevPosCDFE = GetComponentLookup<BulletPreviousPosition>()
            };

            var factionEntities = QueryBuilder().With<Faction, FactionTag>().Build().ToEntityArray(Allocator.Temp);
            foreach (var entity in factionEntities)
            {
                var shipLayer    = latiosWorld.GetCollectionComponent<FactionShipsCollisionLayer>(entity, true).layer;
                state.Dependency = Physics.FindPairs(wormholeLayer, shipLayer, processor).ScheduleParallel(state.Dependency);
            }

            state.Dependency = Physics.FindPairs(wormholeLayer, bulletLayer, processor).ScheduleParallel(state.Dependency);
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
 */

