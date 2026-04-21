using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

//=^..^=   =^..^=   VERSION 1.0.2 (April 2026)    =^..^=    =^..^=
//                    Last Update 01/04/2026 
//=^..^=    =^..^=  By Pedro Sánchez Vázquez      =^..^=    =^..^=


/// Controls the Settings menu panel.

public class SettingsMenuController : MonoBehaviour
{
    [Header("Navigation")]
    [SerializeField] private MainMenuController mainMenuController;
    [SerializeField] private Button backButton;

    [Header("Audio")]
    [SerializeField] private Slider musicVolumeSlider;
    [SerializeField] private Slider sfxVolumeSlider;

    [Header("Gameplay - Duration Multipliers")]
    [Tooltip("Multiplier applied to all announcement durations (1 = original speed).")]
    [SerializeField] private Slider announcementDurationSlider;
    [Tooltip("Multiplier applied to all flower/scoring animation durations (1 = original speed).")]
    [SerializeField] private Slider animationDurationSlider;

    [Header("Gameplay - AI")]
    [Tooltip("Maximum thinking time for MinMax AI, in seconds (1–10).")]
    [SerializeField] private Slider aiThinkingTimeSlider;

    //[Header("Display")]
    //[SerializeField] private Toggle fullscreenToggle;
    //[SerializeField] private TMP_Dropdown resolutionDropdown;

    private void Awake()
    {
        if (backButton != null)
            backButton.onClick.AddListener(OnBackClicked);

        if (musicVolumeSlider != null)
            musicVolumeSlider.onValueChanged.AddListener(MusicVolumeChanged);
        if (sfxVolumeSlider != null)
            sfxVolumeSlider.onValueChanged.AddListener(SfxVolumeChanged);

        if (announcementDurationSlider != null)
        {
            announcementDurationSlider.minValue = GameController.AnnouncementDurationMin;
            announcementDurationSlider.maxValue = GameController.AnnouncementDurationMax;
            announcementDurationSlider.onValueChanged.AddListener(OnAnnouncementDurationChanged);
        }

        if (animationDurationSlider != null)
        {
            animationDurationSlider.minValue = GameController.AnimationDurationMin;
            animationDurationSlider.maxValue = GameController.AnimationDurationMax;
            animationDurationSlider.onValueChanged.AddListener(OnAnimationDurationChanged);
        }

        if (aiThinkingTimeSlider != null)
        {
            aiThinkingTimeSlider.minValue = GameController.AIThinkingTimeMin;
            aiThinkingTimeSlider.maxValue = GameController.AIThinkingTimeMax;
            aiThinkingTimeSlider.onValueChanged.AddListener(OnAIThinkingTimeChanged);
        }

        //if (fullscreenToggle != null)
        //    fullscreenToggle.onValueChanged.AddListener(OnFullscreenToggled);
    }

    private void OnEnable()
    {
        //Set sliders to current values from PlayerPrefs
        if (musicVolumeSlider != null)
            musicVolumeSlider.value = PlayerPrefs.GetFloat("MusicVolume", 1f);
        if (sfxVolumeSlider != null)
            sfxVolumeSlider.value = PlayerPrefs.GetFloat("SFXVolume", 1f);
        if (announcementDurationSlider != null)
            announcementDurationSlider.value = PlayerPrefs.GetFloat(GameController.AnnouncementDurationPrefKey, GameController.AnnouncementDurationDefault);
        if (animationDurationSlider != null)
            animationDurationSlider.value = PlayerPrefs.GetFloat(GameController.AnimationDurationPrefKey, GameController.AnimationDurationDefault);
        if (aiThinkingTimeSlider != null)
            aiThinkingTimeSlider.value = PlayerPrefs.GetFloat(GameController.AIThinkingTimePrefKey, GameController.AIThinkingTimeDefault);
        //if (fullscreenToggle != null)
        //    fullscreenToggle.isOn = Screen.fullScreen;

    }

    private void MusicVolumeChanged(float value)
    {
        PlayerPrefs.SetFloat("MusicVolume", value);
        GameEvents.MusicVolumeChanged();
    }

    private void SfxVolumeChanged(float value)
    {
        PlayerPrefs.SetFloat("SFXVolume", value);
        GameEvents.SFXVolumeChanged();
    }

    private void OnAnnouncementDurationChanged(float value)
    {
        PlayerPrefs.SetFloat(GameController.AnnouncementDurationPrefKey, value);
        if (GameController.Instance != null)
            GameController.Instance.SetAnnouncementMultiplier(value);
    }

    private void OnAnimationDurationChanged(float value)
    {
        PlayerPrefs.SetFloat(GameController.AnimationDurationPrefKey, value);
        if (GameController.Instance != null)
            GameController.Instance.SetAnimationMultiplier(value);
    }

    private void OnAIThinkingTimeChanged(float seconds)
    {
        PlayerPrefs.SetFloat(GameController.AIThinkingTimePrefKey, seconds);
        AIPlayerController ai = FindObjectOfType<AIPlayerController>();
        if (ai != null)
            ai.SetAIThinkingTimeMs(seconds * 1000f);
    }

    private void OnFullscreenToggled(bool isFullscreen)
    {
        Screen.fullScreen = isFullscreen;
    }

    private void OnBackClicked()
    {
        PlayerPrefs.Save();
        if (mainMenuController != null)
            mainMenuController.OnBackToMainMenu();
    }
}

