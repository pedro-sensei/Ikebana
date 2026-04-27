using System;
using System.Collections.Generic;
using UnityEngine;

//=^..^=   =^..^=   VERSION 1.1.0 (April 2026)    =^..^=    =^..^=
//                    Last Update 21/04/2026 
//=^..^=    =^..^=  By Pedro Sánchez Vázquez      =^..^=    =^..^=
/// 
// Global static events. Any script can subscribe to these.
// Firing methods at the bottom so the GameController doesnt have to
// access the event fields directly.
public static class GameEvents
{
    #region EVENT DECLARATIONS

    // GAME
    public static event Action OnGameStart;
    public static event Action OnGameOver;
    public static event Action OnGamePaused;
    public static event Action OnGameResumed;

    // ROUND
    public static event Action<int> OnRoundStart;
    public static event Action<int> OnRoundEnd;
    public static event Action<RoundPhaseEnum> OnPhaseStart;
    public static event Action<RoundPhaseEnum> OnPhaseEnd;

    // TURN - the pipeline goes: End -> Transition -> Start
    public static event Action<int, int> OnTurnEnd;            // playerIndex, turnNumber
    public static event Action<int, int> OnTurnTransition;     // nextPlayerIndex, numPlayers
    public static event Action<int, int> OnTurnStart;          // playerIndex, turnNumber
    public static event Action<int, int> OnTurnChanged;        // legacy, kept for old subscribers

    // HUMAN SELECTION (click flow)
    public static event Action<DisplayView, FlowerColor, List<int>, bool> OnflowerSelected;
    public static event Action OnSelectionCleared;

    // BAG AND DISCARD
    public static event Action OnBagRefilled;
    public static event Action<int> OnBagCountChanged;
    public static event Action<int> OnDiscardCountChanged;
    public static event Action OnBagShuffled;
    public static event Action<IReadOnlyList<FlowerPiece>> OnDiscardChanged;
    public static event Action<IReadOnlyList<FlowerPiece>> OnBagReceivedFromDiscard;

    // FACTORIES AND CENTRAL
    public static event Action<int, IReadOnlyList<FlowerPiece>> OnDisplayFilled;
    public static event Action<int, FlowerColor> OnFactoryPicked;
    public static event Action<FlowerColor> OnCentralPicked;
    public static event Action<IReadOnlyList<FlowerPiece>> OnCentralDisplayChanged;
    public static event Action<int> OnFactoryCleared;
    public static event Action OnCentralCleared;
    public static event Action<IReadOnlyList<FlowerPiece>> OnCentralReceivedFromFactories;
    public static event Action<IReadOnlyList<FlowerPiece>> OnCentralReceivedFromBag;
    public static event Action OnCentralReceivedStartingPlayerToken;
    public static event Action OnCentralFirstPlayerTokenTaken;

    // PLAYER BOARD
    public static event Action<int, int, List<FlowerPiece>> OnflowersPlacedInLine;
    public static event Action<int, List<FlowerPiece>> OnflowersAddedToPenalty;
    public static event Action<int> OnFirstPlayerTokenReceived;
    public static event Action<int, int> OnPlacementLineFilled;
    public static event Action<int> OnPenaltyLineFilled;
    public static event Action<int> OnPlayerTurnStart;

    // SCORING
    public static event Action<int, GridPlacementResult> OnflowerScoredInGrid;
    public static event Action<int, int> OnPenaltyApplied;
    public static event Action<int, int> OnScoreChanged;
    public static event Action<int> OnPlayerScoringComplete;
    public static event Action<int, List<string>> OnEndGameBonusesApplied;
    public static event Action<int, List<GridPlacementResult>> OnScoringComplete;
    public static event Action<int, int> OnScoringTurnChanged;  // playerIndex, numPlayers

    // flower ANIMATION (AI and human)
    // onComplete fires after all ghost flowers have landed
    public static event Action<Vector3, Vector3, Vector3, Vector3,
                               List<FlowerPiece>, List<FlowerPiece>, bool,
                               Action> OnflowerAnimationRequested;

    public static event Action<int> OnAIAnimationStart;
    public static event Action OnAIAnimationEnd;
    public static event Action<int> OnAIThinkingStart;
    public static event Action<int> OnAIThinkingEnd;

    // ANNOUNCEMENTS
    public static event Action<int, int, int, int, BonusType> OnEndGameBonusCellScored;
    public static event Action<string, float> OnShowAnnouncement;
    public static event Action OnHideAnnouncement;
    public static event Action<string, int> OnShowWinner;


    //SETTINGS
    public static event Action OnMusicVolumeChanged;
    public static event Action OnSFXVolumeChanged;

    #endregion

    #region EVENT FIRING METHODS

    // Game
    public static void GameStart() { OnGameStart?.Invoke(); }
    public static void GameOver()  { OnGameOver?.Invoke(); }
    public static void GamePaused()  { OnGamePaused?.Invoke(); }
    public static void GameResumed() { OnGameResumed?.Invoke(); }

    // Round
    public static void RoundStart(int n)          { OnRoundStart?.Invoke(n); }
    public static void RoundEnd(int n)            { OnRoundEnd?.Invoke(n); }
    public static void PhaseStart(RoundPhaseEnum p) { OnPhaseStart?.Invoke(p); }
    public static void PhaseEnd(RoundPhaseEnum p)   { OnPhaseEnd?.Invoke(p); }

    // Turn
    public static void TurnEnd(int pi, int tn) { OnTurnEnd?.Invoke(pi, tn); }

    public static void TurnTransition(int pi, int np)
    {
        // Newer UI listens to TurnTransition, while some older code still expects TurnChanged.
        // We only fire the new one here and keep the legacy helper available for manual callers.
        OnTurnTransition?.Invoke(pi, np);
        // also fire the old event so old subscribers still work
        //OnTurnChanged?.Invoke(pi, np);
    }

    public static void TurnStart(int pi, int tn)  { OnTurnStart?.Invoke(pi, tn); }
    public static void TurnChanged(int pi, int np) { OnTurnChanged?.Invoke(pi, np); }

    // Selection
    public static void FlowerSelected(DisplayView src, FlowerColor c, List<int> lines, bool pen)
    {
        OnflowerSelected?.Invoke(src, c, lines, pen);
    }

    public static void SelectionCleared() { OnSelectionCleared?.Invoke(); }

    // Bag / Discard
    public static void BagRefilled()       { OnBagRefilled?.Invoke(); }
    public static void BagCountChanged(int c)     { OnBagCountChanged?.Invoke(c); }
    public static void DiscardCountChanged(int c)  { OnDiscardCountChanged?.Invoke(c); }
    public static void BagShuffled()       { OnBagShuffled?.Invoke(); }
    public static void DiscardChanged(IReadOnlyList<FlowerPiece> t) { OnDiscardChanged?.Invoke(t); }
    public static void BagReceivedFromDiscard(IReadOnlyList<FlowerPiece> t) { OnBagReceivedFromDiscard?.Invoke(t); }

    // Displays / Central
    public static void DisplayFilled(int i, IReadOnlyList<FlowerPiece> t) { OnDisplayFilled?.Invoke(i, t); }
    public static void FactoryPicked(int i, FlowerColor c) { OnFactoryPicked?.Invoke(i, c); }
    public static void CentralPicked(FlowerColor c) { OnCentralPicked?.Invoke(c); }
    public static void CentralDisplayChanged(IReadOnlyList<FlowerPiece> t) { OnCentralDisplayChanged?.Invoke(t); }
    public static void FactoryCleared(int i) { OnFactoryCleared?.Invoke(i); }
    public static void CentralCleared() { OnCentralCleared?.Invoke(); }
    public static void CentralReceivedFromFactories(IReadOnlyList<FlowerPiece> t) { OnCentralReceivedFromFactories?.Invoke(t); }
    public static void CentralReceivedFromBag(IReadOnlyList<FlowerPiece> t) { OnCentralReceivedFromBag?.Invoke(t); }
    public static void CentralReceivedStartingPlayerToken() { OnCentralReceivedStartingPlayerToken?.Invoke(); }
    public static void CentralFirstPlayerTokenTaken() { OnCentralFirstPlayerTokenTaken?.Invoke(); }

    // Player board
    public static void flowersPlacedInLine(int p, int l, List<FlowerPiece> t) { OnflowersPlacedInLine?.Invoke(p, l, t); }
    public static void flowersAddedToPenalty(int p, List<FlowerPiece> t) { OnflowersAddedToPenalty?.Invoke(p, t); }
    public static void FirstPlayerTokenReceived(int p) { OnFirstPlayerTokenReceived?.Invoke(p); }
    public static void PlacementLineFilled(int p, int l) { OnPlacementLineFilled?.Invoke(p, l); }
    public static void PenaltyLineFilled(int p)   { OnPenaltyLineFilled?.Invoke(p); }
    public static void PlayerTurnStart(int p) { OnPlayerTurnStart?.Invoke(p); }

    // Scoring
    public static void flowerScoredInGrid(int p, GridPlacementResult r) { OnflowerScoredInGrid?.Invoke(p, r); }
    public static void PenaltyApplied(int p, int v) { OnPenaltyApplied?.Invoke(p, v); }
    public static void ScoreChanged(int p, int s)  { OnScoreChanged?.Invoke(p, s); }
    public static void PlayerScoringComplete(int p) { OnPlayerScoringComplete?.Invoke(p); }
    public static void EndGameBonusesApplied(int p, List<string> b) { OnEndGameBonusesApplied?.Invoke(p, b); }
    public static void ScoringComplete(int p, List<GridPlacementResult> r) { OnScoringComplete?.Invoke(p, r); }
    public static void ScoringTurnChanged(int p, int n) { OnScoringTurnChanged?.Invoke(p, n); }

    // flower animation
    public static void flowerAnimationRequested(
        Vector3 from, Vector3 to, Vector3 central, Vector3 penalty,
        List<FlowerPiece> picked, List<FlowerPiece> remaining,
        bool hasToken, Action onComplete)
    {
        OnflowerAnimationRequested?.Invoke(from, to, central, penalty, picked, remaining, hasToken, onComplete);
    }

    public static void AIflowerAnimationRequested(
        Vector3 from, Vector3 to, Vector3 central, Vector3 penalty,
        List<FlowerPiece> picked, List<FlowerPiece> remaining,
        bool hasToken, Action onComplete)
    {
        // Thin wrapper kept for readability at call sites that specifically talk about AI animation.
        flowerAnimationRequested(from, to, central, penalty, picked, remaining, hasToken, onComplete);
    }

    public static void AIAnimationStart(int idx) { OnAIAnimationStart?.Invoke(idx); }
    public static void AIAnimationEnd()          { OnAIAnimationEnd?.Invoke(); }
    public static void AIThinkingStart(int idx)  { OnAIThinkingStart?.Invoke(idx); }
    public static void AIThinkingEnd(int idx)    { OnAIThinkingEnd?.Invoke(idx); }

    // Announcements / UI
    public static void EndGameBonusCellScored(int p, int r, int c, int bonus, BonusType t)
    {
        OnEndGameBonusCellScored?.Invoke(p, r, c, bonus, t);
    }

    public static void ShowAnnouncement(string msg, float dur) { OnShowAnnouncement?.Invoke(msg, dur); }
    public static void HideAnnouncement() { OnHideAnnouncement?.Invoke(); }
    public static void ShowWinner(string name, int score) { OnShowWinner?.Invoke(name, score); }

    // Settings / Audio
    public static void MusicVolumeChanged() { OnMusicVolumeChanged?.Invoke(); }
    public static void SFXVolumeChanged()   { OnSFXVolumeChanged?.Invoke(); }

    #endregion
}

