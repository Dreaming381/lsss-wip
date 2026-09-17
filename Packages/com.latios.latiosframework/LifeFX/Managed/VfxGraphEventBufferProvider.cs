using UnityEngine;
using UnityEngine.VFX;

namespace Latios.LifeFX
{
    /// <summary>
    /// Feeds GPU events from ECS data to a VFX Graph. Attach this to a VFX Graph GameObject with the GameObjectEntity component.
    /// </summary>
    [AddComponentMenu("Latios/LifeFX/VFX Graph Event Buffer Provider (LifeFX)")]
    public class VfxGraphEventBufferProvider : GraphicsEventBufferReceptor
    {
        [Header("VFX Graph Properties")]
        [SerializeField] private string buffer;
        [SerializeField] private string start;
        [SerializeField] private string count;

        private int bufferId;
        private int startId;
        private int countId;

        private bool hasBuffer     = false;
        private bool hasStart      = false;
        private bool hasCount      = false;
        private bool isInitialized = false;

        private VisualEffect effect;

        public override void Publish(GraphicsBuffer graphicsBuffer, int startIndex, int eventCount)
        {
            if (!isInitialized)
            {
                isInitialized = true;
                TryGetComponent(out effect);
                if (effect == null)
                    return;

                if (!string.IsNullOrEmpty(buffer))
                {
                    bufferId  = Shader.PropertyToID(buffer);
                    hasBuffer = effect.HasGraphicsBuffer(bufferId);
                    if (!hasBuffer)
                        Debug.LogWarning($"{name} specifies the buffer property \"{buffer}\", which {effect.visualEffectAsset?.name} does not expose.", this);
                }
                if (!string.IsNullOrEmpty(start))
                {
                    startId  = Shader.PropertyToID(start);
                    hasStart = effect.HasInt(startId);
                    if (!hasStart)
                        Debug.LogWarning($"{name} specifies the start property \"{start}\", which {effect.visualEffectAsset?.name} does not expose.", this);
                }
                if (!string.IsNullOrEmpty(count))
                {
                    countId  = Shader.PropertyToID(count);
                    hasCount = effect.HasInt(countId);
                    if (!hasCount)
                        Debug.LogWarning($"{name} specifies the count property \"{count}\", which {effect.visualEffectAsset?.name} does not expose.", this);
                }
            }

            if (effect == null)
                return;

            if (hasBuffer)
                effect.SetGraphicsBuffer(bufferId, graphicsBuffer);
            if (hasStart)
                effect.SetInt(startId, startIndex);
            if (hasCount)
                effect.SetInt(countId, eventCount);
        }
    }
}

