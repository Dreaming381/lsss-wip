using System.Text;
using Latios;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

using static Unity.Entities.SystemAPI;

namespace Lsss.Tools
{
    public partial class ProfilingDisplayUpdateSystem : SubSystem
    {
        Texture2D     m_texture       = new Texture2D(256, 256, TextureFormat.RGBA32, false);
        StringBuilder m_stringBuilder = new StringBuilder();

        protected override void OnUpdate()
        {
            var panelEntities = QueryBuilder().WithAll<ProfilerPanelReference.ExistComponent>().Build().ToEntityArray(Allocator.Temp);

            if (!worldBlackboardEntity.HasCollectionComponent<ProfilingData>())
            {
                foreach(Entity entity in panelEntities)
                {
                    var panel = latiosWorldUnmanaged.GetManagedStructComponent<ProfilerPanelReference>(entity).profilerPanel;
                    if (panel.panel != null)
                    {
                        if (panel.panel.activeSelf)
                            panel.panel.SetActive(false);
                    }
                }
                return;
            }

            var profilingData = worldBlackboardEntity.GetCollectionComponent<ProfilingData>(false);
            CompleteDependency();
            foreach (Entity entity in panelEntities)
            {
                var panel = latiosWorldUnmanaged.GetManagedStructComponent<ProfilerPanelReference>(entity).profilerPanel;
                if (panel.panel == null)
                {
                    Debug.LogError("Exists with destroyed panel");
                    Debug.LogError(EntityManager.Exists(entity));
                    var types = EntityManager.GetComponentTypes(entity);
                    foreach (var t in types)
                    {
                        Debug.LogError(t.GetManagedType());
                    }
                    return;
                }

                bool toggle = Keyboard.current.pKey.wasPressedThisFrame;
                if (toggle)
                    panel.panel.SetActive(!panel.panel.activeSelf);
                if (panel.panel.activeSelf)
                {
                    if (panel.image.texture == null)
                    {
                        panel.image.texture = m_texture;
                    }

                    m_texture.LoadRawTextureData(profilingData.image);
                    m_texture.Apply();

                    m_stringBuilder.Clear();
                    FixedString4096Bytes fixedString = default;
                    for (int i = 0; i < profilingData.barValues.Length; i++)
                    {
                        fixedString.Append(profilingData.barValues[i]);
                        fixedString.Append('\n');
                        fixedString.Append('\n');
                    }
                    m_stringBuilder.Append(in fixedString);
                    panel.labels.SetText(m_stringBuilder);
                }
            }
        }
    }
}

