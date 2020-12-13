using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Lsss.Authoring
{
    [DisallowMultipleComponent]
    [AddComponentMenu("LSSS/Spawning/Orbital Spawner")]
    public class OrbitalSpawnPointAuthoring : MonoBehaviour, IDeclareReferencedPrefabs, IConvertGameObjectToEntity
    {
        public float3                     center;
        public float                      orbitTime = 100f;
        public SpawnPointGraphicAuthoring spawnGraphicPrefab;

        public void DeclareReferencedPrefabs(List<GameObject> referencedPrefabs)
        {
            referencedPrefabs.Add(spawnGraphicPrefab.gameObject);
        }

        public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
        {
            dstManager.AddComponentData(entity, new SpawnPoint
            {
                spawnGraphicPrefab = conversionSystem.GetPrimaryEntity(spawnGraphicPrefab),
                maxTimeUntilSpawn  = spawnGraphicPrefab.spawnTime,
                maxPauseTime       = spawnGraphicPrefab.lifeTime
            });
            dstManager.AddComponentData(entity, new SpawnPayload { disabledShip = Entity.Null });
            float3 outwardVector                                                = (float3)transform.position - center;
            dstManager.AddComponentData(entity, new SpawnPointOrbitalPath
            {
                center           = center,
                radius           = math.distance(transform.position, center),
                orbitSpeed       = 2 * math.PI / orbitTime,
                orbitPlaneNormal = math.normalize(math.cross(outwardVector, math.cross(outwardVector, transform.up)))
            });
            dstManager.AddComponent<SafeToSpawn>(   entity);
            dstManager.AddComponent<SpawnTimes>(    entity);
            dstManager.AddComponent<SpawnPointTag>( entity);
        }

        void DrawOrbit()
        {
            Gizmos.color                 = new Color(0f, 1f, 0f, 0.75f);
            float3 currentOutwardVector  = (float3)transform.position - center;
            float3 originalOutwardVector = currentOutwardVector;
            float3 normal                = math.normalize(math.cross(originalOutwardVector, math.cross(originalOutwardVector, transform.up)));
            var    rotation              = quaternion.AxisAngle(normal, 2 * math.PI / 512);
            float  radius                = math.length(currentOutwardVector);
            for (int i = 1; i < 512; i++)
            {
                float3 newOutwardVector = math.rotate(rotation, currentOutwardVector);
                newOutwardVector        = math.normalize(newOutwardVector) * radius;
                Gizmos.DrawLine(currentOutwardVector + center, newOutwardVector + center);
                currentOutwardVector = newOutwardVector;
            }
            Gizmos.DrawLine(currentOutwardVector + center, originalOutwardVector + center);

            Gizmos.DrawSphere(transform.position, 10f);
        }

        private void OnDrawGizmos()
        {
            DrawOrbit();
        }
    }
}

