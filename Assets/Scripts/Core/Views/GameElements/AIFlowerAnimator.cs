using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using System;

//=^..^=   =^..^=   VERSION 1.1.1 (April 2026)    =^..^=    =^..^=
//                    Last Update 27/04/2026 
//=^..^=    =^..^=  By Pedro Sánchez Vázquez      =^..^=    =^..^=

// Handles all AI flower flight animations:
//   A: pickedflowers fly from source to target placement line.
//   B: remainingflowers fly from factory to central display.
//      Empty for central picks since those flowers stay put.
//   Token: when picking from central and hasFirstPlayerToken is true, 
//          the token flies from central to the penalty line.
// Each ghost uses the right sprite for its FlowerColor.
// onCompleteAnimation fires after the last flower lands.
// Fires OnAIAnimationStart before spawning ghosts 
// and OnAIAnimationEnd after all have landed.
public class AIFlowerAnimator : MonoBehaviour
{
    #region FIELDS AND PROPERTIES
    [Header("Animation Settings")]
    [Tooltip("Base duration of each flower flight.")]
    [SerializeField] private float flyDuration = 0.30f;
    [Tooltip("Base delay between consecutive flowers in the same group.")]
    [SerializeField] private float delayBetweenflowers = 0.06f;

    // Scaled values (base * multiplier from settings)
    private float _flyDuration;
    private float _delayBetweenflowers;

    private Canvas GetDragCanvas()
    {
        if (GameResources.Instance != null)
            return GameResources.Instance.DragCanvas;
        return null;
    }
    #endregion

    #region CONSTRUCTOR AND INITIALIZATION
    private void Awake()
    {
        float multiplier = PlayerPrefs.GetFloat(GameController.AnimationDurationPrefKey, GameController.AnimationDurationDefault);
        SetAnimationMultiplier(multiplier);
        GameEvents.OnflowerAnimationRequested += HandleAnimationRequested;
    }

    private void OnDestroy()
    {
        GameEvents.OnflowerAnimationRequested -= HandleAnimationRequested;
    }

    /// Scales flight duration and stagger delay 
    public void SetAnimationMultiplier(float multiplier)
    {
        multiplier      = Mathf.Clamp(multiplier, GameController.AnimationDurationMin, GameController.AnimationDurationMax);
        _flyDuration         = flyDuration          * multiplier;
        _delayBetweenflowers = delayBetweenflowers  * multiplier;
    }

    #endregion

    #region ANIMATION LOGIC
    private void HandleAnimationRequested(
        Vector3 sourcePos, 
        Vector3 targetPos, 
        Vector3 centralPos, 
        Vector3 penaltyPos,
        List<FlowerPiece> pickedflowers, 
        List<FlowerPiece> remainingflowers,
        bool hasFirstPlayerToken,
        System.Action onComplete)
    {
        StartCoroutine(PlayBatchAnimation(
            sourcePos, targetPos, centralPos, penaltyPos,
            pickedflowers, remainingflowers, hasFirstPlayerToken, onComplete));
    }

    private System.Collections.IEnumerator PlayBatchAnimation(
        Vector3 sourcePos, Vector3 targetPos, Vector3 centralPos, Vector3 penaltyPos,
        List<FlowerPiece> pickedflowers, List<FlowerPiece> remainingflowers,
        bool hasFirstPlayerToken,
        Action onCompleteAnimation)
    {
        //Get sprite data for flower colors and token
        FlowerSpriteData spriteData = null;
        if (GameResources.Instance != null)
            spriteData = GameResources.Instance.SpriteData;

        // Call event to freeze displays and block user input. (-2) is ignored.
        GameEvents.AIAnimationStart(-2);

        //Counter to track how many animations are still running
        int pending = 0;

        // A: picked flowers fly to placement line
        for (int i = 0; i < pickedflowers.Count; i++)
        {
            pending++;
            Sprite sprite = spriteData != null ? spriteData.GetSprite(pickedflowers[i].Color) : null;
            GameObject ghost = CreateGhost(sprite, sourcePos, 1f);
            float delay = i * _delayBetweenflowers;

            GameObject capturedGhost = ghost;
            Vector3 capturedTarget = targetPos;

            ghost.transform
                .DOMove(targetPos, _flyDuration)
                .SetDelay(delay)
                .SetEase(Ease.InOutQuad)
                .OnComplete(delegate
                {
                    Destroy(capturedGhost);
                    TriggerLandingAnimation(capturedTarget);
                    pending--;
                });
        }

        // B: remaining flowers go to central (factory picks only)
        for (int i = 0; i < remainingflowers.Count; i++)
        {
            pending++;
            Sprite sprite = spriteData != null ? spriteData.GetSprite(remainingflowers[i].Color) : null;
            GameObject ghost = CreateGhost(sprite, sourcePos, 0.70f);
            float delay = i * _delayBetweenflowers;

            GameObject capturedGhost = ghost;

            ghost.transform
                .DOMove(centralPos, _flyDuration)
                .SetDelay(delay)
                .SetEase(Ease.InOutQuad)
                .OnComplete(delegate
                {
                    Destroy(capturedGhost);
                    //TODO: TriggerLandingAnimationCentral(centralPos);
                    pending--;
                });
        }

        // Token: first player token goes to penalty (central picks only)
        if (hasFirstPlayerToken)
        {
            pending++;
            Sprite tokenSprite = spriteData != null ? spriteData.GetFirstPlayerTokenSprite() : null;
            float tokenDelay = pickedflowers.Count * _delayBetweenflowers;
            GameObject tokenGhost = CreateGhost(tokenSprite, sourcePos, 1f);

            GameObject capturedToken = tokenGhost;

            tokenGhost.transform
                .DOMove(penaltyPos, _flyDuration)
                .SetDelay(tokenDelay)
                .SetEase(Ease.InOutQuad)
                .OnComplete(delegate
                {
                    Destroy(capturedToken);
                    pending--;
                });
        }

        // wait until all animations finish
        while (pending > 0)
            yield return null;

        // Unfreeze views first, then fire the callback that lets gameplay continue.
        // That ordering prevents the controller from applying the move while the UI is still visually frozen.
        GameEvents.AIAnimationEnd();
        if (onCompleteAnimation != null)
            onCompleteAnimation.Invoke();
    }

    // Creates a ghost flower GameObject with an Image.
    private GameObject CreateGhost(Sprite sprite, Vector3 worldPos, float alpha)
    {
        Canvas dragCanvas = GetDragCanvas();

        GameObject ghost = new GameObject("AI_Ghost");
        ghost.transform.SetParent(dragCanvas.transform, false);
        ghost.transform.SetAsLastSibling();
        ghost.transform.position = worldPos;

        RectTransform rt = ghost.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(72, 72);

        Image img = ghost.AddComponent<Image>();
        img.raycastTarget = false;
        if (sprite != null) img.sprite = sprite;
        img.color = new Color(1f, 1f, 1f, alpha);

        return ghost;
    }

    // Find the closest placement line view and trigger its landing anim.
    private void TriggerLandingAnimation(Vector3 landWorldPos)
    {
        float closestPlacement = float.MaxValue;
        PlacementLineView bestView = null;

        PlacementLineView[] allViews = FindObjectsByType<PlacementLineView>(FindObjectsSortMode.None);
        for (int i = 0; i < allViews.Length; i++)
        {
            float d = Vector3.Distance(allViews[i].transform.position, landWorldPos);
            if (d < closestPlacement)
            {
                closestPlacement = d;
                bestView = allViews[i];
            }
        }

        PenaltyLineView[] penaltyViews = FindObjectsByType<PenaltyLineView>(FindObjectsSortMode.None);
        for (int i = 0; i < penaltyViews.Length; i++)
        {
            float d = Vector3.Distance(penaltyViews[i].transform.position, landWorldPos);
            if (d < closestPlacement)
                return; // penalty is closer, skip placement line animation
        }

        if (bestView != null)
            bestView.AnimateLand();
    }

    #endregion
}
