using UnityEngine;
using UnityEngine.VFX;

namespace Latios.LifeFX
{
    /// <summary>
    /// Feeds a GPU buffer populated from ECS data to a VFX Graph. Attach this to a VFX Graph GameObject with the GameObjectEntity component.
    /// </summary>
    /// <remarks>
    /// Supported built-in shader properties:
    /// _latiosDeformBuffer
    /// _latiosBoneTransforms
    /// _latiosTrackedWorldTransforms
    /// </remarks>
    [AddComponentMenu("Latios/LifeFX/VFX Graph Global Buffer Provider (LifeFX)")]
    public class VfxGraphGlobalBufferProvider : GraphicsGlobalBufferReceptor
    {
        [Header("VFX Graph Properties")]
        [SerializeField] private string buffer;

        private int bufferId;

        private bool hasBuffer     = false;
        private bool isInitialized = false;

        private VisualEffect effect;

        public override void Publish(GraphicsBuffer graphicsBuffer)
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
                        Debug.LogWarning($"{name} specifies the global buffer property \"{buffer}\", which {effect.visualEffectAsset?.name} does not expose.", this);
                }
            }

            if (effect == null)
                return;

            if (hasBuffer)
                effect.SetGraphicsBuffer(bufferId, graphicsBuffer);
        }
    }
}

