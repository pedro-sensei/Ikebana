using System;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;


//=^..^=   =^..^=   VERSION 1.1.0 (April 2026)    =^..^=    =^..^=
//                    Last Update 21/04/2026 
//=^..^=    =^..^=  By Pedro Sánchez Vázquez      =^..^=    =^..^=


public class GameModel : IGameModel
{

    #region FIELDS AND PROPERTIES


    //Main Board Elements
    private BagModel _bag;
    private DiscardModel _discard;
    private DisplayModel[] _factoryDisplays;
    private DisplayModel _centralDisplay;

    //Players
    private PlayerModel[] _players;
    private int _numberOfPlayers;

    //Round and Turn Information
    private int _currentRound;
    private int _currentPlayerIndex;
    private int _turnNumber;
    private int _totalTurnsPlayed;
    private int _startingPlayerIndex;
    private RoundPhaseEnum _currentPhase;
    private bool _isGameOver;
    private int[] _factoryFillColorCounts;

    //PROPERTIES

    public BagModel Bag => _bag;
    public DiscardModel Discard => _discard;
    public DisplayModel[] FactoryDisplays => _factoryDisplays;
    public DisplayModel CentralDisplay => _centralDisplay;
    public PlayerModel[] Players => _players;
    public int NumberOfPlayers => _numberOfPlayers;
    public int CurrentRound => _currentRound;
    public int CurrentPlayerIndex => _currentPlayerIndex;
    public int TurnNumber => _turnNumber;
    public int TotalTurnsPlayed => _totalTurnsPlayed;
    public int TotalRoundsPlayed => _currentRound;
    public int StartingPlayerIndex => _startingPlayerIndex;
    public RoundPhaseEnum CurrentPhase => _currentPhase;
    public bool IsGameOver => _isGameOver;
    public int[] FactoryFillColorCounts => _factoryFillColorCounts;

    //UNDO SYSTEM
    public GameMove LastMove { get; private set; }
    public GameState StartOfLastRoundGS { get; private set; }
    #endregion

    #region EVENTS
    
    public event Action<int> OnRoundStart;
    public event Action<RoundPhaseEnum> OnPhaseStart;
    public event Action<int> OnRoundEnd;
    public event Action<int, int> OnTurnStart;
    public event Action<int, int> OnTurnEnd;
    public event Action OnGameOver;
    public event Action OnScoringPhaseReady;
    public event Action OnGameStart;

    #endregion

    #region CONSTRUCTORS AND INITIALIZATION

    //Contructor for standard game setup
    public GameModel(int numberOfPlayers, string[] playerNames = null)
    {
        this._numberOfPlayers = numberOfPlayers;
        _currentRound = 0;
        _currentPlayerIndex = 0;
        _totalTurnsPlayed = 0;
        _startingPlayerIndex = UnityEngine.Random.Range(0, numberOfPlayers);
        _isGameOver = false;
        _factoryFillColorCounts = new int[GameConfig.NUM_COLORS];

        //Create main board elements
        _bag = new BagModel();
        _discard = new DiscardModel();
        _factoryDisplays = new DisplayModel[GameConfig.NUM_DISPLAYS];

        for (int i = 0; i < _factoryDisplays.Length; i++)
        {
            // Display index matches the array index (0-based)
            _factoryDisplays[i] = new DisplayModel(i, false);
        }
        //Central display
        _centralDisplay = new DisplayModel(0, true);

        //Create players
        _players = new PlayerModel[numberOfPlayers];
        for (int i = 0; i < numberOfPlayers; i++)
        {
            string name = playerNames != null && i < playerNames.Length 
                ? playerNames[i] : $"Player {i + 1}"; //Default name
            _players[i] = new PlayerModel(i, name);
        }
        Debug.Log($"[GameModel] Created game with {numberOfPlayers} players. Starting player index: {_startingPlayerIndex}");
    }


    //Constructor from serialized game state
    // For loading saved games.
    // This will reconstruct the game state.
    public GameModel(SerializedGameState savedState)
    {
        int savedNumPlayers = savedState.Players.Length;
        int savedNumFactoryDisplays = savedState.FactoryDisplays.Length;

        //Load round and turn information
        _currentRound = savedState.CurrentRound;
        _currentPlayerIndex = savedState.CurrentPlayerIndex;
        _turnNumber = savedState.TurnNumber;
        _totalTurnsPlayed = savedState.TotalTurnsPlayed;
        _startingPlayerIndex = savedState.StartingPlayerIndex;
        _currentPhase = (RoundPhaseEnum)savedState.CurrentPhase;
        _isGameOver = savedState.IsGameOver;
        _factoryFillColorCounts = new int[GameConfig.NUM_COLORS];
        if (savedState.FactoryFillColorCounts != null)
        {
            int copyLength = Mathf.Min(_factoryFillColorCounts.Length, savedState.FactoryFillColorCounts.Length);
            Array.Copy(savedState.FactoryFillColorCounts, _factoryFillColorCounts, copyLength);
        }

        //Load main board elements
        _bag = new BagModel();
        _bag.Initialize(); 
        _bag.Clear();
        for (int i = 0; i < savedState.Bag.Length; i++)
        {
            _bag.AddFlower(new FlowerPiece((FlowerColor)savedState.Bag[i]));
        }

        _discard = new DiscardModel();
        _discard.Initialize();
        _discard.Clear();
        for (int i = 0; i < savedState.Discard.Length; i++)
        {
            _discard.AddFlower(new FlowerPiece((FlowerColor)savedState.Discard[i]));
        }
        _factoryDisplays = new DisplayModel[savedNumFactoryDisplays];
        for (int i = 0; i < _factoryDisplays.Length; i++)
        {
            _factoryDisplays[i] = new DisplayModel(i, false);
            _factoryDisplays[i].Clear();
            for (int j = 0; j < savedState.FactoryDisplays[i].Length; j++)
            {
                _factoryDisplays[i].AddFlower(new FlowerPiece((FlowerColor)savedState.FactoryDisplays[i][j]));
            }
        }
        _centralDisplay = new DisplayModel(0, true);
        _centralDisplay.Clear();
        for (int i = 0; i < savedState.CentralDisplay.Length; i++)
        {
            _centralDisplay.AddFlower(new FlowerPiece((FlowerColor)savedState.CentralDisplay[i]));
        }
        if (savedState.CentralHasFirstPlayerToken)
            _centralDisplay.AddStartingPlayerToken();

        //Load players
        _numberOfPlayers = savedNumPlayers;
        _players = new PlayerModel[_numberOfPlayers];
        for (int i = 0; i < _numberOfPlayers; i++)
        {
            _players[i] = new PlayerModel(i, savedState.Players[i]);
                        
        }

        Debug.Log($"[GameModel] Loaded game from saved state. Round: {_currentRound}, Current Player Index: {_currentPlayerIndex}, Turn: {_turnNumber}, Starting Player Index: {_startingPlayerIndex}, Phase: {_currentPhase}, IsGameOver: {_isGameOver}");

    }

    public void Initialize()
    {
        // Kept for backwards compatibility (save/load path).
        // For new games use StartGame() directly.
        GameSetup();
        Debug.Log($"[GameModel] Initialized game. Round: {_currentRound}, Current Player Index: {_currentPlayerIndex}, Turn: {_turnNumber}, Starting Player Index: {_startingPlayerIndex}, Phase: {_currentPhase}, IsGameOver: {_isGameOver}");
    }
    #endregion

    #region GAME LOGIC

    public void StartGame()
    {
        GameSetup();
        StartNewRound(1);
        Debug.Log($"[GameModel] Game started. Round: {_currentRound}, Current Player Index: {_currentPlayerIndex}, Turn: {_turnNumber}, Starting Player Index: {_startingPlayerIndex}, Phase: {_currentPhase}, IsGameOver: {_isGameOver}");
         OnGameStart?.Invoke();
    }

    public void GameSetup()
    {
        //Standard game setup logic
        //Fill bag with initial flowers
        _bag.Initialize();
        _discard.Initialize();
        _discard.Clear();

        _totalTurnsPlayed = 0;
        if (_factoryFillColorCounts == null || _factoryFillColorCounts.Length != GameConfig.NUM_COLORS)
            _factoryFillColorCounts = new int[GameConfig.NUM_COLORS];
        Array.Clear(_factoryFillColorCounts, 0, _factoryFillColorCounts.Length);

        //Clear factory displays and central display
        for (int i = 0; i < _factoryDisplays.Length; i++)
        {
            _factoryDisplays[i].Clear();
        }
        _centralDisplay.Clear();
        //Setup players
        foreach (var player in _players)
        {
            player.ResetForNewGame();
        }
        Debug.Log($"[GameModel] Game setup completed. Bag count: {_bag.Count}, Discard count: {_discard.Count}, Factory displays cleared, Central display cleared, Players reset.");
    }


    //Starts a new game round.
    //Round number should be managed externally
    public void StartRound()
    {
        StartNewRound(_currentRound);
        Debug.Log($"[GameModel] Started round {_currentRound}. Current Player Index: {_currentPlayerIndex}, Turn: {_turnNumber}, Starting Player Index: {_startingPlayerIndex}, Phase: {_currentPhase}, IsGameOver: {_isGameOver}");
    }

    //Ends the current game round.
    public void EndRound()
    {
        OnRoundEnd?.Invoke(_currentRound);
        Debug.Log($"[GameModel] Ended round {_currentRound}. Current Player Index: {_currentPlayerIndex}, Turn: {_turnNumber}, Starting Player Index: {_startingPlayerIndex}, Phase: {_currentPhase}, IsGameOver: {_isGameOver}");
    }

    //Ends the current round in simulation mode.
    //Differs from play mode since in play
    //Scoring must wait for the game controller
    //To properly handle animations.

    //TODO: MOVE END GAME TO SIMULATOR.
    public void SimEndRound()
    {
        _currentPhase = RoundPhaseEnum.ScoringPhase;
        for (int i = 0; i < _numberOfPlayers; i++)
        {
            _players[i].ScoreRound(_discard);
        }
        //if (CheckEndGameConditions())
        //{
        //    SimEndGame();
        //}
        //else
        //{
         //   _currentRound++;
        //    SimStartNewRound(_currentRound + 1);
        //}
    }

    //Starts a new game roun in simulation mode.
    //IMPORTANT: DONT TRIGGER NEW TURN
    public void SimStartRound()
    {
        SimStartNewRound(_currentRound);
    }

    //Starts a new turn.
    //Invoke turn start event with current turn number and player index.
    //Values should already be set by StartNewRound or EndTurn.
    public void StartTurn()
    {
        OnTurnStart?.Invoke(_turnNumber, _currentPlayerIndex);
        Debug.Log($"[GameModel] Started turn {_turnNumber} for player index {_currentPlayerIndex}. Current Round: {_currentRound}, Starting Player Index: {_startingPlayerIndex}, Phase: {_currentPhase}, IsGameOver: {_isGameOver}");
    }

    //Starts a new turn in simulation mode.
    public void SimStartTurn()
    {
        SimStartNewTurn(_turnNumber, _currentPlayerIndex);
    }

    //Ends the current turn.
    public void EndTurn()
    {
        //Invoke turn end event
        OnTurnEnd?.Invoke(_currentPlayerIndex, _turnNumber);
        _totalTurnsPlayed++;

        // Check if all displays are empty
        if (AreDisplaysEmpty())
        {
            // If so triggrt end of phase event
            StartScoringPhase();
            return;
        }
        Debug.Log($"[GameModel] Ended turn {_turnNumber} for player index {_currentPlayerIndex}. Current Round: {_currentRound}, Starting Player Index: {_startingPlayerIndex}, Phase: {_currentPhase}, IsGameOver: {_isGameOver}");
        //ADVANCE TO NEXT TURN
        _turnNumber++;
        //UPDATE CURRENT PLAYER INDEX
        _currentPlayerIndex = (_currentPlayerIndex + 1) % _numberOfPlayers;
        //TRiGGER TURN CHANGED
        if (!AreDisplaysEmpty()) StartTurn();
        
    }

    //Ends the current turn in simulation mode.
    //IMPORTANT: DONT TRIGGER NEW TURN
    public void SimEndTurn()
    {
        _totalTurnsPlayed++;
        _turnNumber++;
        _currentPlayerIndex = (_currentPlayerIndex + 1) % _numberOfPlayers;
        //if (!AreDisplaysEmpty()) SimStartNewTurn(turnNumber, currentPlayerIndex);
        //else SimEndRound();
    }

    //Ends the game.
    private void EndGame()
    {
        foreach (PlayerModel player in _players)
        {
            player.ApplyEndGameBonus();
            player.UpdateScore();
        }
        Debug.Log($"[GameModel] Game over. Final scores: {string.Join(", ", _players.Select(p => $"{p.PlayerName}: {p.Score}"))}");
        OnGameOver?.Invoke();
        _isGameOver = true;
    }

    public void SimEndGame()
    {
        foreach (PlayerModel player in _players)
        {
            player.ApplyEndGameBonus();
            player.UpdateScore();
        }
        _isGameOver = true;
    }

    //Checks end game conditions.
    public bool CheckEndGameConditions()
    {
        foreach (PlayerModel player in _players)
        {
            if (player.HasCompleteRow)
                return true;
        }
        return false;
    }

    //Checks end round conditions.
    public bool CheckEndRoundConditions()
    {
        if (AreDisplaysEmpty())
            return true; 
        return false;
    }

    //TODO: UNDO MOVE SECTION

    public void UndoMove()
    {
        throw new NotImplementedException();
    }
    
    //Undo the last turn.
    public void UndoTurn()
    {
        throw new NotImplementedException();
    }

    //Undo the last round.
    public void UndoRound()
    {
        throw new NotImplementedException();
    }

    //TODO: UNDO MOVE SECTION

    //Gets a list of all the possible moves for the current player.
    public List<GameMove> GetPossibleMoves()
    {
        List<GameMove> possibleMoves = MoveGenerator.GetAllValidMoves(this);
        return possibleMoves;
    }

    //Gets the current game state.
    public GameState GetGameState()
    {
        return SaveSnapshot();
    }

    public GameState SaveSnapshot()
    {
        GameState snap = GameState.Create(GameConfig.NUM_PLAYERS, GameConfig.NUM_DISPLAYS, GameConfig.FLOWERS_PER_DISPLAY);
        
        FlowerPiece[] _tempBag = _bag.flowers.ToArray<FlowerPiece>();
        int[] _tempBagInt = new int[_tempBag.Length];
        for (int i = 0; i < _tempBag.Length; i++)
        {
            _tempBagInt[i] = (int)_tempBag[i].Color;
        }
        Array.Copy(_tempBagInt, snap.Bag, Bag.Count);
        snap.BagCount = _bag.Count;

        FlowerPiece[] _tempDiscard = _discard.flowers.ToArray<FlowerPiece>();
        int[] _tempDiscardInt = new int[_tempDiscard.Length];
        for (int i = 0; i < _tempDiscard.Length; i++)
        {
            _tempDiscardInt[i] = (int)_tempDiscard[i].Color;
        }
        Array.Copy(_tempDiscardInt, snap.Discard, _discard.Count);
        snap.DiscardCount = _discard.Count;

        for (int f = 0; f < _factoryDisplays.Length; f++)
        {

            FlowerPiece[] _tempDisplay = _factoryDisplays[f].flowers.ToArray<FlowerPiece>();
            int[] _tempDisplayInt = new int[_tempDisplay.Length];
            for (int i = 0; i < _tempDisplay.Length; i++)
            {
                _tempDisplayInt[i] = (int)_tempDisplay[i].Color;
            }
            Array.Copy(_tempDisplayInt, snap.Factories[f], _factoryDisplays[f].Count);
            snap.FactoryCount[f] = _factoryDisplays[f].Count;
        }


        FlowerPiece[] _tempCentral = _centralDisplay.flowers.ToArray<FlowerPiece>();
        int[] _tempCentralInt = new int[_tempCentral.Length];
        for (int i = 0; i < _tempCentral.Length; i++)
        {
            _tempCentralInt[i] = (int)_tempCentral[i].Color;
        }
        Array.Copy(_tempCentralInt, snap.Central, _centralDisplay.Count);
        snap.CentralCount = _centralDisplay.Count;
        snap.CentralHasToken = _centralDisplay.HasFirstPlayerToken;

        snap.CurrentRound = _currentRound;
        snap.CurrentPlayer = _currentPlayerIndex;
        snap.TurnNumber = _turnNumber;
        snap.TotalTurnsPlayed = _totalTurnsPlayed;
        snap.StartingPlayer = _startingPlayerIndex;
        snap.IsGameOver = _isGameOver;
        snap.Phase = _currentPhase;

        if (_factoryFillColorCounts != null)
            Array.Copy(_factoryFillColorCounts, snap.FactoryFillColorCounts, Mathf.Min(_factoryFillColorCounts.Length, snap.FactoryFillColorCounts.Length));

        for (int i = 0; i < _players.Length; i++)
            snap.PlayerSnapshots[i] = _players[i].SaveSnapshot();
        return snap;
    }

    //Sets the current game state.
    public void SetGameState(GameState gameState)
    {
        Bag.Clear();
        foreach (int flower in gameState.Bag)
        {
            Bag.AddFlower(new FlowerPiece((FlowerColor)flower));
        }

        Discard.Clear();
        foreach (int flower in gameState.Discard)
        {
            Discard.AddFlower(new FlowerPiece((FlowerColor)flower));
        }


        for (int f = 0; f < gameState.FactoryCount.Length; f++)
        {
            FactoryDisplays[f].Clear();
            foreach (int flower in gameState.Factories[f])
            {
                FactoryDisplays[f].AddFlower(new FlowerPiece((FlowerColor)flower));
            }
        }

        CentralDisplay.Clear();
        foreach (int flower in gameState.Central)
        {   
            CentralDisplay.AddFlower(new FlowerPiece((FlowerColor)flower));
        }

        if (gameState.CentralHasToken)
            CentralDisplay.AddStartingPlayerToken();

        _currentRound = gameState.CurrentRound;
        _currentPlayerIndex = gameState.CurrentPlayer;
        _turnNumber = gameState.TurnNumber;
        _totalTurnsPlayed = gameState.TotalTurnsPlayed;
        _startingPlayerIndex = gameState.StartingPlayer;
        _isGameOver = gameState.IsGameOver;
        _currentPhase = gameState.Phase;

        if (_factoryFillColorCounts == null || _factoryFillColorCounts.Length != GameConfig.NUM_COLORS)
            _factoryFillColorCounts = new int[GameConfig.NUM_COLORS];

        Array.Clear(_factoryFillColorCounts, 0, _factoryFillColorCounts.Length);
        if (gameState.FactoryFillColorCounts != null)
        {
            int copyLength = Mathf.Min(_factoryFillColorCounts.Length, gameState.FactoryFillColorCounts.Length);
            Array.Copy(gameState.FactoryFillColorCounts, _factoryFillColorCounts, copyLength);
        }

        for (int i = 0; i < gameState.PlayerSnapshots.Length; i++)
            _players[i].RestoreSnapshot(ref gameState.PlayerSnapshots[i]);
        Debug.Log($"[GameModel] Game state set from snapshot. Round: {_currentRound}, Current Player Index: {_currentPlayerIndex}, Turn: {_turnNumber}, Starting Player Index: {_startingPlayerIndex}, Phase: {_currentPhase}, IsGameOver: {_isGameOver}");
    }

    //Gets the current playerIndex.
    public int GetCurrentPlayerIndex()
    {
        return _currentPlayerIndex;
    }

    void IGameModel.EndGame()
    {
        EndGame();
    }

    public int GetRoundNumber()
    {
        return _currentRound;
    }

    #endregion

    #region SIMULATOR LEGACY INTERFACE
    /// <summary>
    /// Special methods for simulator to drive game logic without triggering events or other side effects.
    /// </summary>
    public void SimGameSetup()
    {
        GameSetup();
    }
     
    public void SimStartNewRound(int roundNumber)
    {
        _currentPhase = RoundPhaseEnum.RoundSetup;
        _currentRound = roundNumber;
        _currentPlayerIndex = _startingPlayerIndex;
        _isGameOver = false;

        //Fill factory displays from bag
        for (int i = 0; i < _factoryDisplays.Length; i++)
        {
            FillFromBag(_factoryDisplays[i]);
        }
        _centralDisplay.Clear();
        _centralDisplay.AddStartingPlayerToken();
        _turnNumber = 1;

        _currentPhase = RoundPhaseEnum.PlacementPhase;
        SimStartNewTurn(_turnNumber, _currentPlayerIndex);
    }
    
    public void SimStartNewTurn(int turnNumber, int playerIndex)
    {
        _currentPlayerIndex = playerIndex;
        this._turnNumber = turnNumber;

    }

    #endregion

    #region LEGACY GAMELOGIC PRE-INTERFACE

    //Start a new round.
    // Procedure:
    // 1. Set current phase to RoundSetup
    // 2. Set current round number and player index.
    // 3. Fill factory displays from bag (if bag runs out, refill from discard)
    // 4. Clear central display
    // 5. Add starting player token to central display
    // 6. Set current player to starting player
    // 7. Set turn number to 1
    // 8. Set current phase to RoundSetup
    // 9. Trigger round start event 

    public void StartNewRound(int roundNumber)
    { 
        _currentRound = roundNumber;
        _currentPhase = RoundPhaseEnum.RoundSetup;
        OnPhaseStart?.Invoke(_currentPhase);
        
        _currentPlayerIndex = _startingPlayerIndex;
        _isGameOver = false;

        //Fill factory displays from bag
        UnityEngine.Debug.Log($"[GameModel] StartNewRound({roundNumber}) – filling {_factoryDisplays.Length} factories from bag ({_bag.Count} flowers).");
        for (int i = 0; i < _factoryDisplays.Length; i++)
        {
            FillFromBag(_factoryDisplays[i]);
            UnityEngine.Debug.Log($"[GameModel]   Factory {i}: {_factoryDisplays[i].Count} flowers (bag remaining: {_bag.Count}).");
        }
        _centralDisplay.Clear();
        _centralDisplay.AddStartingPlayerToken();
        _turnNumber = 1;

        //Trigger round start event
        UnityEngine.Debug.Log($"[GameModel] Firing OnRoundStart (subscribers: {(OnRoundStart != null ? "yes" : "none")}).");
        OnRoundStart?.Invoke(_currentRound);
        _currentPhase = RoundPhaseEnum.PlacementPhase;
        OnPhaseStart?.Invoke(_currentPhase);
        StartTurn();
    }

    private void FillFromBag(DisplayModel displayModel)
    {
        for (int i = 0; i < GameConfig.FLOWERS_PER_DISPLAY; i++)
        {
            if (_bag.Count == 0)
            {
                //If bag is empty, refill from discard
                RefillBagFromDiscard(_bag, _discard);
                if (_bag.Count == 0)
                {
                    //If bag is still empty after refilling, stop filling
                    break;
                }
            }
            FlowerPiece flower = _bag.Draw();
            displayModel.AddFlower(flower);
            int colorIndex = (int)flower.Color;
            if (colorIndex >= 0 && colorIndex < _factoryFillColorCounts.Length)
                _factoryFillColorCounts[colorIndex]++;
        }
    }

    private void RefillBagFromDiscard(BagModel bag, DiscardModel discard)
    {
        for (int i = 0; i < discard.Count; i++)
        {
            bag.AddFlower(discard.flowers[i]);

        }
        discard.TakeAll(); //Clear discard after refilling bag
    }

    public bool AreDisplaysEmpty()
    {
        foreach (var display in _factoryDisplays)
        {
            if (!display.IsEmpty)
                return false;
        }

        // Central is considered empty when it has no regular flowers regardless of the token.
        if (_centralDisplay.Count > 0)
            return false;

        return true;
    }

    private void StartScoringPhase()
    {
        _currentPhase = RoundPhaseEnum.ScoringPhase;
        OnPhaseStart?.Invoke(_currentPhase);
        // Scoring is driven externally by GameController coroutine
        // so boards can rotate and animations can play between players.
        OnScoringPhaseReady?.Invoke();
    }


    // Scores a single player's round. Returns the GridPlacementResults without firing any events
    // so GameController can pace them flower-by-flower with delays.
    // Also updates startingPlayerIndex if this player holds the first player token.
    // Returns true if the game-end condition is triggered.
    public bool ScorePlayer(int i, out List<GridPlacementResult> results)
    {
        results = _players[i].ScoreRound(_discard);

        if (_players[i].HasFirstPlayerToken)
            _startingPlayerIndex = i;

        return _players[i].HasCompleteRow;
    }

    public void FinalizeScoringPhase(bool gameEndConditionMet)
    {
        EndRound();
        if (gameEndConditionMet)
            EndGame();
        else
            StartNewRound(_currentRound + 1);
    }

    public PickResult PickFromFactory(int factoryIndex, FlowerColor color)
    {
        if (factoryIndex < 0 || factoryIndex >= _factoryDisplays.Length)
            return default;

        PickResult result = _factoryDisplays[factoryIndex].PickColor(color);

        foreach (FlowerPiece remaining in result.Remainingflowers)
        {
            _centralDisplay.AddFlower(remaining);
        }

        return result;
    }
    public PickResult PickFromCentral(FlowerColor color)
    {
        PickResult result = _centralDisplay.PickColor(color);

        // Central display keeps its non-picked flowers — add them back.
        // (PickColor clears everything; remaining flowers stay in central.)
        foreach (FlowerPiece remaining in result.Remainingflowers)
            _centralDisplay.AddFlower(remaining);

        return result;
    }

    #endregion

    #region HELPER METHODS

    public int GetFactoryFillColorCount(FlowerColor color)
    {
        int colorIndex = (int)color;
        if (_factoryFillColorCounts == null || colorIndex < 0 || colorIndex >= _factoryFillColorCounts.Length)
            return 0;

        return _factoryFillColorCounts[colorIndex];
    }

    internal PlayerModel GetCurrentPlayer()
    {
        return _players[_currentPlayerIndex];
    }

    public void ExecuteMove(GameMove move)
    {
        throw new NotImplementedException("ExecuteMove is only supported on MinimalGM.");
    }

    #endregion

    #region VALIDATION
    #endregion
}
