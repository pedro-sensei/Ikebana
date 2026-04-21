using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
//=^..^=   =^..^=   VERSION 1.1.0 (April 2026)    =^..^=    =^..^=
//                    Last Update 21/04/2026 
//=^..^=    =^..^=  By Pedro Sánchez Vázquez      =^..^=    =^..^=

/// Controls the "New Game" setup menu.

public class NewGameMenuController : MonoBehaviour
{


    [Header("Data")]
    [SerializeField] private GameSetupData setupData;
    [SerializeField] private PortraitDatabase portraitDatabase;
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
        new Color(0.26f, 0.54f, 0.96f),
        new Color(0.92f, 0.34f, 0.34f),
        new Color(0.29f, 0.73f, 0.36f),
        new Color(0.95f, 0.78f, 0.24f)
    };

    private static readonly string[] PlayerColorNames =
    {
        "Blue",
        "Red",
        "Green",
        "Yellow"
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
            if (slot.ColorDropdown != null)
                slot.ColorDropdown.onValueChanged.AddListener(colorIdx => OnColorChanged(idx, colorIdx));
            if (slot.PortraitButton != null)
                slot.PortraitButton.onClick.AddListener(() => OnPortraitCycled(idx, +1));
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
        if (slot.AIBrainDropdown != null)
            slot.AIBrainDropdown.gameObject.SetActive(isAI && (opponentRepository == null || opponentRepository.Count == 0));

        if (isAI) ApplyOpponentPresetToSlot(slotIndex);
    }

    private void OnOpponentChanged(int slotIndex)
    {
        ApplyOpponentPresetToSlot(slotIndex);
    }

    private void OnColorChanged(int slotIndex, int colorIdx)
    {
        if (slotIndex < 0 || slotIndex >= _selectedColorIndices.Length) return;

        _selectedColorIndices[slotIndex] = Mathf.Clamp(colorIdx, 0, PlayerColors.Length - 1);
        PlayerSlotUI slot = playerSlotUIs[slotIndex];
        if (slot.ColorPreview != null)
            slot.ColorPreview.color = PlayerColors[_selectedColorIndices[slotIndex]];
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

            if (slot.AIBrainDropdown != null)
                slot.AIBrainDropdown.gameObject.SetActive(_isAISlot[i] && !hasRepo);
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
                if (slot.AIBrainDropdown != null)
                    slot.AIBrainDropdown.gameObject.SetActive(false && !hasRepo);
            }

            // Populate AI brain dropdown from the enum
            if (slot.AIBrainDropdown != null)
            {
                slot.AIBrainDropdown.ClearOptions();
                string[] brainNames = Enum.GetNames(typeof(AIBrainType));
                slot.AIBrainDropdown.AddOptions(new System.Collections.Generic.List<string>(brainNames));
                slot.AIBrainDropdown.value = 0;
            }

            if (slot.ColorDropdown != null)
            {
                int defaultColorIndex = Mathf.Clamp(i, 0, PlayerColors.Length - 1);
                _selectedColorIndices[i] = defaultColorIndex;
                slot.ColorDropdown.ClearOptions();
                slot.ColorDropdown.AddOptions(new List<string>(PlayerColorNames));
                slot.ColorDropdown.SetValueWithoutNotify(defaultColorIndex);
            }

            if (slot.ColorPreview != null)
                slot.ColorPreview.color = PlayerColors[_selectedColorIndices[i]];

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
            slot.NameInput.text = string.IsNullOrEmpty(opp.OpponentName) ? slot.NameInput.text : opp.OpponentName;

        if (slot.PortraitImage != null && opp.Portrait != null)
            slot.PortraitImage.sprite = opp.Portrait;

        int portraitIndex = FindPortraitIndex(opp.Portrait);
        if (portraitIndex >= 0) _portraitIndices[slotIndex] = portraitIndex;
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
                else if (slot.AIBrainDropdown != null)
                {
                    string selected = slot.AIBrainDropdown.options[slot.AIBrainDropdown.value].text;
                    if (Enum.TryParse(selected, out AIBrainType brainType))
                        cfg.AIBrain = brainType;
                    else
                        cfg.AIBrain = AIBrainType.Random;
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

    [Tooltip("Dropdown to choose AI brain type. Only visible when AI is selected.")]
    public TMP_Dropdown AIBrainDropdown;

    [Tooltip("Dropdown to choose player color.")]
    public TMP_Dropdown ColorDropdown;

    [Tooltip("Visual preview for selected player color.")]
    public Image ColorPreview;

    [Tooltip("Button to cycle portraits forward.")]
    public Button PortraitButton;

    [Tooltip("Button to cycle portraits backward.")]
    public Button PortraitPrevButton;

    [Tooltip("Image that displays the current portrait.")]
    public Image PortraitImage;
}
