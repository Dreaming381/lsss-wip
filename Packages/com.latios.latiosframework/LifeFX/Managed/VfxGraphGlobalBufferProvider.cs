using UnityEngine;
using UnityEngine.VFX;

namespace Latios.LifeFX
{
    [AddComponentMenu("Latios/LifeFX/VFX Graph Global Buffer Provider")]
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
                    hasBuffer = true;
                    bufferId  = Shader.PropertyToID(buffer);
                }
            }

            if (effect == null)
                return;

            if (hasBuffer)
                effect.SetGraphicsBuffer(bufferId, graphicsBuffer);
        }
    }
}

