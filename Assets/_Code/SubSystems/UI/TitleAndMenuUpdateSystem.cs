using Latios;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine.InputSystem;

namespace Lsss
{
    public class TitleAndMenuUpdateSystem : SubSystem
    {
        private SerializedSettings serializedSettings;
        const string               settingsPath = "PlayerSavedSettings.json";

        protected override void OnCreate()
        {
            serializedSettings = UnityEngine.ScriptableObject.CreateInstance<SerializedSettings>();
            if (System.IO.File.Exists(settingsPath))
            {
                var json = System.IO.File.ReadAllText(settingsPath);
                UnityEngine.JsonUtility.FromJsonOverwrite(json, serializedSettings);
                UnityEngine.QualitySettings.SetQualityLevel(serializedSettings.graphicsQuality);
            }
            else
            {
                serializedSettings.graphicsQuality     = UnityEngine.QualitySettings.GetQualityLevel();
                serializedSettings.musicVolume         = 1f;
                serializedSettings.sfxVolume           = 1f;
                serializedSettings.mouseLoolMultiplier = 1f;
            }
            worldBlackboardEntity.AddComponentData(new AudioMasterVolumes
            {
                musicVolume = serializedSettings.musicVolume,
                sfxVolume   = serializedSettings.sfxVolume
            });
            worldBlackboardEntity.AddComponentData(new GraphicsQualityLevel { level     = serializedSettings.graphicsQuality });
            worldBlackboardEntity.AddComponentData(new MouseLookMultiplier { multiplier = serializedSettings.mouseLoolMultiplier });
        }

        protected override void OnUpdate()
        {
            Entities.ForEach((TitleAndMenu titleAndMenu, in TitleAndMenuResources resources) =>
            {
                if (titleAndMenu.titlePanel.activeSelf)
                {
                    bool somethingPressed = false;
                    var  gamepad          = Gamepad.current;
                    if (gamepad != null)
                    {
                        if (gamepad.buttonEast.wasPressedThisFrame ||
                            gamepad.buttonNorth.wasPressedThisFrame ||
                            gamepad.buttonSouth.wasPressedThisFrame ||
                            gamepad.buttonWest.wasPressedThisFrame ||
                            gamepad.leftStickButton.wasPressedThisFrame ||
                            gamepad.rightStickButton.wasPressedThisFrame ||
                            gamepad.startButton.wasPressedThisFrame ||
                            gamepad.selectButton.wasPressedThisFrame ||
                            gamepad.leftShoulder.wasPressedThisFrame ||
                            gamepad.leftTrigger.wasPressedThisFrame ||
                            gamepad.rightShoulder.wasPressedThisFrame ||
                            gamepad.rightTrigger.wasPressedThisFrame)
                        {
                            somethingPressed = true;
                        }
                    }
                    else
                    {
                        var mouse    = Mouse.current;
                        var keyboard = Keyboard.current;
                        if (mouse != null && keyboard != null)
                        {
                            if (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame)
                                somethingPressed = true;
                            if (keyboard.anyKey.wasPressedThisFrame)
                                somethingPressed = true;
                        }
                    }
                    if (somethingPressed)
                    {
                        titleAndMenu.titlePanel.SetActive(false);
                        titleAndMenu.menuPanel.SetActive(true);
                        PlaySound(resources.selectSoundEffect);
                        titleAndMenu.musicSlider.value                  = serializedSettings.musicVolume;
                        titleAndMenu.sfxSlider.value                    = serializedSettings.sfxVolume;
                        titleAndMenu.graphicsDropdown.value             = serializedSettings.graphicsQuality;
                        titleAndMenu.mouseLookMultiplierSlider.value    = serializedSettings.mouseLoolMultiplier;
                        titleAndMenu.musicSliderLastValue               = titleAndMenu.musicSlider.value;
                        titleAndMenu.sfxSliderLastValue                 = titleAndMenu.sfxSlider.value;
                        titleAndMenu.mouseLookMultiplierSliderLastValue = titleAndMenu.mouseLookMultiplierSlider.value;
                    }
                }
                else
                {
                    var gamepad = Gamepad.current;
                    {
                        if (gamepad != null)
                        {
                            if (titleAndMenu.eventSystem.currentSelectedGameObject == null || !titleAndMenu.eventSystem.currentSelectedGameObject.activeInHierarchy)
                            {
                                if (titleAndMenu.menuPanel.activeSelf)
                                {
                                    foreach (var selectableButton in titleAndMenu.defaultSelectedMissionButtons)
                                    {
                                        if (selectableButton.gameObject.activeInHierarchy)
                                        {
                                            selectableButton.Select();
                                            titleAndMenu.lastSelectedThing = selectableButton.gameObject;
                                            break;
                                        }
                                    }
                                }
                                else if (titleAndMenu.settingsPanel.activeSelf)
                                {
                                    titleAndMenu.musicSlider.Select();
                                    titleAndMenu.lastSelectedThing = titleAndMenu.musicSlider.gameObject;
                                }
                                else
                                {
                                    titleAndMenu.creditsBackButton.Select();
                                    titleAndMenu.lastSelectedThing = titleAndMenu.creditsBackButton.gameObject;
                                }
                            }
                            if (gamepad.bButton.wasPressedThisFrame)
                            {
                                if (titleAndMenu.menuPanel.activeSelf)
                                {
                                    titleAndMenu.menuPanel.SetActive(false);
                                    titleAndMenu.titlePanel.SetActive(true);
                                    PlaySound(resources.selectSoundEffect);
                                }
                                else if (titleAndMenu.settingsPanel.activeSelf)
                                {
                                    titleAndMenu.settingsPanel.SetActive(false);
                                    titleAndMenu.menuPanel.SetActive(true);
                                    PlaySound(resources.selectSoundEffect);
                                }
                                else if (titleAndMenu.creditsPanel.activeSelf)
                                {
                                    titleAndMenu.creditsPanel.SetActive(false);
                                    titleAndMenu.menuPanel.SetActive(true);
                                    PlaySound(resources.selectSoundEffect);
                                }
                            }
                        }
                    }
                }

                if (titleAndMenu.eventSystem.currentSelectedGameObject != titleAndMenu.lastSelectedThing)
                {
                    PlaySound(resources.navigateSoundEffect);
                    titleAndMenu.lastSelectedThing = titleAndMenu.eventSystem.currentSelectedGameObject;
                }

                if (titleAndMenu.selectedScene.Length > 0)
                {
                    var ecb                                                                                              = latiosWorld.syncPoint.CreateEntityCommandBuffer();
                    ecb.AddComponent(                             sceneBlackboardEntity, new RequestLoadScene { newScene = titleAndMenu.selectedScene });
                    var sound                                                                                            = ecb.Instantiate(resources.selectSoundEffect);
                    ecb.AddComponent<DontDestroyOnSceneChangeTag>(sound);
                }

                if (titleAndMenu.openSettings)
                {
                    titleAndMenu.menuPanel.SetActive(false);
                    titleAndMenu.settingsPanel.SetActive(true);
                    PlaySound(resources.selectSoundEffect);
                    titleAndMenu.openSettings = false;
                }

                if (titleAndMenu.closeSettings)
                {
                    titleAndMenu.settingsPanel.SetActive(false);
                    titleAndMenu.menuPanel.SetActive(true);
                    PlaySound(resources.selectSoundEffect);
                    titleAndMenu.closeSettings = false;
                }

                if (titleAndMenu.openCredits)
                {
                    titleAndMenu.menuPanel.SetActive(false);
                    titleAndMenu.creditsPanel.SetActive(true);
                    PlaySound(resources.selectSoundEffect);
                    titleAndMenu.openCredits = false;
                }

                if (titleAndMenu.closeCredits)
                {
                    titleAndMenu.creditsPanel.SetActive(false);
                    titleAndMenu.menuPanel.SetActive(true);
                    PlaySound(resources.selectSoundEffect);
                    titleAndMenu.closeCredits = false;
                }

                //Workaround for UI not sending slider change events
                if (titleAndMenu.musicSliderDirty ||
                    (titleAndMenu.settingsPanel.activeSelf && titleAndMenu.musicSlider.value != titleAndMenu.musicSliderLastValue))
                {
                    serializedSettings.musicVolume = titleAndMenu.musicSlider.value;
                    titleAndMenu.musicSliderDirty  = false;
                    PlaySound(resources.selectSoundEffect);
                    WriteSettings();
                    var volumes         = worldBlackboardEntity.GetComponentData<AudioMasterVolumes>();
                    volumes.musicVolume = serializedSettings.musicVolume;
                    worldBlackboardEntity.SetComponentData(volumes);
                    titleAndMenu.musicSliderLastValue = serializedSettings.musicVolume;
                }

                if (titleAndMenu.sfxSliderDirty ||
                    (titleAndMenu.settingsPanel.activeSelf && titleAndMenu.sfxSlider.value != titleAndMenu.sfxSliderLastValue))
                {
                    serializedSettings.sfxVolume = titleAndMenu.sfxSlider.value;
                    titleAndMenu.sfxSliderDirty  = false;
                    PlaySound(resources.selectSoundEffect);
                    WriteSettings();
                    var volumes       = worldBlackboardEntity.GetComponentData<AudioMasterVolumes>();
                    volumes.sfxVolume = serializedSettings.sfxVolume;
                    worldBlackboardEntity.SetComponentData(volumes);
                    titleAndMenu.sfxSliderLastValue = serializedSettings.sfxVolume;
                }

                if (titleAndMenu.graphicsQualityDirty)
                {
                    serializedSettings.graphicsQuality = titleAndMenu.graphicsDropdown.value;
                    titleAndMenu.graphicsQualityDirty  = false;
                    PlaySound(resources.selectSoundEffect);
                    WriteSettings();
                    UnityEngine.QualitySettings.SetQualityLevel(serializedSettings.graphicsQuality);
                    worldBlackboardEntity.SetComponentData(new GraphicsQualityLevel { level = serializedSettings.graphicsQuality });
                }

                if (titleAndMenu.mouseLookMultiplierSliderDirty ||
                    (titleAndMenu.settingsPanel.activeSelf && titleAndMenu.mouseLookMultiplierSlider.value != titleAndMenu.mouseLookMultiplierSliderLastValue))
                {
                    serializedSettings.mouseLoolMultiplier      = titleAndMenu.mouseLookMultiplierSlider.value;
                    titleAndMenu.mouseLookMultiplierSliderDirty = false;
                    PlaySound(resources.selectSoundEffect);
                    WriteSettings();
                    worldBlackboardEntity.SetComponentData(new MouseLookMultiplier { multiplier = serializedSettings.mouseLoolMultiplier });
                    titleAndMenu.mouseLookMultiplierSliderLastValue                             = serializedSettings.mouseLoolMultiplier;
                }

                if (titleAndMenu.scrollLeft)
                {
                    for (int i = 0; i < titleAndMenu.missionPanels.Count; i++)
                    {
                        if (titleAndMenu.missionPanels[i].activeSelf)
                        {
                            titleAndMenu.missionPanels[i].SetActive(false);
                            if (i == 0)
                                i = titleAndMenu.missionPanels.Count - 1;
                            else
                                i--;
                            titleAndMenu.missionPanels[i].SetActive(true);
                            PlaySound(resources.selectSoundEffect);
                            break;
                        }
                    }
                    titleAndMenu.scrollLeft = false;
                }

                if (titleAndMenu.scrollRight)
                {
                    for (int i = 0; i < titleAndMenu.missionPanels.Count; i++)
                    {
                        if (titleAndMenu.missionPanels[i].activeSelf)
                        {
                            titleAndMenu.missionPanels[i].SetActive(false);
                            if (i == titleAndMenu.missionPanels.Count - 1)
                                i = 0;
                            else
                                i++;
                            titleAndMenu.missionPanels[i].SetActive(true);
                            PlaySound(resources.selectSoundEffect);
                            break;
                        }
                    }
                    titleAndMenu.scrollRight = false;
                }

                float a                             = 0.75f * (float)math.sin(Time.ElapsedTime / titleAndMenu.pulsePeriod * 2d * math.PI_DBL) + 0.5f;
                titleAndMenu.pressToStartText.color = new UnityEngine.Color(1f, 1f, 1f, a);
            }).WithoutBurst().Run();
        }

        protected override void OnDestroy()
        {
            UnityEngine.Object.Destroy(serializedSettings);
        }

        void PlaySound(Entity sound)
        {
            var ecb = latiosWorld.syncPoint.CreateEntityCommandBuffer();
            ecb.Instantiate(sound);
        }

        void WriteSettings()
        {
            var json = UnityEngine.JsonUtility.ToJson(serializedSettings);
            System.IO.File.WriteAllText(settingsPath, json);
        }
    }
}

