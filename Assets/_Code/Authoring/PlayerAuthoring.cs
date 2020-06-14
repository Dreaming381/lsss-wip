using Unity.Entities;
using UnityEngine;

namespace Lsss.Authoring
{
    public class PlayerAuthoring : MonoBehaviour, IConvertGameObjectToEntity
    {
        public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
        {
            dstManager.AddComponent<PlayerTag>(entity);
        }
    }
}

