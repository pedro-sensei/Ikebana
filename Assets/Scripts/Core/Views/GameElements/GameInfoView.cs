using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;

//=^..^=   =^..^=   VERSION 1.1.0 (April 2026)    =^..^=    =^..^=
//                    Last Update 21/04/2026 
//=^..^=    =^..^=  By Pedro Sánchez Vázquez      =^..^=    =^..^==

// Shows announcement messages during the game (round start, game over, etc.)
public class GameInfoView : MonoBehaviour
{
    [Header("References")]
    [Tooltip("TextMeshProUGUI used to render the message text.")]
    [SerializeField] private TextMeshProUGUI messageText;
    [Tooltip("CanvasGroup on the root panel for alpha fading.")]
    [SerializeField] private CanvasGroup panelGroup;

    [Header("Animation")]
    [SerializeField] private float fadeDuration = 0.3f;

    private Coroutine _currentCoroutine;

    private void Awake()
    {
        //Invisible at start
        SetAlpha(0f);

        if (panelGroup != null)
        {
            panelGroup.blocksRaycasts = false;
            panelGroup.interactable   = false;
        }

        GameEvents.OnShowAnnouncement += HandleShowAnnouncement;
    }

    private void OnDestroy()
    {
        GameEvents.OnShowAnnouncement -= HandleShowAnnouncement;
    }

    private void HandleShowAnnouncement(string message, float displayDuration)
    {
        Show(message, displayDuration);
    }

    // Shows the message for the given duration with fade in/out.
    public void Show(string message, float displayDuration)
    {
        if (_currentCoroutine != null)
           StopCoroutine(_currentCoroutine);
        _currentCoroutine = StartCoroutine(ShowCoroutine(message, displayDuration));
    }

    private IEnumerator ShowCoroutine(string message, float displayDuration)
    {
        if (messageText != null)
            messageText.text = message;

        SetBlocking(true);
        yield return FadeTo(1f);
        yield return new WaitForSeconds(displayDuration);
        yield return FadeTo(0f);
        SetBlocking(false);

        _currentCoroutine = null;
    }

    private void SetBlocking(bool block)
    {
        if (panelGroup != null)
        {
            panelGroup.blocksRaycasts = block;
            panelGroup.interactable   = block;
        }
    }

    private IEnumerator FadeTo(float target)
    {
        if (panelGroup != null)
        {
            float startAlpha = panelGroup.alpha;
            Tween t = DOTween.To(
                    delegate() { return panelGroup.alpha; },
                    delegate(float x) { panelGroup.alpha = x; },
                    target,
                    fadeDuration)
                .SetEase(Ease.InOutQuad);
            yield return t.WaitForCompletion();
        }
        else if (messageText != null)
        {
            yield return messageText
                .DOFade(target, fadeDuration)
                .SetEase(Ease.InOutQuad)
                .WaitForCompletion();
        }
    }

    private void SetAlpha(float alpha)
    {
        if (panelGroup != null)
        {
            panelGroup.alpha = alpha;
        }
        else if (messageText != null)
        {
            Color c = messageText.color;
            c.a = alpha;
            messageText.color = c;
        }
    }
}
