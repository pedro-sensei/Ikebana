using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using UnityEngine.Serialization;

//=^..^=   =^..^=   VERSION 1.1.0 (April 2026)    =^..^=    =^..^=
//                    Last Update 21/04/2026 
//=^..^=    =^..^=  By Pedro Sánchez Vázquez      =^..^=    =^..^=


/// Controls the Settings menu panel.

public class SettingsMenuController : MonoBehaviour
{
    private const string BoardRotationPrefKey = "EnableBoardRotation";
    private const string ScoringHelperPrefKey = "EnableScoringHelper";

    [Serializable]
    private class FlowerSelectorRow
    {
        public FlowerColor Color;
        public Button PrevButton;
        public Button NextButton;
        public Image PreviewImage;
        public TMP_Text VariantLabel;
    }

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

    [Header("Gameplay - Board")]
    [SerializeField] private Toggle boardRotationToggle;
    [SerializeField] private Toggle scoringHelperToggle;

    [Header("Flower Sprite Selectors")]
    [FormerlySerializedAs("spriteLibrary")]
    [SerializeField] private FlowerSpriteRepository flowerSpriteLibrary;
    [FormerlySerializedAs("spriteData")]
    [SerializeField] private FlowerSpriteData flowerSpriteData;
    [FormerlySerializedAs("spriteRows")]
    [SerializeField] private FlowerSelectorRow[] flowerSelectorRows;


    private void Awake()
    {
        if (mainMenuController == null && GameResources.Instance != null)
            mainMenuController = GameResources.Instance.MainMenuControllerRef;

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

        if (boardRotationToggle != null)
            boardRotationToggle.onValueChanged.AddListener(OnBoardRotationToggled);
        if (scoringHelperToggle != null)
            scoringHelperToggle.onValueChanged.AddListener(OnScoringHelperToggled);

        if ((flowerSpriteLibrary == null || flowerSpriteData == null) && GameResources.Instance != null)
        {
            if (flowerSpriteLibrary == null) flowerSpriteLibrary = GameResources.Instance.SpriteLibrary;
            if (flowerSpriteData == null) flowerSpriteData = GameResources.Instance.SpriteData;
        }

        WireFlowerSelectorButtons();

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
        if (boardRotationToggle != null)
            boardRotationToggle.isOn = PlayerPrefs.GetInt(BoardRotationPrefKey, 1) == 1;
        if (scoringHelperToggle != null)
            scoringHelperToggle.isOn = PlayerPrefs.GetInt(ScoringHelperPrefKey, 1) == 1;
        WireFlowerSelectorButtons();
        RefreshFlowerSelectorUI();

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

    private void OnBoardRotationToggled(bool enabled)
    {
        PlayerPrefs.SetInt(BoardRotationPrefKey, enabled ? 1 : 0);

        PlayerBoardView[] views = FindObjectsOfType<PlayerBoardView>(true);
        for (int i = 0; i < views.Length; i++)
            views[i].RefreshBoardRotationMode();
    }

    private void OnScoringHelperToggled(bool enabled)
    {
        PlayerPrefs.SetInt(ScoringHelperPrefKey, enabled ? 1 : 0);

        PlayerGridView[] grids = FindObjectsOfType<PlayerGridView>(true);
        for (int i = 0; i < grids.Length; i++)
            grids[i].RefreshScoringHelperOverlay();
    }

    private void WireFlowerSelectorButtons()
    {
        if (flowerSelectorRows == null) return;

        for (int i = 0; i < flowerSelectorRows.Length; i++)
        {
            FlowerSelectorRow row = flowerSelectorRows[i];
            if (row == null) continue;

            FlowerColor c = row.Color;

            if (row.PrevButton != null)
            {
                row.PrevButton.onClick.RemoveAllListeners();
                row.PrevButton.onClick.AddListener(() => ChangeFlowerSpriteVariant(c, -1));
            }

            if (row.NextButton != null)
            {
                row.NextButton.onClick.RemoveAllListeners();
                row.NextButton.onClick.AddListener(() => ChangeFlowerSpriteVariant(c, +1));
            }
        }
    }

    private void ChangeFlowerSpriteVariant(FlowerColor color, int delta)
    {
        if (flowerSpriteLibrary == null || flowerSpriteData == null) return;

        int count = flowerSpriteLibrary.VariantCount(color);
        if (count <= 0) return;

        int current = FlowerSpriteSettings.LoadIndex(color);
        int next = (current + delta) % count;
        if (next < 0) next += count;

        FlowerSpriteSettings.ApplyAndSave(color, next, flowerSpriteLibrary, flowerSpriteData);
        RefreshFlowerSelectorUI();
    }

    private void RefreshFlowerSelectorUI()
    {
        if (flowerSelectorRows == null || flowerSpriteLibrary == null || flowerSpriteData == null) return;

        for (int i = 0; i < flowerSelectorRows.Length; i++)
        {
            FlowerSelectorRow row = flowerSelectorRows[i];
            if (row == null) continue;

            int count = flowerSpriteLibrary.VariantCount(row.Color);
            int index = count > 0 ? Mathf.Clamp(FlowerSpriteSettings.LoadIndex(row.Color), 0, count - 1) : 0;

            if (row.PreviewImage != null)
                row.PreviewImage.sprite = flowerSpriteData.GetSprite(row.Color);

            if (row.VariantLabel != null)
                row.VariantLabel.text = count > 0 ? (index + 1) + "/" + count : "-";

            bool hasVariants = count > 1;
            if (row.PrevButton != null) row.PrevButton.interactable = hasVariants;
            if (row.NextButton != null) row.NextButton.interactable = hasVariants;
        }
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

    public void PrevBlueFlowerSprite()   { ChangeFlowerSpriteVariant(FlowerColor.Blue, -1); }
    public void NextBlueFlowerSprite()   { ChangeFlowerSpriteVariant(FlowerColor.Blue, +1); }
    public void PrevYellowFlowerSprite() { ChangeFlowerSpriteVariant(FlowerColor.Yellow, -1); }
    public void NextYellowFlowerSprite() { ChangeFlowerSpriteVariant(FlowerColor.Yellow, +1); }
    public void PrevRedFlowerSprite()    { ChangeFlowerSpriteVariant(FlowerColor.Red, -1); }
    public void NextRedFlowerSprite()    { ChangeFlowerSpriteVariant(FlowerColor.Red, +1); }
    public void PrevPinkFlowerSprite()   { ChangeFlowerSpriteVariant(FlowerColor.Pink, -1); }
    public void NextPinkFlowerSprite()   { ChangeFlowerSpriteVariant(FlowerColor.Pink, +1); }
    public void PrevGreenFlowerSprite()  { ChangeFlowerSpriteVariant(FlowerColor.Green, -1); }
    public void NextGreenFlowerSprite()  { ChangeFlowerSpriteVariant(FlowerColor.Green, +1); }

#if UNITY_EDITOR
    private void OnValidate()
    {
        GameResources resources = GameResources.Instance;
        bool hasSharedSpriteLibrary = resources != null && resources.SpriteLibrary != null;
        bool hasSharedSpriteData = resources != null && resources.SpriteData != null;

        if (backButton == null)
            Debug.LogWarning("[SettingsMenuController] Back button is not assigned.", this);

        if (flowerSelectorRows != null && flowerSelectorRows.Length > 0)
        {
            if (flowerSpriteLibrary == null && !hasSharedSpriteLibrary)
                Debug.LogWarning("[SettingsMenuController] Flower sprite library is missing while selector rows are configured.", this);

            if (flowerSpriteData == null && !hasSharedSpriteData)
                Debug.LogWarning("[SettingsMenuController] Flower sprite data is missing while selector rows are configured.", this);
        }
    }
#endif
}

