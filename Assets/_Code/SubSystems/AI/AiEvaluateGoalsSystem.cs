using Latios;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine.SceneManagement;

namespace Lsss
{
    public class AiEvaluateGoalsSystem : SubSystem
    {
        struct Rng : IComponentData
        {
            public Random random;
        }

        protected override void OnUpdate()
        {
            if (!sceneGlobalEntity.HasComponentData<Rng>())
                sceneGlobalEntity.AddComponentData(new Rng { random = new Random(26247) });

            float  arenaRadius = sceneGlobalEntity.GetComponentData<ArenaRadius>().radius;
            var    random      = sceneGlobalEntity.GetComponentData<Rng>().random;
            Entity sceneEntity = sceneGlobalEntity;

            Entities.WithAll<AiRadarTag>().ForEach((ref AiShipRadar radar, in AiShipRadarScanResults results) =>
            {
                radar.target = results.target;
            }).ScheduleParallel();

            Entities.WithAll<AiTag>().ForEach((ref AiDestination destination, ref AiWantsToFire wantsToFire, in Translation trans, in AiPersonality personality, in AiBrain brain) =>
            {
                var shipScan = GetComponent<AiShipRadarScanResults>(
                    brain.shipRadar);
                wantsToFire.fire = shipScan.nearestEnemy != Entity.Null && !shipScan.friendFound;

                destination.chase = false;
                if (shipScan.target != Entity.Null)
                {
                    destination.position = math.forward(shipScan.targetTransform.rot) * personality.targetLeadDistance + shipScan.targetTransform.pos;
                    destination.chase    = true;
                }
                else if (math.distancesq(destination.position, trans.Value) < personality.destinationRadius * personality.destinationRadius)
                {
                    float radius                               = random.NextFloat(0f, arenaRadius);
                    destination.position                       = random.NextFloat3Direction() * radius;
                    SetComponent(sceneEntity, new Rng { random = random });
                }
            }).Schedule();
        }
    }
}

