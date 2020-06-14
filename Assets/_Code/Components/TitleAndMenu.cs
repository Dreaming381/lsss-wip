using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Lsss
{
    public class TitleAndMenu : MonoBehaviour
    {
        public GameObject titlePanel;
        public GameObject menuPanel;

        [Space(10)]
        public TMP_Text pressToStartText;
        public float    pulsePeriod = 5f;

        [HideInInspector]
        public string selectedScene;

        public void SetScene(string nextScene)
        {
            selectedScene = nextScene;
        }
    }
}

