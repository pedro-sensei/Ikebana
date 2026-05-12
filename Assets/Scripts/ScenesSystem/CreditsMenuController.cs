using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine;

//=^..^=   =^..^=   VERSION 1.1.0 (April 2026)    =^..^=    =^..^=
//                    Last Update 21/04/2026 
//=^..^=    =^..^=  By Pedro Sánchez Vázquez      =^..^=    =^..^=

// Controls the Credits panel.
// Displays credits text and a Back button.

public class CreditsMenuController : MonoBehaviour
{
    [Header("Navigation")]
    [SerializeField] private MainMenuController mainMenuController;
    [SerializeField] private Button backButton;

    [Header("Content")]
    [Tooltip("Credits text.")]
    [SerializeField] private TextMeshProUGUI creditsText;

    [Tooltip("Scroll through credits.")]
    [SerializeField] private ScrollRect scrollRect;

    [Header("Auto Scroll")]
    [Tooltip("Speed for credits scroll automatically (0 = no auto scroll).")]
    [SerializeField] private float autoScrollSpeed = 20f;

    [Tooltip("Auto scroll waits after the player scrolls manually.")]
    [SerializeField] private float resumeAutoScrollDelay = 2f;

    private float autoScrollResumeTimer;
    private bool isApplyingAutoScroll;

    private void Awake()
    {
        if (backButton != null)
            backButton.onClick.AddListener(OnBackClicked);

        if (scrollRect != null)
            scrollRect.onValueChanged.AddListener(OnScrollValueChanged);
    }

    private void OnDestroy()
    {
        if (backButton != null)
            backButton.onClick.RemoveListener(OnBackClicked);

        if (scrollRect != null)
            scrollRect.onValueChanged.RemoveListener(OnScrollValueChanged);
    }

    private void OnEnable()
    {
        RefreshScrollLayout();
        SetScrollPosition(1f);
        autoScrollResumeTimer = 0f;
    }

    private void Update()
    {
        if (autoScrollResumeTimer > 0f)
        {
            autoScrollResumeTimer -= Time.unscaledDeltaTime;
            return;
        }

        ApplyAutoScroll();
    }

    private void OnBackClicked()
    {
        if (mainMenuController != null)
            mainMenuController.OnBackToMainMenu();
    }

    private void RefreshScrollLayout()
    {
        if (scrollRect == null)
            return;

        Canvas.ForceUpdateCanvases();

        if (creditsText != null)
            creditsText.ForceMeshUpdate();

        if (scrollRect.content != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRect.content);

        if (scrollRect.viewport != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRect.viewport);

        Canvas.ForceUpdateCanvases();
    }

    private void ApplyAutoScroll()
    {
        if (autoScrollSpeed <= 0f || scrollRect == null || scrollRect.content == null || scrollRect.viewport == null)
            return;

        float hiddenHeight = scrollRect.content.rect.height - scrollRect.viewport.rect.height;
        if (hiddenHeight <= 0f)
            return;

        float nextPosition = scrollRect.verticalNormalizedPosition - (autoScrollSpeed * Time.unscaledDeltaTime / hiddenHeight);
        SetScrollPosition(nextPosition);
    }

    private void SetScrollPosition(float normalizedPosition)
    {
        if (scrollRect == null)
            return;

        isApplyingAutoScroll = true;
        scrollRect.verticalNormalizedPosition = Mathf.Clamp01(normalizedPosition);
        isApplyingAutoScroll = false;
    }

    private void OnScrollValueChanged(Vector2 _)
    {
        if (isApplyingAutoScroll)
            return;

        autoScrollResumeTimer = resumeAutoScrollDelay;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        GameResources resources = GameResources.Instance;
        bool hasSharedMainMenuController = resources != null && resources.MainMenuControllerRef != null;

        if (backButton == null)
            Debug.LogWarning("[CreditsMenuController] Back button is not assigned.", this);

        if (mainMenuController == null && !hasSharedMainMenuController)
            Debug.LogWarning("[CreditsMenuController] MainMenuController reference is not assigned.", this);

        if (scrollRect == null)
            Debug.LogWarning("[CreditsMenuController] ScrollRect is not assigned.", this);

        if (autoScrollSpeed < 0f)
            autoScrollSpeed = 0f;

        if (resumeAutoScrollDelay < 0f)
            resumeAutoScrollDelay = 0f;
    }
#endif
}
