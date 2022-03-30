using Unity.Entities;

namespace Latios.Authoring.Systems
{
    [UpdateInGroup(typeof(GameObjectBeforeConversionGroup), OrderLast = true)]
    [WorldSystemFilter(WorldSystemFilterFlags.GameObjectConversion)]
    public class SmartBlobberConversionGroup : ComponentSystemGroup
    {
    }
}

