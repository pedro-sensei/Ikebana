using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

//=^..^=   =^..^=   VERSION 1.1.0 (April 2026)    =^..^=    =^..^=
//                    Last Update 21/04/2026 
//=^..^=    =^..^=  By Pedro Sánchez Vázquez      =^..^=    =^..^=

/// Saving system controller for UI.
///
public class InGameSaveController : MonoBehaviour
{
    [Header("UI")]
    [Tooltip("Button to save the game.")]
    [SerializeField] private Button saveButton;

    [Tooltip("Button to return/back from Save menu.")]
    [SerializeField] private Button returnButton;

    [Tooltip("Custom save name field. Leave empty for auto-timestamp.")]
    [SerializeField] private TMP_InputField saveNameInput;

    [Tooltip("Label to show feedback.")]
    [SerializeField] private TextMeshProUGUI feedbackLabel;

    [Header("Settings")]
    [Tooltip("Seconds the feedback message stays visible.")]
    [SerializeField] private float feedbackDuration = 2f;

    private Coroutine _feedbackCoroutine;

    [Header("Navigation")]
    [SerializeField] private MainMenuController mainMenuController;

    private void Awake()
    {
        if (saveButton != null)
            saveButton.onClick.AddListener(OnSaveClicked);
        if (returnButton != null)
            returnButton.onClick.AddListener(OnReturnClicked);

        if (feedbackLabel != null)
            feedbackLabel.gameObject.SetActive(false);
    }

    private void OnReturnClicked()
    {
        if (mainMenuController != null)
            mainMenuController.OnBackToMainMenu();
        else if (gameObject != null)
            gameObject.SetActive(false);
    }

    private void OnSaveClicked()
    {
        if (SaveGameManager.Instance == null)
        {
            Debug.LogWarning("SaveGameManager.Instance is null.");
            ShowFeedback("Save failed!");
            return;
        }

        if (GameController.Instance == null || GameController.Instance.Model == null)
        {
            Debug.LogWarning("No active game to save.");
            ShowFeedback("No game in progress!");
            return;
        }

        string slotName = null;
        if (saveNameInput != null && !string.IsNullOrWhiteSpace(saveNameInput.text))
            slotName = saveNameInput.text.Trim();

        SaveGameManager.Instance.SaveCurrentGame(slotName);
        ShowFeedback("Game Saved!");
    }

    private void ShowFeedback(string message)
    {
        if (feedbackLabel == null) return;

        if (_feedbackCoroutine != null)
            StopCoroutine(_feedbackCoroutine);

        _feedbackCoroutine = StartCoroutine(FeedbackCoroutine(message));
    }

    private IEnumerator FeedbackCoroutine(string message)
    {
        feedbackLabel.text = message;
        feedbackLabel.gameObject.SetActive(true);

        if (saveButton != null) saveButton.interactable = false;

        yield return new WaitForSeconds(feedbackDuration);

        feedbackLabel.gameObject.SetActive(false);
        if (saveButton != null) saveButton.interactable = true;

        _feedbackCoroutine = null;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (saveButton == null)
            Debug.LogWarning("[InGameSaveController] Save button is not assigned.", this);

        if (returnButton == null)
            Debug.LogWarning("[InGameSaveController] Return button is not assigned.", this);

        if (mainMenuController == null)
            Debug.LogWarning("[InGameSaveController] MainMenuController reference is not assigned.", this);

        if (feedbackDuration < 0f)
            feedbackDuration = 0f;
    }
#endif
}
