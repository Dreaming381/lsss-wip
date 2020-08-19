using System.Text;
using Latios;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Lsss.Tools
{
    public class ProfilingDisplayUpdateSystem : SubSystem
    {
        Texture2D     m_texture       = new Texture2D(256, 256, TextureFormat.RGBA32, false);
        StringBuilder m_stringBuilder = new StringBuilder();

        protected override void OnUpdate()
        {
            var profilingData = worldGlobalEntity.GetCollectionComponent<ProfilingData>(false);
            Entities.ForEach((ProfilerPanel panel) =>
            {
                //Strange bug in build
                if (panel.panel == null)
                    return;

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
                    for (int i = 0; i < profilingData.barValues.Length; i++)
                    {
                        m_stringBuilder.Append(profilingData.barValues[i]);
                        m_stringBuilder.Append('\n');
                        m_stringBuilder.Append('\n');
                    }
                    panel.labels.SetText(m_stringBuilder);
                }
            }).WithoutBurst().Run();
        }
    }
}

