using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Lsss.Authoring
{
    [DisallowMultipleComponent]
    [AddComponentMenu("LSSS/Objects/Wormhole")]
    public class WormholeAuthoring : MonoBehaviour
    {
        public WormholeAuthoring otherEnd;
        public float             swarchschildRadius    = 0.1f;
        public float             maxW                  = 5f;
        public float             gravityWarpZoneRadius = 10f;
    }

    public class WormholeBaker : Baker<WormholeAuthoring>
    {
        public override void Bake(WormholeAuthoring authoring)
        {
            AddComponent(new WormholeDestination { wormholeDestination = GetEntity(authoring.otherEnd) });
            AddComponent<WormholeTag>();

            AddComponent(new GravityWarpZone
            {
                swarchschildRadius = authoring.swarchschildRadius,
                maxW               = authoring.maxW
            });
            AddComponent(new GravityWarpZoneRadius { radius = authoring.gravityWarpZoneRadius });
            AddComponent<GravityWarpZoneTag>();
        }
    }
}

