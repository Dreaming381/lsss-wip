using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Lsss
{
    [AddComponentMenu("LSSS/UI/Title and Menu")]
    public class TitleAndMenu : MonoBehaviour
    {
        public GameObject                           titlePanel;
        public GameObject                           menuPanel;
        public GameObject                           settingsPanel;
        public UnityEngine.EventSystems.EventSystem eventSystem;

        [Space(10)]
        public TMP_Text pressToStartText;
        public float    pulsePeriod = 5f;

        [Space(10)]
        public UnityEngine.UI.Slider musicSlider;
        public UnityEngine.UI.Slider sfxSlider;
        public TMP_Dropdown          graphicsDropdown;

        [Space(10)]
        public UnityEngine.UI.Button firstMissionButton;

        [HideInInspector]
        public string selectedScene;

        [HideInInspector]
        public SerializedSettings settings;

        const string settingsPath = "PlayerSavedSettings.json";

        public void SetScene(string nextScene)
        {
            selectedScene = nextScene;
        }

        public void OpenSettings()
        {
            menuPanel.SetActive(false);
            settingsPanel.SetActive(true);
        }

        public void CloseSettings()
        {
            settingsPanel.SetActive(false);
            menuPanel.SetActive(true);
        }

        public void SetMusicVolume()
        {
            settings.musicVolume = musicSlider.value;
            WriteJson();
        }

        public void SetSfxVolume()
        {
            settings.sfxVolume = sfxSlider.value;
            WriteJson();
        }

        public void SetQualityLevel()
        {
            int level = graphicsDropdown.value;
            QualitySettings.SetQualityLevel(level);
            settings.graphicsQuality = level;
            WriteJson();
        }

        void WriteJson()
        {
            var json = JsonUtility.ToJson(settings);
            System.IO.File.WriteAllText(settingsPath, json);
        }

        private void Awake()
        {
            settings = ScriptableObject.CreateInstance<SerializedSettings>();
            if (System.IO.File.Exists(settingsPath))
            {
                var json = System.IO.File.ReadAllText(settingsPath);
                JsonUtility.FromJsonOverwrite(json, settings);
                musicSlider.value      = settings.musicVolume;
                sfxSlider.value        = settings.sfxVolume;
                graphicsDropdown.value = settings.graphicsQuality;
                QualitySettings.SetQualityLevel(settings.graphicsQuality);
            }
            else
            {
                settings.graphicsQuality = QualitySettings.GetQualityLevel();
                graphicsDropdown.value   = settings.graphicsQuality;
            }
        }
    }
}

