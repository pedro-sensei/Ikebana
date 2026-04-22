using System.Collections;
using System.Collections.Generic;
using UnityEngine;


//=^..^=   =^..^=   VERSION 1.1.0 (April 2026)    =^..^=    =^..^=
//                    Last Update 21/04/2026 
//=^..^=    =^..^=  By Pedro Sánchez Vázquez      =^..^=    =^..^=



// Presenter for the game.
//
// Responsibilities (MVP pattern):
//   1. SETUP   – Reads GameSetupData, initialises GameConfig + GameModel.
//   2. INPUT   – Exposes public action methods (pick/place) called by
//                human drag-handlers and AIPlayerController.
//   3. PRESENT – Listens to GameModel events and re-broadcasts them
//                through GameEvents so views never touch the model.
//

public class GameController : MonoBehaviour
{
    #region FIELDS AND PROPERTIES

    public static GameController Instance { get; private set; }

    [Header("Configuration")]
    [SerializeField] private PlayerSlotConfig[] playerSlots;
    [Tooltip("Game rules configuration.")]
    [SerializeField] private GameConfigData gameConfigData;
    [Tooltip("Shared setup data from the menu scene.")]
    [SerializeField] private GameSetupData setupData;
    [Tooltip("AI-PlayerController")]
    [SerializeField] private AIPlayerController aiPlayerController;

    [Header("Timing")]
    [Tooltip("Seconds after flower placement before the turn advances.")]
    [SerializeField] private float postPlacementDelay = 0.5f;
    [Tooltip("Base seconds the turn announcement is shown (multiplied by the settings slider).")]
    [SerializeField] private float turnAnnouncementDuration = 2f;
    [Tooltip("Base seconds the round announcement is shown (multiplied by the settings slider).")]
    [SerializeField] private float roundAnnouncementDuration = 2f;
    [Tooltip("Base seconds the game-over screen is shown (multiplied by the settings slider).")]
    [SerializeField] private float gameOverAnnouncementDuration = 5f;
    [Tooltip("Base seconds to pause on each player's board before scoring their flowers (multiplied by the settings slider).")]
    [SerializeField] private float scoringBoardRotationDelay = 0.8f;  
    [Tooltip("Base seconds between each individual flower being scored on the grid (multiplied by the settings slider).")]
    [SerializeField] private float scoringflowerDelay = 0.4f;
    [Tooltip("Base seconds to wait after fully scoring one player before moving to the next (multiplied by the settings slider).")]
    [SerializeField] private float scoringBetweenPlayersDelay = 1.2f;

    // Announcement duration multiplier (PlayerPrefs key + slider range)
    public const string AnnouncementDurationPrefKey = "AnnouncementDuration";
    public const float  AnnouncementDurationMin     = 0.5f;
    public const float  AnnouncementDurationMax     = 3f;
    public const float  AnnouncementDurationDefault = 1f;

    // Animation duration multiplier (PlayerPrefs key + slider range)
    public const string AnimationDurationPrefKey    = "AnimationDuration";
    public const float  AnimationDurationMin        = 0.5f;
    public const float  AnimationDurationMax        = 3f;
    public const float  AnimationDurationDefault    = 1f;

    // AI thinking time (PlayerPrefs key + slider range, stored in seconds)
    public const string AIThinkingTimePrefKey       = "AIThinkingTime";
    public const float  AIThinkingTimeMin           = 1f;
    public const float  AIThinkingTimeMax           = 10f;
    public const float  AIThinkingTimeDefault       = 5f;

    // Runtime-scaled live values (base * multiplier)
    private float _turnAnnouncementDuration;
    private float _roundAnnouncementDuration;
    private float _gameOverAnnouncementDuration;
    private float _scoringBoardRotationDelay;
    private float _scoringflowerDelay;
    private float _scoringBetweenPlayersDelay;
    
    //MVP model.
    private GameModel gameModel;

    private bool _endTurnPending = false;
    private bool _roundJustStarted = false;
    private bool _isPaused = false;
    private Coroutine _gameOverMenuCoroutine;

    public GameModel Model => gameModel;
    public bool IsGameOver => gameModel != null && gameModel.IsGameOver;
    public bool IsPaused => _isPaused;
    public PlayerSlotConfig[] PlayerSlots => playerSlots;
    public float TurnAnnouncementDuration => _turnAnnouncementDuration;


    #endregion

    #region SETUP

    private void Awake()
    {
        //Singleton. TODO: Create SingletonMonobehaviour base class.
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;

        ResolveResourcesFromGameResources();

        // Apply saved multipliers from settings menu.
        SetAnnouncementMultiplier(PlayerPrefs.GetFloat(AnnouncementDurationPrefKey, AnnouncementDurationDefault));
        SetAnimationMultiplier(PlayerPrefs.GetFloat(AnimationDurationPrefKey, AnimationDurationDefault));

        // 1. Restore menu / load-game.
        if (setupData != null)
        {
            setupData.RestoreFromSnapshot();
            if (setupData.PlayerSlots != null && setupData.PlayerSlots.Length > 0)
                ApplySetupData(setupData);
        }

        // 2. Initialise GameConfig.
        int numPlayers = playerSlots.Length;
        GameConfig.Initialize(gameConfigData, numPlayers);

        // 3. Build the model.
        string[] names = BuildPlayerNames(numPlayers);
        gameModel = new GameModel(numPlayers, names);
        SubscribeToModelEvents();

        //4. Build the AI brains for AI players.
        if (aiPlayerController != null)
            aiPlayerController.InitializeBrains();
        Debug.Log($"[GameController] Awake complete – {numPlayers} players, " +
                  $"{GameConfig.NUM_DISPLAYS} factories.");
    }

    private void ResolveResourcesFromGameResources()
    {
        GameResources resources = GameResources.Instance;
        if (resources == null) return;

        if (gameConfigData == null) gameConfigData = resources.GameConfigData;
        if (setupData == null) setupData = resources.GameSetupData;
        if (aiPlayerController == null) aiPlayerController = resources.AIPlayerController;
        if (aiPlayerController == null) aiPlayerController = FindFirstObjectByType<AIPlayerController>();
    }

    private void Start()
    {
        Debug.Log($"[GameController] Start – launching game. Displays={gameModel.FactoryDisplays.Length}, BagCount={gameModel.Bag.Count}");
        gameModel.StartGame();
        // Debug for factory fill.
        //for (int i = 0; i < gameModel.FactoryDisplays.Length; i++)
        //   Debug.Log($"[GameController] Factory {i} has {gameModel.FactoryDisplays[i].Count} flowers after StartGame.");
    }

    private void OnDestroy()
    {
        if (_gameOverMenuCoroutine != null)
            StopCoroutine(_gameOverMenuCoroutine);
        UnsubscribeFromModelEvents();
    }

    public void ApplySetupData(GameSetupData data)
    {
        //Skip if not data set.
        if (data == null || data.PlayerSlots == null || data.PlayerSlots.Length == 0) return;

        playerSlots = new PlayerSlotConfig[data.PlayerCount];
        for (int i = 0; i < data.PlayerCount; i++)
        {
            if (i < data.PlayerSlots.Length)
                playerSlots[i] = data.PlayerSlots[i];
            else
                playerSlots[i] = new PlayerSlotConfig
                {
                    PlayerName = "Player " + (i + 1),
                    PlayerType = PlayerType.Human,
                    AIBrain = AIBrainType.Random
                };
        }
    }

    //Restart game
    public void RestartGame()
    {
        int numPlayers = playerSlots != null ? playerSlots.Length : 2;
        GameConfig.Initialize(gameConfigData, numPlayers);

        string[] names = BuildPlayerNames(numPlayers);

        UnsubscribeFromModelEvents();
        gameModel = new GameModel(numPlayers, names);
        SubscribeToModelEvents();
        gameModel.StartGame();

        Debug.Log($"[GameController] Game reinitialized with {numPlayers} players.");
    }

    #endregion

    #region PLAYER ACTIONS

    // Pick flowers of a colour from a factory display and place them.

    public bool PickFromFactoryAndPlace(int factoryIndex, FlowerColor color, int targetLineIndex)
    {
        //Block when not in placement.
        if (_isPaused || gameModel.IsGameOver || gameModel.CurrentPhase != RoundPhaseEnum.PlacementPhase)
            return false;

        PlayerModel currentPlayer = gameModel.GetCurrentPlayer();


        //DEBUG For AI moves.
        if (targetLineIndex >= 0 && !currentPlayer.CanPlaceInLine(targetLineIndex, color))
        {
            Debug.LogWarning($"Cannot place {color} in line {targetLineIndex}");
            return false;
        }

        PickResult pickResult = gameModel.PickFromFactory(factoryIndex, color);

        if (pickResult.Pickedflowers == null || pickResult.Pickedflowers.Count == 0)
        {
            Debug.LogWarning($"No flowers of color {color} in factory {factoryIndex}");
            return false;
        }

        // Notify views 
        GameEvents.FactoryPicked(factoryIndex, color);

        //Notify if leftowver was sent to central.
        if (pickResult.Remainingflowers != null && pickResult.Remainingflowers.Count > 0)
            GameEvents.CentralDisplayChanged(gameModel.CentralDisplay.flowers);

        // Place on board and notify views.
        PlaceFlowersOnBoard(currentPlayer, targetLineIndex, pickResult);

        ScheduleEndTurn();
        return true;
    }

    /// <summary>
    /// Pick flowers of a colour from the central display and place them.
    /// targetLineIndex: 0-based; -1 for penalty.
    /// </summary>
    public bool PickFromCentralAndPlace(FlowerColor color, int targetLineIndex)
    {
        if (_isPaused || gameModel.IsGameOver || gameModel.CurrentPhase != RoundPhaseEnum.PlacementPhase)
            return false;

        PlayerModel currentPlayer = gameModel.GetCurrentPlayer();

        if (targetLineIndex >= 0 && !currentPlayer.CanPlaceInLine(targetLineIndex, color))
        {
            Debug.LogWarning($"Cannot place {color} in line {targetLineIndex}");
            return false;
        }

        PickResult pickResult = gameModel.PickFromCentral(color);

        if (pickResult.Pickedflowers == null || pickResult.Pickedflowers.Count == 0)
        {
            Debug.LogWarning($"No flowers of color {color} in central display");
            return false;
        }

        // Notify views of the pick.
        GameEvents.CentralPicked(color);

        if (pickResult.PickedFirstPlayerToken)
        {
            currentPlayer.ReceiveFirstPlayerToken();
            GameEvents.FirstPlayerTokenReceived(gameModel.CurrentPlayerIndex);
        }

        GameEvents.CentralDisplayChanged(gameModel.CentralDisplay.flowers);

        // Place on board and notify views.
        PlaceFlowersOnBoard(currentPlayer, targetLineIndex, pickResult);

        ScheduleEndTurn();
        return true;
    }

    #endregion

    #region MODEL EVENT HANDLERS

    //ROUND

    private void HandleRoundStart(int round)
    {
        Debug.Log($"[GameController] HandleRoundStart({round}) – pushing {gameModel.FactoryDisplays.Length} factories to views.");

        // Push factory and central contents to views.
        for (int i = 0; i < gameModel.FactoryDisplays.Length; i++)
        {
            var flowers = gameModel.FactoryDisplays[i].flowers;
            Debug.Log($"[GameController]   Factory {i}: {flowers.Count} flowers ? DisplayFilled");
            GameEvents.DisplayFilled(i, flowers);
        }
        GameEvents.CentralDisplayChanged(gameModel.CentralDisplay.flowers);
        GameEvents.BagCountChanged(gameModel.Bag.Count);
        GameEvents.DiscardCountChanged(gameModel.Discard.Count);

        GameEvents.RoundStart(round);

        string msg = round == 1 ? "Game Start - Round 1" : $"Round {round}";
        GameEvents.ShowAnnouncement(msg, _roundAnnouncementDuration);
        _roundJustStarted = true;
    }

    private void HandleRoundEnd(int round) => GameEvents.RoundEnd(round);
    private void HandlePhaseStart(RoundPhaseEnum phase) => GameEvents.PhaseStart(phase);

    // -- Turn lifecycle --------------------------------------------------

    private void HandleTurnStart(int turnNumber, int playerIndex)
    {
        StartCoroutine(TurnTransitionCoroutine(playerIndex, turnNumber));
    }

    private void HandleTurnEnd(int p, int t) => GameEvents.TurnEnd(p, t);

    // Scoring ---------------------------------------------------------

    private void HandleScoringPhaseReady() => StartCoroutine(RunScoringSequence());

    // Game over -------------------------------------------------------

    private void HandleGameOver()
    {
        GameEvents.GameOver();

        int maxScore = -1;
        int winnerIndex = 0;
        for (int i = 0; i < gameModel.Players.Length; i++)
        {
            var p = gameModel.Players[i];
            Debug.Log($"{p.PlayerName}: {p.Score} points");
            if (p.Score > maxScore ||
               (p.Score == maxScore && p.CompletedRows > gameModel.Players[winnerIndex].CompletedRows))
            {
                maxScore = p.Score;
                winnerIndex = i;
            }
        }
        string winnerName = gameModel.Players[winnerIndex].PlayerName;
        Debug.Log($"Game Over! Winner: {winnerName} with {maxScore} points");
        GameEvents.ShowWinner(winnerName, maxScore);
        GameEvents.ShowAnnouncement(
            $"Game Over! Winner: {winnerName} with {maxScore} points",
            _gameOverAnnouncementDuration);

        if (_gameOverMenuCoroutine != null)
            StopCoroutine(_gameOverMenuCoroutine);
        _gameOverMenuCoroutine = StartCoroutine(ShowMenuAfterGameOverDelay());
    }

    private IEnumerator ShowMenuAfterGameOverDelay()
    {
        yield return new WaitForSeconds(_gameOverAnnouncementDuration);


        MainMenuController menu = GameResources.Instance.MainMenuControllerRef;
        if (menu != null)
            menu.OnInGameMenuClicked();

        _gameOverMenuCoroutine = null;
    }

    #endregion

    #region ANIMATION SEQUENCES

    private IEnumerator TurnTransitionCoroutine(int nextPlayerIndex, int turnNumber)
    {
        int numPlayers = gameModel.NumberOfPlayers;
        GameEvents.TurnTransition(nextPlayerIndex, numPlayers);

        if (_roundJustStarted)
        {
            _roundJustStarted = false;
        }
        else
        {
        string playerName = gameModel.Players[nextPlayerIndex].PlayerName;
            GameEvents.ShowAnnouncement($"{playerName}'s Turn Starts", _turnAnnouncementDuration);
        }

        yield return new WaitForSeconds(_turnAnnouncementDuration);
        GameEvents.TurnStart(nextPlayerIndex, turnNumber);
    }

    private IEnumerator RunScoringSequence()
    {
        int numPlayers = gameModel.NumberOfPlayers;
        bool gameEnd = false;

        for (int i = 0; i < numPlayers; i++)
        {
            int previousScore = gameModel.Players[i].Score;
            GameEvents.ScoringTurnChanged(i, numPlayers);
            yield return new WaitForSeconds(_scoringBoardRotationDelay);

            bool playerTriggeredGameEnd = gameModel.ScorePlayer(i, out List<GridPlacementResult> results);
            if (playerTriggeredGameEnd) gameEnd = true;

            foreach (GridPlacementResult result in results)
            {
                GameEvents.flowerScoredInGrid(i, result);
                yield return new WaitForSeconds(_scoringflowerDelay);
            }

            int penalty = gameModel.Players[i].PenaltyLine.CalculatePenalty();
            if (penalty != 0) GameEvents.PenaltyApplied(i, penalty);

            GameEvents.ScoreChanged(i, gameModel.Players[i].Score);
            int scoreIncrease = gameModel.Players[i].Score - previousScore;
            GameEvents.ShowAnnouncement("+" + scoreIncrease + " points",
                _scoringflowerDelay * 1.5f);
            GameEvents.PlayerScoringComplete(i);

            yield return new WaitForSeconds(_scoringBetweenPlayersDelay);
        }

        if (gameEnd)
            yield return StartCoroutine(RunEndGameBonusSequence());

        gameModel.FinalizeScoringPhase(gameEnd);
    }

    private IEnumerator RunEndGameBonusSequence()
    {
        int numPlayers = gameModel.NumberOfPlayers;
        int size = GameConfig.GRID_SIZE;

        for (int p = 0; p < numPlayers; p++)
        {
            GameEvents.ScoringTurnChanged(p, numPlayers);
            yield return new WaitForSeconds(_scoringBoardRotationDelay);

            PlayerGridModel grid = gameModel.Players[p].Grid;
            int bonusPoints = BonusScoringModel.CalculateEndGameBonus(grid);

            // Complete rows
            for (int r = 0; r < size; r++)
            {
                bool complete = true;
                for (int c = 0; c < size; c++)
                    if (!grid.IsOccupied(r, c)) { complete = false; break; }
                if (!complete) continue;
                for (int c = 0; c < size; c++)
                {
                    GameEvents.EndGameBonusCellScored(p, r, c,
                        GameConfig.COMPLETE_LINE_SCORING_BONUS, BonusType.Row);
                    yield return new WaitForSeconds(_scoringflowerDelay * 0.5f);
                }
                yield return new WaitForSeconds(_scoringflowerDelay);
            }

            // Complete columns
            for (int c = 0; c < size; c++)
            {
                bool complete = true;
                for (int r = 0; r < size; r++)
                    if (!grid.IsOccupied(r, c)) { complete = false; break; }
                if (!complete) continue;
                for (int r = 0; r < size; r++)
                {
                    GameEvents.EndGameBonusCellScored(p, r, c,
                        GameConfig.COMPLETE_COLUMN_SCORING_BONUS, BonusType.Column);
                    yield return new WaitForSeconds(_scoringflowerDelay * 0.5f);
                }
                yield return new WaitForSeconds(_scoringflowerDelay);
            }

            // Complete colours
            for (int colorIdx = 0; colorIdx < GameConfig.NUM_COLORS; colorIdx++)
            {
                FlowerColor color = (FlowerColor)colorIdx;
                bool allPlaced = true;
                for (int r = 0; r < size; r++)
                    if (!grid.IsColorPlacedInRow(r, color)) { allPlaced = false; break; }
                if (!allPlaced) continue;
                for (int r = 0; r < size; r++)
                {
                    int col = grid.GetColumnForColor(r, color);
                    GameEvents.EndGameBonusCellScored(p, r, col,
                        GameConfig.COMPLETE_COLOR_SCORING_BONUS, BonusType.Color);
                    yield return new WaitForSeconds(_scoringflowerDelay * 0.5f);
                }
                yield return new WaitForSeconds(_scoringflowerDelay);
            }

            GameEvents.ShowAnnouncement(
                $"Player {gameModel.Players[p].PlayerName} scored: \n +{bonusPoints} bonus points!",
                _scoringflowerDelay * 4f);
            yield return new WaitForSeconds(_scoringBetweenPlayersDelay);
        }
    }

    #endregion

    #region HELPERS


    // Places picked flowers on the player board. 
    // Fires the appropriate GameEvents for view updates.
   
    private void PlaceFlowersOnBoard(PlayerModel player, int lineIndex, PickResult pickResult)
    {
        int pIdx = player.PlayerIndex;

        if (lineIndex < 0)
        {
            List<FlowerPiece> excess = player.PenaltyLine.AddFlowers(pickResult.Pickedflowers);
            GameEvents.flowersAddedToPenalty(pIdx, pickResult.Pickedflowers);

            if (excess.Count > 0)
            {
                gameModel.Discard.AddRange(excess);
                GameEvents.DiscardCountChanged(gameModel.Discard.Count);
            }
        }
        else
        {
            PlaceResult placeResult = player.PlaceflowersInLine(lineIndex, pickResult.Pickedflowers);
            GameEvents.flowersPlacedInLine(pIdx, lineIndex, pickResult.Pickedflowers);

            if (placeResult.OverflowFlowers.Count > 0)
                GameEvents.flowersAddedToPenalty(pIdx, placeResult.OverflowFlowers);

            if (placeResult.Excessflowers != null && placeResult.Excessflowers.Count > 0)
            {
                gameModel.Discard.AddRange(placeResult.Excessflowers);
                GameEvents.DiscardCountChanged(gameModel.Discard.Count);
            }
        }
    }

    private void ScheduleEndTurn()
    {
        if (_endTurnPending) return;
        _endTurnPending = true;
        StartCoroutine(EndTurnCoroutine());
    }

    private IEnumerator EndTurnCoroutine()
    {
        float remainingDelay = postPlacementDelay;
        while (remainingDelay > 0f)
        {
            if (!_isPaused)
                remainingDelay -= Time.unscaledDeltaTime;
            yield return null;
        }

        int endingPlayer = gameModel.CurrentPlayerIndex;
        int endingTurn   = gameModel.TurnNumber;
        GameEvents.TurnEnd(endingPlayer, endingTurn);

        _endTurnPending = false;
        gameModel.EndTurn();
    }

    // Pushes the full current model state to views via GameEvents.
    private void NotifyViewsOfCurrentState()
    {
        for (int i = 0; i < gameModel.FactoryDisplays.Length; i++)
            GameEvents.DisplayFilled(i, gameModel.FactoryDisplays[i].flowers);
        GameEvents.CentralDisplayChanged(gameModel.CentralDisplay.flowers);
        GameEvents.BagCountChanged(gameModel.Bag.Count);
        GameEvents.DiscardCountChanged(gameModel.Discard.Count);
        GameEvents.RoundStart(gameModel.CurrentRound);
        GameEvents.TurnTransition(gameModel.CurrentPlayerIndex, gameModel.NumberOfPlayers);
    }

    private void SubscribeToModelEvents()
    {
        gameModel.OnRoundStart        += HandleRoundStart;
        gameModel.OnRoundEnd          += HandleRoundEnd;
        gameModel.OnPhaseStart        += HandlePhaseStart;
        gameModel.OnTurnStart         += HandleTurnStart;
        gameModel.OnTurnEnd           += HandleTurnEnd;
        gameModel.OnScoringPhaseReady += HandleScoringPhaseReady;
        gameModel.OnGameOver          += HandleGameOver;
    }

    private void UnsubscribeFromModelEvents()
    {
        if (gameModel == null) return;
        gameModel.OnRoundStart        -= HandleRoundStart;
        gameModel.OnRoundEnd          -= HandleRoundEnd;
        gameModel.OnPhaseStart        -= HandlePhaseStart;
        gameModel.OnTurnStart         -= HandleTurnStart;
        gameModel.OnTurnEnd           -= HandleTurnEnd;
        gameModel.OnScoringPhaseReady -= HandleScoringPhaseReady;
        gameModel.OnGameOver          -= HandleGameOver;
    }

    private string[] BuildPlayerNames(int numPlayers)
    {
        string[] names = new string[numPlayers];
        for (int i = 0; i < numPlayers; i++)
        {
            names[i] = (playerSlots != null && i < playerSlots.Length &&
                        !string.IsNullOrEmpty(playerSlots[i].PlayerName))
                ? playerSlots[i].PlayerName
                : $"Player {i + 1}";
        }
        return names;
    }


    public Sprite GetPortrait(int playerIndex)
    {
        if (playerSlots == null || playerIndex < 0 || playerIndex >= playerSlots.Length)
            return null;
        return playerSlots[playerIndex].Portrait;
    }
    #endregion

    #region PAUSE FOR MENU

    public void PauseGame()
    {
        if (_isPaused) return;
        _isPaused = true;
        Time.timeScale = 0f;
        GameEvents.GamePaused();
    }

    public void ResumeGame()
    {
        if (!_isPaused) return;
        _isPaused = false;
        Time.timeScale = 1f;
        GameEvents.GameResumed();
    }

    public void TogglePause()
    {
        if (_isPaused) ResumeGame();
        else PauseGame();
    }

    /// <summary>
    /// Scales all announcement durations by <paramref name="multiplier"/> relative to the
    /// Inspector base values. 1 = original speed, 0.5 = half duration, 2 = double.
    /// </summary>
    public void SetAnnouncementMultiplier(float multiplier)
    {
        multiplier = Mathf.Clamp(multiplier, AnnouncementDurationMin, AnnouncementDurationMax);
        _turnAnnouncementDuration = turnAnnouncementDuration * multiplier;
        _roundAnnouncementDuration = roundAnnouncementDuration * multiplier;
        _gameOverAnnouncementDuration = gameOverAnnouncementDuration * multiplier;
    }

    /// <summary>
    /// Scales all in-game animation / scoring delays by <paramref name="multiplier"/> relative
    /// to the Inspector base values. Also forwards to AIFlowerAnimator if present.
    /// </summary>
    public void SetAnimationMultiplier(float multiplier)
    {
        multiplier = Mathf.Clamp(multiplier, AnimationDurationMin, AnimationDurationMax);
        _scoringBoardRotationDelay = scoringBoardRotationDelay * multiplier;
        _scoringflowerDelay = scoringflowerDelay * multiplier;
        _scoringBetweenPlayersDelay = scoringBetweenPlayersDelay * multiplier;

        AIFlowerAnimator animator = FindObjectOfType<AIFlowerAnimator>();
        if (animator != null)
            animator.SetAnimationMultiplier(multiplier);
    }

    #endregion

    #region VALIDATION
#if UNITY_EDITOR
    private void OnValidate()
    {
        GameResources resources = GameResources.Instance;

        bool hasSharedGameConfigData = resources != null && resources.GameConfigData != null;
        bool hasSharedSetupData = resources != null && resources.GameSetupData != null;
        bool hasSharedAIController = resources != null && resources.AIPlayerController != null;

        if (gameConfigData == null && !hasSharedGameConfigData)
            Debug.LogWarning("[GameController] GameConfigData is missing.", this);

        if (setupData == null && !hasSharedSetupData)
            Debug.LogWarning("[GameController] GameSetupData is missing.", this);

        if (aiPlayerController == null && !hasSharedAIController)
            Debug.LogWarning("[GameController] AIPlayerController reference is missing.", this);

        if (playerSlots == null || playerSlots.Length < 2)
            Debug.LogWarning("[GameController] PlayerSlots should contain at least 2 entries.", this);

        if (postPlacementDelay < 0f) postPlacementDelay = 0f;
        if (turnAnnouncementDuration < 0f) turnAnnouncementDuration = 0f;
        if (roundAnnouncementDuration < 0f) roundAnnouncementDuration = 0f;
        if (gameOverAnnouncementDuration < 0f) gameOverAnnouncementDuration = 0f;
        if (scoringBoardRotationDelay < 0f) scoringBoardRotationDelay = 0f;
        if (scoringflowerDelay < 0f) scoringflowerDelay = 0f;
        if (scoringBetweenPlayersDelay < 0f) scoringBetweenPlayersDelay = 0f;
    }
#endif
    #endregion
}


