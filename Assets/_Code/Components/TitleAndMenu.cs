using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Entities;
using UnityEngine;

namespace Lsss
{
    [AddComponentMenu("LSSS/UI/Title and Menu")]
    public class TitleAndMenu : MonoBehaviour
    {
        public GameObject                           titlePanel;
        public GameObject                           menuPanel;
        public GameObject                           settingsPanel;
        public GameObject                           creditsPanel;
        public UnityEngine.EventSystems.EventSystem eventSystem;

        [Space(10)]
        public TMP_Text pressToStartText;
        public float    pulsePeriod = 5f;

        [Space(10)]
        public UnityEngine.UI.Slider musicSlider;
        public UnityEngine.UI.Slider sfxSlider;
        public TMP_Dropdown          graphicsDropdown;
        public UnityEngine.UI.Slider mouseLookMultiplierSlider;

        [Space(10)]
        public UnityEngine.UI.Button       creditsBackButton;
        public List<GameObject>            missionPanels;
        public List<UnityEngine.UI.Button> defaultSelectedMissionButtons;

        [HideInInspector] public string     selectedScene;
        [HideInInspector] public bool       openSettings;
        [HideInInspector] public bool       closeSettings;
        [HideInInspector] public bool       openCredits;
        [HideInInspector] public bool       closeCredits;
        [HideInInspector] public bool       musicSliderDirty;
        [HideInInspector] public bool       sfxSliderDirty;
        [HideInInspector] public bool       graphicsQualityDirty;
        [HideInInspector] public bool       mouseLookMultiplierSliderDirty;
        [HideInInspector] public bool       scrollLeft;
        [HideInInspector] public bool       scrollRight;
        [HideInInspector] public GameObject lastSelectedThing;

        //Slider events aren't working. Poll instead.
        [HideInInspector] public float musicSliderLastValue;
        [HideInInspector] public float sfxSliderLastValue;
        [HideInInspector] public float mouseLookMultiplierSliderLastValue;

        public void SetScene(string nextScene)
        {
            selectedScene = nextScene;
        }

        public void OpenSettings()
        {
            openSettings = true;
        }

        public void CloseSettings()
        {
            closeSettings = true;
        }

        public void OpenCredits()
        {
            openCredits = true;
        }

        public void CloseCredits()
        {
            closeCredits = true;
        }

        public void SetMusicVolume()
        {
            musicSliderDirty = true;
        }

        public void SetSfxVolume()
        {
            sfxSliderDirty = true;
        }

        public void SetGraphicsQuality()
        {
            graphicsQualityDirty = true;
        }

        public void SetMouseLookMultiplier()
        {
            mouseLookMultiplierSliderDirty = true;
        }

        public void ScrollLeft()
        {
            scrollLeft = true;
        }

        public void ScrollRight()
        {
            scrollRight = true;
        }

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

