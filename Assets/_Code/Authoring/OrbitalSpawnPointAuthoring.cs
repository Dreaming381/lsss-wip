using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Lsss.Authoring
{
    [DisallowMultipleComponent]
    [AddComponentMenu("LSSS/Spawning/Orbital Spawner")]
    public class OrbitalSpawnPointAuthoring : MonoBehaviour
    {
        public float3                     center;
        public float                      orbitTime = 100f;
        public SpawnPointGraphicAuthoring spawnGraphicPrefab;

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

    public class OrbitalSpawnPointBaker : Baker<OrbitalSpawnPointAuthoring>
    {
        public override void Bake(OrbitalSpawnPointAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            GetComponent<SpawnPointGraphicAuthoring>(authoring.spawnGraphicPrefab);

            AddComponent(entity, new SpawnPoint
            {
                spawnGraphicPrefab = GetEntity(authoring.spawnGraphicPrefab, TransformUsageFlags.Dynamic),
                maxTimeUntilSpawn  = authoring.spawnGraphicPrefab.spawnTime,
                maxPauseTime       = authoring.spawnGraphicPrefab.lifeTime
            });
            AddComponent(entity, new SpawnPayload { disabledShip = Entity.Null });

            var    transform     = GetComponent<Transform>();
            float3 outwardVector = (float3)transform.position - authoring.center;
            AddComponent(entity, new SpawnPointOrbitalPath
            {
                center           = authoring.center,
                radius           = math.distance(transform.position, authoring.center),
                orbitSpeed       = 2 * math.PI / authoring.orbitTime,
                orbitPlaneNormal = math.normalize(math.cross(outwardVector, math.cross(outwardVector, transform.up)))
            });
            AddComponent<SafeToSpawn>(  entity);
            AddComponent<SpawnTimes>(   entity);
            AddComponent<SpawnPointTag>(entity);
        }
    }
}

