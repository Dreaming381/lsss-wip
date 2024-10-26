using Latios.Transforms;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Events;

namespace Latios.LifeFX
{
    [AddComponentMenu("Latios/LifeFX/Graphics Global Buffer Receptor")]
    public class GraphicsGlobalBufferReceptor : MonoBehaviour, IInitializeGameObjectEntity
    {
        [SerializeField] private string m_bufferShaderProperty;
        private int                     m_propertyId;

        public delegate void OnGraphicsGlobalPublishedDelegate(GraphicsBuffer graphicsBuffer);
        public event OnGraphicsGlobalPublishedDelegate      OnGraphicsGlobalPublished;
        [SerializeField] private UnityEvent<GraphicsBuffer> OnGraphicsGlobalPublishedSerialized;

        public virtual void Publish(GraphicsBuffer graphicsBuffer)
        {
        }

        public void Initialize(LatiosWorld latiosWorld, Entity gameObjectEntity)
        {
            if (string.IsNullOrEmpty(m_bufferShaderProperty))
                return;

            m_propertyId = Shader.PropertyToID(m_bufferShaderProperty);
            DynamicBuffer<GraphicsGlobalBufferDestination> buffer;
            if (latiosWorld.EntityManager.HasBuffer<GraphicsGlobalBufferDestination>(gameObjectEntity))
                buffer = latiosWorld.EntityManager.GetBuffer<GraphicsGlobalBufferDestination>(gameObjectEntity);
            else
                buffer = latiosWorld.EntityManager.AddBuffer<GraphicsGlobalBufferDestination>(gameObjectEntity);
            buffer.Add(new GraphicsGlobalBufferDestination
            {
                requestor        = this,
                shaderPropertyId = m_propertyId,
            });
        }

        internal void PublishInternal(GraphicsBuffer graphicsBuffer)
        {
            Publish(graphicsBuffer);
            OnGraphicsGlobalPublished?.Invoke(graphicsBuffer);
            OnGraphicsGlobalPublishedSerialized?.Invoke(graphicsBuffer);
        }
    }
}

