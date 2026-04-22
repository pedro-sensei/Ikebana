using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

//=^..^=   =^..^=   VERSION 1.1.0 (April 2026)    =^..^=    =^..^=
//                    Last Update 21/04/2026 
//=^..^=    =^..^=  By Pedro Sánchez Vázquez      =^..^=    =^..^=


// View for a full player board.
// Each board sits in a fixed visual slot (slotIndex). When the turn changes
// it swaps which model player it shows so that slot 0 is always the active player.
//
// displayedPlayerIndex = (currentPlayerIndex + slotIndex) % numPlayers
public class PlayerBoardView : MonoBehaviour
{
    private const string BoardRotationPrefKey = "EnableBoardRotation";

    [Header("Configuration")]
    [Tooltip("Fixed position of this board in the UI. 0 = active player slot.")]
    [SerializeField] private int slotIndex = 0;

    [Header("References")]
    [SerializeField] private PlacementLineView[] placementLineViews;
    [SerializeField] private PenaltyLineView penaltyLineView;
    [SerializeField] private PlayerGridView gridView;
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private TextMeshProUGUI playerNameText;
    [SerializeField] private Image portraitImage;
    [SerializeField] private Transform aiThinkingWobbleTarget;

    [Header("Active Player Indicator")]
    [SerializeField] private Image boardBorderImage;
    [SerializeField] private Color activeColor   = new Color(0.2f, 0.8f, 0.2f, 1f);
    [SerializeField] private Color inactiveColor = new Color(0.5f, 0.5f, 0.5f, 1f);

    [Header("Animations")]
    [SerializeField] private float boardTransitionCloneDuration = 0.2f;
    [SerializeField] private float boardTransitionCloneAlpha = 0.9f;
    [SerializeField] private float portraitThinkingWobbleAngle = 8f;
    [SerializeField] private float portraitThinkingDuration = 0.12f;
    [SerializeField] private float portraitThinkingSpeedMultiplier = 1f;

    // which model player this board is currently showing
    private int displayedPlayerIndex = 0;
    private RectTransform _rectTransform;
    private Tween _boardTransitionTween;
    private Tween _portraitThinkingTween;
    private Quaternion _portraitBaseLocalRotation;
    private bool _portraitBaseRotationCaptured;

    // slot 0 is always the active (interactable) board
    private bool IsActiveSlot
    {
        get { return slotIndex == 0; }
    }

    public int PlayerIndex
    {
        get { return displayedPlayerIndex; }
    }
    public int SlotIndex
    {
        get { return slotIndex; }
    }
    public PlacementLineView[] PlacementLineViews
    {
        get { return placementLineViews; }
    }
    public PenaltyLineView PenaltyLineView
    {
        get { return penaltyLineView; }
    }

    private void Awake()
    {
        AutoResolvePortraitReferences();

        _rectTransform = GetComponent<RectTransform>();
        if (_rectTransform != null)
            ViewRegistry.RegisterPlayerBoard(slotIndex, _rectTransform);

        GameEvents.OnTurnTransition       += HandleTurnTransition;
        GameEvents.OnTurnStart            += HandleTurnStart;
        GameEvents.OnScoringTurnChanged   += HandleScoringTurnChanged;
        GameEvents.OnAIThinkingStart      += HandleAIThinkingStart;
        GameEvents.OnAIThinkingEnd        += HandleAIThinkingEnd;
        GameEvents.OnflowersPlacedInLine    += HandleflowersPlacedInLine;
        GameEvents.OnflowersAddedToPenalty  += HandleflowersAddedToPenalty;
        GameEvents.OnflowerScoredInGrid     += HandleflowerScoredInGrid;
        GameEvents.OnScoreChanged         += HandleScoreChanged;
        GameEvents.OnFirstPlayerTokenReceived += HandleFirstPlayerToken;
        GameEvents.OnRoundStart           += HandleRoundStarted;
        GameEvents.OnPlayerScoringComplete += HandlePlayerScoringComplete;
        GameEvents.OnGameOver             += HandleGameOver;
    }

    private void Start()
    {
        GameController gc = null;
        if (GameResources.Instance != null && GameResources.Instance.GameController != null)
            gc = GameResources.Instance.GameController;
        else
            gc = GameController.Instance;

        if (gc == null) return;

        RefreshBoardRotationMode();
    }

    private void OnDestroy()
    {
        if (_boardTransitionTween != null) _boardTransitionTween.Kill();
        if (_portraitThinkingTween != null) _portraitThinkingTween.Kill();

        ViewRegistry.UnregisterPlayerBoard(slotIndex);

        GameEvents.OnTurnTransition       -= HandleTurnTransition;
        GameEvents.OnTurnStart            -= HandleTurnStart;
        GameEvents.OnScoringTurnChanged   -= HandleScoringTurnChanged;
        GameEvents.OnAIThinkingStart      -= HandleAIThinkingStart;
        GameEvents.OnAIThinkingEnd        -= HandleAIThinkingEnd;
        GameEvents.OnflowersPlacedInLine    -= HandleflowersPlacedInLine;
        GameEvents.OnflowersAddedToPenalty  -= HandleflowersAddedToPenalty;
        GameEvents.OnflowerScoredInGrid     -= HandleflowerScoredInGrid;
        GameEvents.OnScoreChanged         -= HandleScoreChanged;
        GameEvents.OnFirstPlayerTokenReceived -= HandleFirstPlayerToken;
        GameEvents.OnRoundStart           -= HandleRoundStarted;
        GameEvents.OnPlayerScoringComplete -= HandlePlayerScoringComplete;
        GameEvents.OnGameOver             -= HandleGameOver;
    }

    // === EVENT HANDLERS ===

    private void HandleTurnTransition(int nextPlayerIndex, int numPlayers)
    {
        if (!ShouldRotateForPlacement()) return;

        PlayTurnTransitionClone(nextPlayerIndex, numPlayers);
        SwitchDisplayedPlayer(nextPlayerIndex, numPlayers);
    }

    private void HandleTurnStart(int playerIndex, int turnNumber)
    {
        GameController gc = null;
        if (GameResources.Instance != null && GameResources.Instance.GameController != null)
            gc = GameResources.Instance.GameController;
        else
            gc = GameController.Instance;
        if (gc == null) return;
        if (!ShouldRotateForPlacement()) return;

        int numPlayers = gc.Model.NumberOfPlayers;
        int expected = (playerIndex + slotIndex) % numPlayers;
        if (displayedPlayerIndex != expected)
            SwitchDisplayedPlayer(playerIndex, numPlayers);
    }

    private void HandleScoringTurnChanged(int scoringPlayerIndex, int numPlayers)
    {
        // During scoring, rotation should still happen even if board rotation
        // is disabled for placement turns.
        SwitchDisplayedPlayer(scoringPlayerIndex, numPlayers);
    }

    private void HandleAIThinkingStart(int pIndex)
    {
        if (pIndex != displayedPlayerIndex) return;

        Transform target = GetAIThinkingWobbleTarget();
        if (target == null) return;

        if (!_portraitBaseRotationCaptured)
        {
            _portraitBaseLocalRotation = target.localRotation;
            _portraitBaseRotationCaptured = true;
        }

        if (_portraitThinkingTween != null) _portraitThinkingTween.Kill();
        target.localRotation = _portraitBaseLocalRotation;

        float d = GetEffectivePortraitThinkingDuration();

        Sequence wobble = DOTween.Sequence();
        wobble.Append(target.DOLocalRotate(new Vector3(0f, 0f, portraitThinkingWobbleAngle), d)
            .SetRelative()
            .SetEase(Ease.InOutSine));
        wobble.Append(target.DOLocalRotate(new Vector3(0f, 0f, -portraitThinkingWobbleAngle * 2f), d * 2f)
            .SetRelative()
            .SetEase(Ease.InOutSine));
        wobble.Append(target.DOLocalRotate(new Vector3(0f, 0f, portraitThinkingWobbleAngle), d)
            .SetRelative()
            .SetEase(Ease.InOutSine));

        _portraitThinkingTween = wobble
            .SetLoops(-1, LoopType.Restart)
            .SetEase(Ease.Linear);
    }

    private void HandleAIThinkingEnd(int pIndex)
    {
        if (pIndex != displayedPlayerIndex || portraitImage == null) return;
        StopPortraitThinkingAnimation();
    }

    private void StopPortraitThinkingAnimation()
    {
        if (_portraitThinkingTween != null)
        {
            _portraitThinkingTween.Kill();
            _portraitThinkingTween = null;
        }

        Transform target = GetAIThinkingWobbleTarget();
        if (target != null)
        {
            if (!_portraitBaseRotationCaptured)
            {
                _portraitBaseLocalRotation = target.localRotation;
                _portraitBaseRotationCaptured = true;
            }
            target.localRotation = _portraitBaseLocalRotation;
        }
    }

    private Transform GetAIThinkingWobbleTarget()
    {
        if (aiThinkingWobbleTarget != null)
            return aiThinkingWobbleTarget;

        if (portraitImage != null)
            return portraitImage.transform;

        return null;
    }

    private float GetEffectivePortraitThinkingDuration()
    {
        float speed = Mathf.Max(0.05f, portraitThinkingSpeedMultiplier);
        return portraitThinkingDuration / speed;
    }

    private void AutoResolvePortraitReferences()
    {
        if (portraitImage == null)
        {
            Image[] images = GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                string n = images[i].name.ToLowerInvariant();
                if (n.Contains("portrait"))
                {
                    portraitImage = images[i];
                    break;
                }
            }
        }

        if (aiThinkingWobbleTarget == null && portraitImage != null)
            aiThinkingWobbleTarget = portraitImage.transform;
    }

    private void PlayTurnTransitionClone(int nextPlayerIndex, int numPlayers)
    {
        if (_rectTransform == null || numPlayers <= 1) return;

        int oldDisplayed = displayedPlayerIndex;
        int destinationSlot = (oldDisplayed - nextPlayerIndex + numPlayers) % numPlayers;

        if (destinationSlot == slotIndex) return;

        RectTransform destinationRt;
        if (!ViewRegistry.GetPlayerBoardPosition(destinationSlot, out destinationRt) || destinationRt == null)
            return;

        RectTransform cloneRt = CreateTransitionCloneRect();
        if (cloneRt == null) return;

        cloneRt.anchoredPosition = _rectTransform.anchoredPosition;
        cloneRt.SetAsLastSibling();

        if (_boardTransitionTween != null) _boardTransitionTween.Kill();

        Vector3 targetWorldPos = destinationRt.position;
        Vector2 targetSize = destinationRt.rect.size;
        Vector3 targetScale = destinationRt.localScale;

        Sequence seq = DOTween.Sequence();
        seq.Join(cloneRt.DOMove(targetWorldPos, boardTransitionCloneDuration).SetEase(Ease.OutCubic));
        seq.Join(cloneRt.DOSizeDelta(targetSize, boardTransitionCloneDuration).SetEase(Ease.OutCubic));
        seq.Join(cloneRt.DOScale(targetScale, boardTransitionCloneDuration).SetEase(Ease.OutCubic));
        seq.OnComplete(delegate
        {
            _boardTransitionTween = null;
            if (cloneRt != null) Destroy(cloneRt.gameObject);
        });

        _boardTransitionTween = seq;
    }

    private RectTransform CreateTransitionCloneRect()
    {
        if (_rectTransform == null) return null;

        GameObject ghost = new GameObject("BoardTransitionClone_" + slotIndex,
            typeof(RectTransform), typeof(CanvasGroup));
        ghost.transform.SetParent(_rectTransform.parent, false);

        RectTransform ghostRt = ghost.GetComponent<RectTransform>();
        CopyRectTransform(_rectTransform, ghostRt);
        ghostRt.anchoredPosition = _rectTransform.anchoredPosition;

        CanvasGroup cg = ghost.GetComponent<CanvasGroup>();
        cg.alpha = boardTransitionCloneAlpha;
        cg.interactable = false;
        cg.blocksRaycasts = false;

        CopyGraphicsOnTransform(_rectTransform, ghostRt.gameObject);
        CloneVisualChildrenRecursive(_rectTransform, ghostRt);

        return ghostRt;
    }

    private static void CloneVisualChildrenRecursive(Transform sourceParent, Transform destinationParent)
    {
        for (int i = 0; i < sourceParent.childCount; i++)
        {
            Transform srcChild = sourceParent.GetChild(i);
            RectTransform srcRect = srcChild as RectTransform;
            if (srcRect == null) continue;

            GameObject clone = new GameObject(srcChild.name, typeof(RectTransform));
            clone.transform.SetParent(destinationParent, false);
            clone.SetActive(srcChild.gameObject.activeSelf);

            RectTransform cloneRect = clone.GetComponent<RectTransform>();
            CopyRectTransform(srcRect, cloneRect);

            CopyGraphicsOnTransform(srcChild, clone);
            CloneVisualChildrenRecursive(srcChild, clone.transform);
        }
    }

    private static void CopyRectTransform(RectTransform src, RectTransform dst)
    {
        dst.anchorMin = src.anchorMin;
        dst.anchorMax = src.anchorMax;
        dst.anchoredPosition = src.anchoredPosition;
        dst.sizeDelta = src.sizeDelta;
        dst.pivot = src.pivot;
        dst.localScale = src.localScale;
        dst.localRotation = src.localRotation;
    }

    private static void CopyGraphicsOnTransform(Transform source, GameObject target)
    {
        Image srcImage = source.GetComponent<Image>();
        if (srcImage != null)
        {
            Image dstImage = target.AddComponent<Image>();
            dstImage.sprite = srcImage.sprite;
            dstImage.type = srcImage.type;
            dstImage.color = srcImage.color;
            dstImage.material = srcImage.material;
            dstImage.preserveAspect = srcImage.preserveAspect;
            dstImage.raycastTarget = false;
            dstImage.fillCenter = srcImage.fillCenter;
            dstImage.fillMethod = srcImage.fillMethod;
            dstImage.fillAmount = srcImage.fillAmount;
            dstImage.fillClockwise = srcImage.fillClockwise;
            dstImage.fillOrigin = srcImage.fillOrigin;
        }

        TextMeshProUGUI srcTmp = source.GetComponent<TextMeshProUGUI>();
        if (srcTmp != null)
        {
            TextMeshProUGUI dstTmp = target.AddComponent<TextMeshProUGUI>();
            dstTmp.text = srcTmp.text;
            dstTmp.font = srcTmp.font;
            dstTmp.fontSize = srcTmp.fontSize;
            dstTmp.fontStyle = srcTmp.fontStyle;
            dstTmp.color = srcTmp.color;
            dstTmp.alignment = srcTmp.alignment;
            dstTmp.raycastTarget = false;
            dstTmp.enableWordWrapping = srcTmp.enableWordWrapping;
            dstTmp.overflowMode = srcTmp.overflowMode;
        }
    }

    public void RefreshBoardRotationMode()
    {
        GameController gc = null;
        if (GameResources.Instance != null && GameResources.Instance.GameController != null)
            gc = GameResources.Instance.GameController;
        else
            gc = GameController.Instance;
        if (gc == null) return;

        int numPlayers = gc.Model.NumberOfPlayers;
        if (ShouldRotateForPlacement())
        {
            SwitchDisplayedPlayer(gc.Model.CurrentPlayerIndex, numPlayers);
        }
        else
        {
            int singleHumanIndex = FindSingleHumanPlayerIndex(gc);
            int fixedPlayerIndex;
            if (singleHumanIndex >= 0)
            {
                // One-human mode with rotation disabled:
                // keep human always in big board (slot 0), regardless of who starts.
                fixedPlayerIndex = (singleHumanIndex + slotIndex) % numPlayers;
            }
            else
            {
                fixedPlayerIndex = slotIndex;
                if (fixedPlayerIndex < 0) fixedPlayerIndex = 0;
                if (fixedPlayerIndex >= numPlayers) fixedPlayerIndex = numPlayers - 1;
            }

            displayedPlayerIndex = fixedPlayerIndex;
            PropagatePlayerIndex();
            RefreshAll();
            UpdateInteractable();
            UpdateActiveIndicator();
        }
    }

    private bool ShouldRotateForPlacement()
    {
        GameController gc = null;
        if (GameResources.Instance != null && GameResources.Instance.GameController != null)
            gc = GameResources.Instance.GameController;
        else
            gc = GameController.Instance;

        // Multi-human sessions always rotate boards.
        if (gc != null && CountHumanPlayers(gc) > 1)
            return true;

        return PlayerPrefs.GetInt(BoardRotationPrefKey, 1) == 1;
    }

    private static int CountHumanPlayers(GameController gc)
    {
        if (gc == null || gc.PlayerSlots == null) return 0;

        int count = 0;
        for (int i = 0; i < gc.PlayerSlots.Length; i++)
        {
            if (gc.PlayerSlots[i] != null && gc.PlayerSlots[i].PlayerType == PlayerType.Human)
                count++;
        }
        return count;
    }

    private static int FindSingleHumanPlayerIndex(GameController gc)
    {
        if (gc == null || gc.PlayerSlots == null) return -1;

        int humanCount = 0;
        int humanIndex = -1;
        for (int i = 0; i < gc.PlayerSlots.Length; i++)
        {
            if (gc.PlayerSlots[i] != null && gc.PlayerSlots[i].PlayerType == PlayerType.Human)
            {
                humanCount++;
                humanIndex = i;
                if (humanCount > 1) return -1;
            }
        }

        return humanCount == 1 ? humanIndex : -1;
    }

    private void HandleflowersPlacedInLine(int pIndex, int lineIndex, List<FlowerPiece> flowers)
    {
        if (pIndex != displayedPlayerIndex) return;
        // lineIndex is 0-based
        if (placementLineViews != null && lineIndex >= 0 && lineIndex < placementLineViews.Length)
        {
            if (placementLineViews[lineIndex] != null)
                placementLineViews[lineIndex].Refresh();
        }
    }

    private void HandleflowersAddedToPenalty(int pIndex, List<FlowerPiece> flowers)
    {
        if (pIndex != displayedPlayerIndex) return;
        if (penaltyLineView != null)
            penaltyLineView.Refresh();
    }

    private void HandleflowerScoredInGrid(int pIndex, GridPlacementResult result)
    {
        if (pIndex != displayedPlayerIndex) return;
        if (gridView != null)
            gridView.PlaceflowerWithScore(result.Row, result.Column, result.Color, result.PointsScored);
    }

    private void HandleScoreChanged(int pIndex, int newScore)
    {
        if (pIndex != displayedPlayerIndex) return;
        if (scoreText != null)
            scoreText.text = newScore.ToString();
    }

    private void HandlePlayerScoringComplete(int pIndex)
    {
        if (pIndex != displayedPlayerIndex) return;
        RefreshLines();
    }

    private void HandleFirstPlayerToken(int pIndex)
    {
        if (pIndex != displayedPlayerIndex) return;
        if (penaltyLineView != null)
            penaltyLineView.Refresh();
    }

    private void HandleRoundStarted(int round)
    {
        RefreshLines();

        // After scoring rotations, return to normal placement seating mode.
        RefreshBoardRotationMode();
    }

    private void HandleGameOver()
    {
        if (GameController.Instance == null) return;
        if (scoreText != null)
            scoreText.text = GameController.Instance.Model.Players[displayedPlayerIndex].Score.ToString();
    }

    private void SwitchDisplayedPlayer(int currentPlayerIndex, int numPlayers)
    {
        displayedPlayerIndex = (currentPlayerIndex + slotIndex) % numPlayers;

        PropagatePlayerIndex();
        RefreshAll();
        UpdateInteractable();
        UpdateActiveIndicator();
    }

    // Tells child views which player they should show
    private void PropagatePlayerIndex()
    {
        if (placementLineViews != null)
        {
            for (int i = 0; i < placementLineViews.Length; i++)
            {
                if (placementLineViews[i] != null)
                    placementLineViews[i].SetPlayerIndex(displayedPlayerIndex);
            }
        }

        if (penaltyLineView != null)
            penaltyLineView.SetPlayerIndex(displayedPlayerIndex);
        if (gridView != null)
            gridView.SetPlayerIndex(displayedPlayerIndex);
    }

    // Rebuilds every visual to match the displayed player
    private void RefreshAll()
    {
        GameController gc = null;
        if (GameResources.Instance != null && GameResources.Instance.GameController != null)
            gc = GameResources.Instance.GameController;
        else
            gc = GameController.Instance;
        if (gc == null) return;

        PlayerModel player = gc.Model.Players[displayedPlayerIndex];

        if (playerNameText != null)
            playerNameText.text = player.PlayerName;

        if (scoreText != null)
            scoreText.text = player.Score.ToString();

        if (portraitImage != null)
            portraitImage.sprite = gc.GetPortrait(displayedPlayerIndex);

        if (gridView != null)
            gridView.Reinitialize();
        RefreshLines();
    }

    // Refreshes placement lines + penalty line from model
    private void RefreshLines()
    {
        if (placementLineViews != null)
        {
            for (int i = 0; i < placementLineViews.Length; i++)
            {
                if (placementLineViews[i] != null)
                    placementLineViews[i].Refresh();
            }
        }
        if (penaltyLineView != null)
            penaltyLineView.Refresh();
    }

    private void UpdateInteractable()
    {
        bool interactable = IsActiveSlot;
        if (placementLineViews != null)
        {
            for (int i = 0; i < placementLineViews.Length; i++)
            {
                if (placementLineViews[i] != null)
                    placementLineViews[i].SetInteractable(interactable);
            }
        }
        if (penaltyLineView != null)
            penaltyLineView.SetInteractable(interactable);
    }

    private void UpdateActiveIndicator()
    {
        if (boardBorderImage != null)
        {
            Color slotColor = activeColor;
            GameController gc = null;
            if (GameResources.Instance != null && GameResources.Instance.GameController != null)
                gc = GameResources.Instance.GameController;
            else
                gc = GameController.Instance;

            if (gc != null && gc.PlayerSlots != null &&
                displayedPlayerIndex >= 0 && displayedPlayerIndex < gc.PlayerSlots.Length)
            {
                slotColor = gc.PlayerSlots[displayedPlayerIndex].PlayerColor;
            }

            if (IsActiveSlot)
            {
                boardBorderImage.color = new Color(slotColor.r, slotColor.g, slotColor.b, 1f);
            }
            else
            {
                boardBorderImage.color = new Color(slotColor.r, slotColor.g, slotColor.b, 1f);
            }
        }

        if (playerNameText != null)
            playerNameText.fontStyle = IsActiveSlot
                ? FontStyles.Bold | FontStyles.Underline
                : FontStyles.Normal;
    }


    public void SetSlotIndex(int index)
    {
        slotIndex = index;
    }

    public void ForceUpdate()
    {
        GameController gc = null;
        if (GameResources.Instance != null && GameResources.Instance.GameController != null)
            gc = GameResources.Instance.GameController;
        else
            gc = GameController.Instance;
        if (gc == null) return;

        int current = gc.Model.CurrentPlayerIndex;
        int numPlayers = gc.Model.NumberOfPlayers;
        SwitchDisplayedPlayer(current, numPlayers);
    }
}
