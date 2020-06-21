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
    public class AiInitializeNewShipsSystem : SubSystem
    {
        EntityQuery m_query;

        protected override void OnUpdate()
        {
            float arenaRadius = sceneGlobalEntity.GetComponentData<ArenaRadius>().radius;
            Entities.WithAll<AiInitializeTag>().WithStoreEntityQueryInField(ref m_query).ForEach((ref AiDestination targetPosition, in Translation trans, in Rotation rot,
                                                                                                  in AiPersonality personality) =>
            {
                targetPosition.position = math.forward(rot.Value) * (personality.spawnForwardDistance + personality.destinationRadius) + trans.Value;
                var radius              = math.length(targetPosition.position);
                targetPosition.position = math.select(targetPosition.position, targetPosition.position * arenaRadius / radius, radius > arenaRadius);
            }).Run();

            EntityManager.RemoveComponent<AiInitializeTag>(m_query);
        }
    }
}

