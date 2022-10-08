using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;

namespace Lsss.Tools
{
    [AddComponentMenu("LSSS/UI/Profiler Panel")]
    public class ProfilerPanel : MonoBehaviour
    {
        public GameObject panel;
        public RawImage   image;
        public TMP_Text   labels;

        private Entity entity = Entity.Null;

        private void Update()
        {
            if (entity != Entity.Null)
                return;

            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            entity = em.CreateEntity();
            em.AddComponentObject(entity, this);
        }
    }
}

