using UnityEngine;
using UnityEngine.UI;
using TMPro;

//=^..^=   =^..^=   VERSION 1.0.2 (April 2026)    =^..^=    =^..^=
//                    Last Update 01/04/2026 
//=^..^=    =^..^=  By Pedro Sánchez Vázquez      =^..^=    =^..^=

// Controls the Credits panel.
// Displays credits text and a Back button.

public class CreditsMenuController : MonoBehaviour
{
    [Header("Navigation")]
    [SerializeField] private MainMenuController mainMenuController;
    [SerializeField] private Button backButton;

    [Header("Content")]
    [Tooltip("TextMeshPro element that holds the credits text.")]
    [SerializeField] private TextMeshProUGUI creditsText;

    [Tooltip("Optional: ScrollRect that allows the player to scroll through credits.")]
    [SerializeField] private ScrollRect scrollRect;

    [Header("Auto Scroll")]
    [Tooltip("Speed at which credits scroll automatically (0 = no auto scroll).")]
    [SerializeField] private float autoScrollSpeed = 20f;

    private void Awake()
    {
        if (backButton != null)
            backButton.onClick.AddListener(OnBackClicked);
    }

    private void OnEnable()
    {
        // Reset scroll position to top when panel opens
        if (scrollRect != null)
            scrollRect.verticalNormalizedPosition = 1f;
    }

    private void Update()
    {
        if (autoScrollSpeed > 0f && scrollRect != null)
        {
            float step = autoScrollSpeed * Time.deltaTime / scrollRect.content.rect.height;
            scrollRect.verticalNormalizedPosition -= step;

            if (scrollRect.verticalNormalizedPosition <= 0f)
                scrollRect.verticalNormalizedPosition = 0f;
        }
    }

    private void OnBackClicked()
    {
        if (mainMenuController != null)
            mainMenuController.OnBackToMainMenu();
    }
}
