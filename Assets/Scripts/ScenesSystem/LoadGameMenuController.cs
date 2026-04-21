using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

//=^..^=   =^..^=   VERSION 1.1.0 (April 2026)    =^..^=    =^..^=
//                    Last Update 21/04/2026 
//=^..^=    =^..^=  By Pedro Sánchez Vázquez      =^..^=    =^..^=


/// Controls the "Load Game" menu.
public class LoadGameMenuController : MonoBehaviour
{

    [Header("Data")]
    [SerializeField] private GameSetupData setupData;

    [Header("Navigation")]
    [SerializeField] private MainMenuController mainMenuController;

    [Header("Save List")]
    [Tooltip("Parent transform where buttons are spawned.")]
    [SerializeField] private Transform saveListContent;

    [Tooltip("Prefab for a single save")]
    [SerializeField] private GameObject saveSlotPrefab;

    [Header("Info")]
    [Tooltip("Shows the selected save name.")]
    [SerializeField] private TextMeshProUGUI selectedSaveLabel;

    [Header("Buttons")]
    [SerializeField] private Button loadButton;
    [SerializeField] private Button deleteButton;
    [SerializeField] private Button backButton;



    private string _selectedSave;
    private readonly List<GameObject> _spawnedSlots = new List<GameObject>();



    private void Awake()
    {
        if (loadButton != null)   loadButton.onClick.AddListener(OnLoadClicked);
        if (deleteButton != null) deleteButton.onClick.AddListener(OnDeleteClicked);
        if (backButton != null)   backButton.onClick.AddListener(OnBackClicked);
    }

    private void OnEnable()
    {
        _selectedSave = null;
        RefreshSaveList();
        UpdateSelectionUI();
    }



    private void RefreshSaveList()
    {
        // Destroy previously spawned slots
        foreach (GameObject slot in _spawnedSlots)
        {
            if (slot != null) Destroy(slot);
        }
        _spawnedSlots.Clear();

        if (SaveGameManager.Instance == null)
        {
            Debug.LogWarning("[LoadGameMenuController] SaveGameManager.Instance is null.");
            return;
        }

        string[] saves = SaveGameManager.Instance.GetSaveFileList();

        if (saves == null || saves.Length == 0)
        {
            return;
        }

        foreach (string saveName in saves)
        {
            if (saveSlotPrefab == null || saveListContent == null) break;

            GameObject slotGO = Instantiate(saveSlotPrefab, saveListContent);
            _spawnedSlots.Add(slotGO);

            // Set the label text
            TextMeshProUGUI label = slotGO.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null) label.text = saveName;

            // Hook up button click
            Button btn = slotGO.GetComponent<Button>();
            if (btn == null) btn = slotGO.GetComponentInChildren<Button>();

            if (btn != null)
            {
                string captured = saveName; // capture for closure
                btn.onClick.AddListener(() => OnSaveSlotSelected(captured));
            }
        }
    }


    private void OnSaveSlotSelected(string saveName)
    {
        _selectedSave = saveName;
        UpdateSelectionUI();
    }

    private void OnLoadClicked()
    {
        if (string.IsNullOrEmpty(_selectedSave))
        {
            Debug.LogWarning("[LoadGameMenuController] No save selected.");
            return;
        }

        if (SaveGameManager.Instance == null)
        {
            Debug.LogWarning("[LoadGameMenuController] SaveGameManager.Instance is null.");
            return;
        }

        bool success = SaveGameManager.Instance.LoadGame(_selectedSave);
        if (!success)
        {
            Debug.LogWarning("[LoadGameMenuController] Failed to load save: " + _selectedSave);
            return;
        }

        // Launch the correct game scene based on the loaded player count
        string sceneName = setupData.GetSceneForPlayerCount();
        SceneManager.LoadScene(sceneName);
    }

    private void OnDeleteClicked()
    {
        if (string.IsNullOrEmpty(_selectedSave))
        {
            Debug.LogWarning("[LoadGameMenuController] No save selected to delete.");
            return;
        }

        if (SaveGameManager.Instance != null)
            SaveGameManager.Instance.DeleteSave(_selectedSave);

        _selectedSave = null;
        RefreshSaveList();
        UpdateSelectionUI();
    }

    private void OnBackClicked()
    {
        if (mainMenuController != null)
            mainMenuController.OnBackToMainMenu();
    }

    private void UpdateSelectionUI()
    {
        if (selectedSaveLabel != null)
            selectedSaveLabel.text = string.IsNullOrEmpty(_selectedSave) ? "No save selected" : _selectedSave;

        if (loadButton != null)   loadButton.interactable = !string.IsNullOrEmpty(_selectedSave);
        if (deleteButton != null) deleteButton.interactable = !string.IsNullOrEmpty(_selectedSave);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (setupData == null)
            Debug.LogWarning("[LoadGameMenuController] GameSetupData reference is missing.", this);

        if (saveListContent == null)
            Debug.LogWarning("[LoadGameMenuController] Save list content transform is missing.", this);

        if (saveSlotPrefab == null)
            Debug.LogWarning("[LoadGameMenuController] Save slot prefab is missing.", this);

        if (loadButton == null)
            Debug.LogWarning("[LoadGameMenuController] Load button is not assigned.", this);

        if (deleteButton == null)
            Debug.LogWarning("[LoadGameMenuController] Delete button is not assigned.", this);

        if (backButton == null)
            Debug.LogWarning("[LoadGameMenuController] Back button is not assigned.", this);
    }
#endif
}
