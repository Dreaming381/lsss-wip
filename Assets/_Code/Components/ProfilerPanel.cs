using System.Collections;
using System.Collections.Generic;
using Latios;
using Latios.Transforms;
using TMPro;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;

namespace Lsss.Tools
{
    [AddComponentMenu("LSSS/UI/Profiler Panel")]
    public class ProfilerPanel : MonoBehaviour, IInitializeGameObjectEntity
    {
        public GameObject panel;
        public RawImage   image;
        public TMP_Text   labels;

        public void Initialize(LatiosWorld latiosWorld, Entity gameObjectEntity)
        {
            latiosWorld.latiosWorldUnmanaged.AddManagedStructComponent(gameObjectEntity, new ProfilerPanelReference { profilerPanel = this });
        }
    }

    public partial struct ProfilerPanelReference : IManagedStructComponent
    {
        public ProfilerPanel profilerPanel;
    }
}

