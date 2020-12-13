using usfxr;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Lsss.Authoring
{
    [DisallowMultipleComponent]
    [AddComponentMenu("LSSS/Behaviors/Sound Effect")]
    public class SoundEffectAuthoring : MonoBehaviour, IConvertGameObjectToEntity
    {
        public SfxrParams effectSettings;

        public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
        {
            //
        }
    }
}

