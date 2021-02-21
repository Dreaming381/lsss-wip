using Unity.Entities;

namespace Lsss
{
    public struct PlayerTag : IComponentData { }

    public struct FactionSpawnsPlayerTag : IComponentData { }

    //Temporary until DOTS Input System is incorporated
    public struct MouseLookMultiplier : IComponentData
    {
        public float multiplier;
    }
}

