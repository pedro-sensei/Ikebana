using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

//=^..^=   =^..^=   VERSION 1.0.2 (April 2026)    =^..^=    =^..^=
//                    Last Update 01/04/2026 
//=^..^=    =^..^=  By Pedro Sánchez Vázquez      =^..^=    =^..^=


// View for a full player board.
// Each board sits in a fixed visual slot (slotIndex). When the turn changes
// it swaps which model player it shows so that slot 0 is always the active player.
//
// displayedPlayerIndex = (currentPlayerIndex + slotIndex) % numPlayers
public class PlayerBoardView : MonoBehaviour
{
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

    [Header("Active Player Indicator")]
    [SerializeField] private Image boardBorderImage;
    [SerializeField] private Color activeColor   = new Color(0.2f, 0.8f, 0.2f, 1f);
    [SerializeField] private Color inactiveColor = new Color(0.5f, 0.5f, 0.5f, 1f);

    // which model player this board is currently showing
    private int displayedPlayerIndex = 0;

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
        GameEvents.OnTurnTransition       += HandleTurnTransition;
        GameEvents.OnTurnStart            += HandleTurnStart;
        GameEvents.OnScoringTurnChanged   += HandleScoringTurnChanged;
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

        int numPlayers = gc.Model.NumberOfPlayers;
        int currentPlayer = gc.Model.CurrentPlayerIndex;
        SwitchDisplayedPlayer(currentPlayer, numPlayers);
    }

    private void OnDestroy()
    {
        GameEvents.OnTurnTransition       -= HandleTurnTransition;
        GameEvents.OnTurnStart            -= HandleTurnStart;
        GameEvents.OnScoringTurnChanged   -= HandleScoringTurnChanged;
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

        SwitchDisplayedPlayer(playerIndex, gc.Model.NumberOfPlayers);
    }

    private void HandleScoringTurnChanged(int scoringPlayerIndex, int numPlayers)
    {
        // rotate boards same way as placement so the scoring player
        // is in the active slot
        SwitchDisplayedPlayer(scoringPlayerIndex, numPlayers);
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
            boardBorderImage.color = IsActiveSlot ? activeColor : inactiveColor;

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
