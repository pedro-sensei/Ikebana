using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.Serialization;
//=^..^=   =^..^=   VERSION 1.1.0 (April 2026)    =^..^=    =^..^=
//                    Last Update 21/04/2026 
//=^..^=    =^..^=  By Pedro Sánchez Vázquez      =^..^=    =^..^=

/// Controls the "New Game" setup menu.

public class NewGameMenuController : MonoBehaviour
{


    [Header("Data")]
    [SerializeField] private GameSetupData setupData;
    [SerializeField] private PortraitRepository portraitDatabase;
    [SerializeField] private AIOpponentRepository opponentRepository;

    [Header("Player Count")]
    [SerializeField] private TMP_Dropdown playerCountDropdown;

    [Header("Player Slot UI (index 0-3, enable/disable based on count)")]
    [SerializeField] private PlayerSlotUI[] playerSlotUIs;

    [Header("Buttons")]
    [SerializeField] private Button startGameButton;
    [SerializeField] private Button backButton;

    [Header("Navigation")]
    [Tooltip("Reference to MainMenuController to call OnBackToMainMenu.")]
    [SerializeField] private MainMenuController mainMenuController;

  

    private int _selectedPlayerCount = 2;
    private bool[] _isAISlot;
    private int[] _selectedColorIndices;
    private int[] _portraitIndices;

    private static readonly Color[] PlayerColors = new Color[]
    {
        new Color(0.23f, 0.51f, 0.96f), // Blue
        new Color(0.92f, 0.26f, 0.26f), // Red
        new Color(0.18f, 0.72f, 0.34f), // Green
        new Color(0.96f, 0.78f, 0.16f), // Yellow
        new Color(0.58f, 0.34f, 0.92f), // Purple
        new Color(0.96f, 0.47f, 0.16f), // Orange
        new Color(0.12f, 0.74f, 0.78f), // Cyan
        new Color(0.95f, 0.22f, 0.63f), // Pink
        new Color(0.44f, 0.30f, 0.18f), // Brown
        new Color(0.55f, 0.55f, 0.60f), // Gray
        new Color(0.50f, 0.00f, 0.50f), // Magenta
        new Color(0.00f, 0.50f, 0.50f), // Teal
        new Color(0.40f, 0.20f, 0.00f), // Maroon
        new Color(0.00f, 0.20f, 0.60f), // Navy
        new Color(0.50f, 0.50f, 0.00f), // Olive
        new Color(0.00f, 0.40f, 0.00f)  // Dark Green
    };


    private void Awake()
    {
        _isAISlot = new bool[4];
        _selectedColorIndices = new int[4];
        _portraitIndices = new int[4];

        if (playerCountDropdown != null)
        {
            playerCountDropdown.ClearOptions();
            playerCountDropdown.AddOptions(new System.Collections.Generic.List<string> { "2 Players", "3 Players", "4 Players" });
            playerCountDropdown.onValueChanged.AddListener(OnPlayerCountChanged);
        }

        if (startGameButton != null) startGameButton.onClick.AddListener(OnStartGameClicked);
        if (backButton != null)      backButton.onClick.AddListener(OnBackClicked);

        // Hook up per-slot listeners
        for (int i = 0; i < playerSlotUIs.Length; i++)
        {
            int idx = i; // capture for closure
            PlayerSlotUI slot = playerSlotUIs[i];
            if (slot.HumanAIToggle != null)
                slot.HumanAIToggle.onValueChanged.AddListener(isAI => OnHumanAIToggled(idx, isAI));
            if (slot.OpponentDropdown != null)
                slot.OpponentDropdown.onValueChanged.AddListener(_ => OnOpponentChanged(idx));
            if (slot.ColorSelectorImage != null)
                slot.ColorSelectorImage.onClick.AddListener(() => OnColorSelectorClicked(idx));
            if (slot.PortraitNextButton != null)
                slot.PortraitNextButton.onClick.AddListener(() => OnPortraitCycled(idx, +1));
            if (slot.PortraitPrevButton != null)
                slot.PortraitPrevButton.onClick.AddListener(() => OnPortraitCycled(idx, -1));
        }
    }

    private void OnEnable()
    {
        // Reset to defaults when the panel opens
        _selectedPlayerCount = 2;
        if (playerCountDropdown != null) playerCountDropdown.value = 0;
        RefreshSlotVisibility();
        PopulateDefaults();
        PopulateOpponentDropdowns();
    }

   
    private void OnPlayerCountChanged(int dropdownIndex)
    {
        _selectedPlayerCount = dropdownIndex + 2; // dropdown 0=2p, 1=3p, 2=4p
        RefreshSlotVisibility();
    }

    private void OnHumanAIToggled(int slotIndex, bool isAI)
    {
        if (slotIndex >= playerSlotUIs.Length) return;

        _isAISlot[slotIndex] = isAI;

        PlayerSlotUI slot = playerSlotUIs[slotIndex];
        if (slot.OpponentDropdown != null)
            slot.OpponentDropdown.gameObject.SetActive(isAI);

        if (isAI) ApplyOpponentPresetToSlot(slotIndex);
    }

    private void OnOpponentChanged(int slotIndex)
    {
        ApplyOpponentPresetToSlot(slotIndex);
    }

    private void OnColorSelectorClicked(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= _selectedColorIndices.Length) return;

        if (PlayerColors.Length <= 0) return;

        int current = Mathf.Clamp(_selectedColorIndices[slotIndex], 0, PlayerColors.Length - 1);
        int next = (current + 1) % PlayerColors.Length;
        _selectedColorIndices[slotIndex] = next;
        ApplyColorToSlotVisual(slotIndex);
    }

    private void OnPortraitCycled(int slotIndex, int direction)
    {
        if (portraitDatabase == null || portraitDatabase.Count == 0) return;

        _portraitIndices[slotIndex] = (_portraitIndices[slotIndex] + direction + portraitDatabase.Count) % portraitDatabase.Count;
        Sprite portrait = portraitDatabase.GetPortrait(_portraitIndices[slotIndex]);

        if (slotIndex < playerSlotUIs.Length && playerSlotUIs[slotIndex].PortraitImage != null)
            playerSlotUIs[slotIndex].PortraitImage.sprite = portrait;
    }

    private void OnStartGameClicked()
    {
        WriteSetupData();

        string sceneName = setupData.GetSceneForPlayerCount();
        SceneManager.LoadScene(sceneName);
    }

    private void OnBackClicked()
    {
        if (mainMenuController != null)
            mainMenuController.OnBackToMainMenu();
    }

  

    private void RefreshSlotVisibility()
    {
        for (int i = 0; i < playerSlotUIs.Length; i++)
        {
            bool active = i < _selectedPlayerCount;
            if (playerSlotUIs[i].Root != null)
                playerSlotUIs[i].Root.SetActive(active);
        }
    }

    private void PopulateOpponentDropdowns()
    {
        bool hasRepo = opponentRepository != null && opponentRepository.Count > 0;
        List<string> names = hasRepo
            ? opponentRepository.GetNames()
            : new List<string> { "No Opponents" };

        for (int i = 0; i < playerSlotUIs.Length; i++)
        {
            PlayerSlotUI slot = playerSlotUIs[i];

            if (slot.OpponentDropdown != null)
            {
                slot.OpponentDropdown.ClearOptions();
                slot.OpponentDropdown.AddOptions(names);
                slot.OpponentDropdown.SetValueWithoutNotify(0);
                slot.OpponentDropdown.gameObject.SetActive(_isAISlot[i]);
            }

        }
    }

    private void PopulateDefaults()
    {
        bool hasRepo = opponentRepository != null && opponentRepository.Count > 0;

        for (int i = 0; i < playerSlotUIs.Length; i++)
        {
            PlayerSlotUI slot = playerSlotUIs[i];

            if (slot.NameInput != null)
                slot.NameInput.text = "Player " + (i + 1);

            if (slot.HumanAIToggle != null)
            {
                slot.HumanAIToggle.isOn = false; // default human
                _isAISlot[i] = false;

                if (slot.OpponentDropdown != null)
                    slot.OpponentDropdown.gameObject.SetActive(false);
            }

            int defaultColorIndex = Mathf.Clamp(i, 0, PlayerColors.Length - 1);
            _selectedColorIndices[i] = defaultColorIndex;
            ApplyColorToSlotVisual(i);

            // Default portrait
            _portraitIndices[i] = i % Mathf.Max(1, portraitDatabase != null ? portraitDatabase.Count : 1);
            if (portraitDatabase != null && slot.PortraitImage != null)
                slot.PortraitImage.sprite = portraitDatabase.GetPortrait(_portraitIndices[i]);
        }
    }

    private void ApplyOpponentPresetToSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= playerSlotUIs.Length) return;
        if (!_isAISlot[slotIndex]) return;
        if (opponentRepository == null || opponentRepository.Count == 0) return;

        PlayerSlotUI slot = playerSlotUIs[slotIndex];
        if (slot.OpponentDropdown == null) return;

        AIOpponentData opp = opponentRepository.Get(slot.OpponentDropdown.value);
        if (opp == null) return;

        if (slot.NameInput != null)
        {
            string currentName = slot.NameInput.text;
            bool useDefaultName = string.IsNullOrEmpty(currentName) || currentName.StartsWith("Player ");
            if (useDefaultName && !string.IsNullOrEmpty(opp.OpponentName))
                slot.NameInput.text = opp.OpponentName;
        }

        if (slot.PortraitImage != null && opp.Portrait != null)
        {
            // Only apply opponent portrait as default when slot has no portrait yet.
            if (slot.PortraitImage.sprite == null)
                slot.PortraitImage.sprite = opp.Portrait;
        }

        if (slot.PortraitImage != null)
        {
            int portraitIndex = FindPortraitIndex(slot.PortraitImage.sprite);
            if (portraitIndex >= 0) _portraitIndices[slotIndex] = portraitIndex;
        }
    }

    private int FindPortraitIndex(Sprite portrait)
    {
        if (portrait == null || portraitDatabase == null) return -1;
        for (int i = 0; i < portraitDatabase.Count; i++)
        {
            if (portraitDatabase.GetPortrait(i) == portrait)
                return i;
        }
        return -1;
    }

   
    private void WriteSetupData()
    {
        setupData.PlayerCount = _selectedPlayerCount;
        setupData.IsLoadingFromSave = false;
        setupData.SavedGameJson = null;
        setupData.PlayerSlots = new PlayerSlotConfig[_selectedPlayerCount];

        bool hasRepo = opponentRepository != null && opponentRepository.Count > 0;

        for (int i = 0; i < _selectedPlayerCount; i++)
        {
            PlayerSlotUI slot = playerSlotUIs[i];
            PlayerSlotConfig cfg = new PlayerSlotConfig();

            cfg.PlayerName = slot.NameInput != null ? slot.NameInput.text : "Player " + (i + 1);
            cfg.PlayerColor = PlayerColors[Mathf.Clamp(_selectedColorIndices[i], 0, PlayerColors.Length - 1)];

            bool isAI = _isAISlot[i];
            cfg.PlayerType = isAI ? PlayerType.AI : PlayerType.Human;

            if (isAI)
            {
                if (hasRepo && slot.OpponentDropdown != null)
                {
                    AIOpponentData opponent = opponentRepository.Get(slot.OpponentDropdown.value);
                    if (opponent != null)
                    {
                        PlayerSlotConfig oppConfig = opponent.ToPlayerSlotConfig();
                        cfg.AIBrain = oppConfig.AIBrain;
                        cfg.OptimizerWeights = oppConfig.OptimizerWeights;
                        cfg.EmilyEarlyWeights = oppConfig.EmilyEarlyWeights;
                        cfg.EmilyMidWeights = oppConfig.EmilyMidWeights;
                        cfg.EmilyLateWeights = oppConfig.EmilyLateWeights;
                        cfg.MinMaxDepth = oppConfig.MinMaxDepth;
                        cfg.MinMaxTimeLimitMs = oppConfig.MinMaxTimeLimitMs;
                        cfg.MinMaxEvaluator = oppConfig.MinMaxEvaluator;
                        cfg.RelativeImmediateSimulationWeight = oppConfig.RelativeImmediateSimulationWeight;
                        cfg.RelativeTerminalDeltaWeight = oppConfig.RelativeTerminalDeltaWeight;
                        cfg.RelativeExpectedMoveWeight = oppConfig.RelativeExpectedMoveWeight;
                        cfg.RelativeUsePhaseAwareImmediateWeight = oppConfig.RelativeUsePhaseAwareImmediateWeight;
                        cfg.MLAgentModel = oppConfig.MLAgentModel;
                        if (opponent.Portrait != null)
                            cfg.Portrait = opponent.Portrait;
                    }
                }
                else
                    cfg.AIBrain = AIBrainType.Random;
            }

            if (slot.PortraitImage != null && slot.PortraitImage.sprite != null)
                cfg.Portrait = slot.PortraitImage.sprite;
            else if (portraitDatabase != null)
                cfg.Portrait = portraitDatabase.GetPortrait(_portraitIndices[i]);

            setupData.PlayerSlots[i] = cfg;
        }

        setupData.SaveRuntimeSnapshot();
    }

    private void ApplyColorToSlotVisual(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= playerSlotUIs.Length) return;
        PlayerSlotUI slot = playerSlotUIs[slotIndex];
        Color c = PlayerColors[Mathf.Clamp(_selectedColorIndices[slotIndex], 0, PlayerColors.Length - 1)];

        if (slot.ColorPreview != null)
            slot.ColorPreview.color = c;

        if (slot.ColorSelectorImage != null)
        {
            Image selectorImage = slot.ColorSelectorImage.GetComponent<Image>();
            if (selectorImage != null)
                selectorImage.color = c;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (setupData == null)
            Debug.LogWarning("[NewGameMenuController] GameSetupData reference is missing.", this);

        if (playerCountDropdown == null)
            Debug.LogWarning("[NewGameMenuController] Player count dropdown is not assigned.", this);

        if (startGameButton == null)
            Debug.LogWarning("[NewGameMenuController] Start game button is not assigned.", this);

        if (backButton == null)
            Debug.LogWarning("[NewGameMenuController] Back button is not assigned.", this);

        if (playerSlotUIs == null || playerSlotUIs.Length == 0)
            Debug.LogWarning("[NewGameMenuController] Player slot UI array is empty.", this);
        else if (playerSlotUIs.Length < 4)
            Debug.LogWarning("[NewGameMenuController] Player slot UI array should usually contain 4 slots.", this);
    }
#endif
}

//Player panel in the UI.
[Serializable]
public struct PlayerSlotUI
{
    [Tooltip("Root GameObject of this slot (enabled/disabled based on player count).")]
    public GameObject Root;

    [Tooltip("Input field for the player name.")]
    public TMP_InputField NameInput;

    [Tooltip("Toggle: OFF = Human, ON = AI.")]
    public Toggle HumanAIToggle;

    [Tooltip("Dropdown to select a pre-configured opponent from the repository.")]
    public TMP_Dropdown OpponentDropdown;

    [FormerlySerializedAs("ColorDropdown")]
    [Tooltip("Button/Image used as random color selector (click to randomize player highlight color).")]
    public Button ColorSelectorImage;

    [Tooltip("Visual preview for selected player color.")]
    public Image ColorPreview;

    [FormerlySerializedAs("PortraitButton")]
    [Tooltip("Button to cycle portraits forward.")]
    public Button PortraitNextButton;

    [Tooltip("Button to cycle portraits backward.")]
    public Button PortraitPrevButton;

    [Tooltip("Image that displays the current portrait.")]
    public Image PortraitImage;
}
